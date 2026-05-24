using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    /// <summary>
    /// IProcedureModule 默认实现。基于 string 状态键的跨帧状态机。
    /// V0.6 Iter 7 起支持 <see cref="IAsyncProcedure"/> 异步路径。
    /// </summary>
    public sealed class ProcedureModule : IProcedureModule
    {
        private readonly Dictionary<string, IProcedure> _procedures = new Dictionary<string, IProcedure>();
        private IProcedure _current;
        private string _currentId;
        private IModuleHost _host;
        private bool _isTransitioning;

        // V0.6 Iter 7：异步生命周期状态
        private bool _isEntering;
        private bool _isExiting;
        private Exception _lastAsyncError;

        public int Priority => -200;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string CurrentState => _currentId;
        public bool IsRunning => _current != null;
        public IModuleHost Host => _host;

        public bool IsEntering => _isEntering;
        public bool IsExiting => _isExiting;
        public Exception LastAsyncError => _lastAsyncError;

        public void OnInit(IModuleHost host) { _host = host; }

        public void Shutdown()
        {
            if (_current != null)
            {
                try { _current.OnExit(this); }
                catch { /* Shutdown 路径吞异常 */ }
            }
            _current = null;
            _currentId = null;
            _procedures.Clear();
            _host = null;
            _isEntering = false;
            _isExiting = false;
            _lastAsyncError = null;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (_isEntering || _isExiting) return;
            _current?.OnUpdate(this, deltaTime, unscaledDeltaTime);
        }

        public void AddProcedure(string id, IProcedure procedure)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Procedure id must be non-empty.", nameof(id));
            if (procedure == null)
                throw new ArgumentNullException(nameof(procedure));
            if (_procedures.ContainsKey(id))
                throw new InvalidOperationException($"Procedure '{id}' already registered.");

            _procedures[id] = procedure;
        }

        public void Start(string initial)
        {
            if (_current != null)
                throw new InvalidOperationException(
                    $"ProcedureModule already started (current: '{_currentId}'). Call Stop first or use TransitionTo.");
            if (string.IsNullOrEmpty(initial))
                throw new ArgumentException("Initial procedure id must be non-empty.", nameof(initial));
            if (!_procedures.TryGetValue(initial, out var procedure))
                throw new InvalidOperationException($"Procedure '{initial}' not registered.");

            _current = procedure;
            _currentId = initial;
            _lastAsyncError = null;
            _current.OnEnter(this);

            if (_current is IAsyncProcedure asyncProc)
            {
                BeginAsyncEnter(asyncProc);
            }
        }

        public void TransitionTo(string target)
        {
            if (_current == null)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");
            if (_isEntering || _isExiting)
                throw new InvalidOperationException(
                    "Cannot TransitionTo while current procedure is in async Enter/Exit. Wait until async phase completes.");
            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("Target procedure id must be non-empty.", nameof(target));
            if (!_procedures.TryGetValue(target, out var next))
                throw new InvalidOperationException($"Procedure '{target}' not registered.");
            if (_isTransitioning)
                throw new InvalidOperationException(
                    "Re-entrant TransitionTo: cannot call TransitionTo from within OnEnter / OnExit. " +
                    "If you need conditional re-transition, defer to next OnUpdate.");

            _isTransitioning = true;
            try
            {
                var prev = _current;
                _lastAsyncError = null;

                if (prev is IAsyncProcedure asyncPrev)
                {
                    BeginAsyncExit(asyncPrev, () => SwitchTo(next, target));
                }
                else
                {
                    try { prev.OnExit(this); }
                    catch { throw; }
                    SwitchTo(next, target);
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public void Stop()
        {
            if (_current == null)
                return;

            if (_current is IAsyncProcedure asyncProc)
            {
                if (_isEntering || _isExiting)
                {
                    try { _current.OnExit(this); }
                    catch { }
                    _current = null;
                    _currentId = null;
                    _isEntering = false;
                    _isExiting = false;
                    return;
                }

                BeginAsyncExit(asyncProc, () =>
                {
                    _current = null;
                    _currentId = null;
                });
                return;
            }

            try { _current.OnExit(this); }
            catch { }
            _current = null;
            _currentId = null;
        }

        // ----------------- V0.6 Iter 7 async helpers -----------------

        private void BeginAsyncEnter(IAsyncProcedure asyncProc)
        {
            _isEntering = true;
            TGTask task;
            try
            {
                task = asyncProc.OnEnterAsync(this);
            }
            catch (Exception ex)
            {
                _lastAsyncError = ex;
                _isEntering = false;
                return;
            }

            if (task.IsCompleted)
            {
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                _isEntering = false;
                return;
            }

            task.GetAwaiter().OnCompleted(() =>
            {
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                _isEntering = false;
            });
        }

        private void BeginAsyncExit(IAsyncProcedure asyncProc, Action afterExit)
        {
            _isExiting = true;
            TGTask task;
            try
            {
                task = asyncProc.OnExitAsync(this);
            }
            catch (Exception ex)
            {
                _lastAsyncError = ex;
                _isExiting = false;
                try { asyncProc.OnExit(this); }
                catch (Exception innerEx) { _lastAsyncError = innerEx; }
                afterExit?.Invoke();
                return;
            }

            if (task.IsCompleted)
            {
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                try { asyncProc.OnExit(this); }
                catch (Exception ex) { _lastAsyncError = ex; }
                _isExiting = false;
                afterExit?.Invoke();
                return;
            }

            task.GetAwaiter().OnCompleted(() =>
            {
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                try { asyncProc.OnExit(this); }
                catch (Exception ex) { _lastAsyncError = ex; }
                _isExiting = false;
                afterExit?.Invoke();
            });
        }

        private void SwitchTo(IProcedure next, string targetId)
        {
            _current = next;
            _currentId = targetId;
            _current.OnEnter(this);

            if (_current is IAsyncProcedure asyncNext)
            {
                BeginAsyncEnter(asyncNext);
            }
        }
    }
}
