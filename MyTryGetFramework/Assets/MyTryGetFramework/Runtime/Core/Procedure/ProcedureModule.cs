using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    public sealed class ProcedureModule : IProcedureModule
    {
        private readonly Dictionary<string, IProcedure> _procedures = new Dictionary<string, IProcedure>();
        private readonly List<string> _stack = new List<string>();
        private IModuleHost _host;

        private bool _isEntering;
        private bool _isExiting;
        private int _pendingExitCount;
        private int _exitVersion;
        private Exception _lastAsyncError;

        public int Priority => -200;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string CurrentProcedure => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool IsRunning => _stack.Count > 0;
        public int StackDepth => _stack.Count;
        public IModuleHost Host => _host;
        public bool IsEntering => _isEntering;
        public bool IsExiting => _isExiting;
        public Exception LastAsyncError => _lastAsyncError;

        public void OnInit(IModuleHost host) { _host = host; }

        public void Shutdown()
        {
            if (_stack.Count > 0)
            {
                for (int i = _stack.Count - 1; i >= 0; i--)
                {
                    var proc = _procedures[_stack[i]];
                    try { proc.OnExit(this); } catch { }
                }
                _stack.Clear();
            }
            _procedures.Clear();
            _host = null;
            _isEntering = false;
            _isExiting = false;
            _pendingExitCount = 0;
            _exitVersion++;
            _lastAsyncError = null;
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            if (_isEntering || _isExiting) return;
            if (_stack.Count == 0) return;
            var current = _procedures[_stack[_stack.Count - 1]];
            current.OnUpdate(this, deltaTime, unscaledDeltaTime);
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
            if (_stack.Count > 0)
                throw new InvalidOperationException(
                    $"ProcedureModule already started (current: '{CurrentProcedure}'). Call Stop first.");
            EnterProcedure(initial);
        }

        public void Push(string target)
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");

            var currentProc = _procedures[_stack[_stack.Count - 1]];
            currentProc.OnPause(this);
            EnterProcedure(target);
        }

        public void Pop()
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule stack is empty.");

            ExitTop(() =>
            {
                if (_stack.Count > 0)
                {
                    var resumed = _procedures[_stack[_stack.Count - 1]];
                    resumed.OnResume(this);
                }
            });
        }

        public void Replace(string target)
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");
            if (!_procedures.ContainsKey(target))
                throw new InvalidOperationException($"Procedure '{target}' not registered.");

            ExitTop(() => EnterProcedure(target));
        }

        public void Stop()
        {
            if (_stack.Count == 0) return;

            _exitVersion++;
            _isEntering = false;

            if (_isExiting)
            {
                _pendingExitCount = 0;
                _stack.Clear();
                return;
            }

            _pendingExitCount = 0;

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var proc = _procedures[_stack[i]];
                ExitForStop(proc);
            }

            _stack.Clear();
        }

        private void ExitForStop(IProcedure proc)
        {
            if (proc is IAsyncProcedure asyncProc)
            {
                ExitAsyncForStop(asyncProc);
                return;
            }

            try { proc.OnExit(this); }
            catch (Exception ex) { _lastAsyncError = ex; }
        }

        private void ExitAsyncForStop(IAsyncProcedure asyncProc)
        {
            try
            {
                var version = _exitVersion;
                var task = asyncProc.OnExitAsync(this);
                if (task.IsCompleted)
                {
                    try { task.GetAwaiter().GetResult(); }
                    catch (Exception ex) { _lastAsyncError = ex; }
                    try { asyncProc.OnExit(this); } catch { }
                    return;
                }

                _pendingExitCount++;
                _isExiting = true;
                task.GetAwaiter().OnCompleted(() =>
                {
                    if (version != _exitVersion)
                        return;

                    try { task.GetAwaiter().GetResult(); }
                    catch (Exception ex) { _lastAsyncError = ex; }
                    try { asyncProc.OnExit(this); } catch { }
                    _pendingExitCount--;
                    if (_pendingExitCount == 0)
                        _isExiting = false;
                });
            }
            catch (Exception ex)
            {
                _lastAsyncError = ex;
                try { asyncProc.OnExit(this); } catch { }
            }
        }

        private void EnterProcedure(string id)
        {
            if (!_procedures.TryGetValue(id, out var proc))
                throw new InvalidOperationException($"Procedure '{id}' not registered.");

            _lastAsyncError = null;
            _stack.Add(id);
            proc.OnEnter(this);

            if (proc is IAsyncProcedure asyncProc)
                BeginAsyncEnter(asyncProc);
        }

        private void ExitTop(Action afterExit)
        {
            var id = _stack[_stack.Count - 1];
            var proc = _procedures[id];

            if (proc is IAsyncProcedure asyncProc)
            {
                BeginSyncExit(id, asyncProc, afterExit);
            }
            else
            {
                RemoveTop(id);
                proc.OnExit(this);
                afterExit?.Invoke();
            }
        }

        private void BeginSyncExit(string id, IAsyncProcedure asyncProc, Action afterExit)
        {
            int version = ++_exitVersion;
            _isExiting = true;
            TGTask task;
            try { task = asyncProc.OnExitAsync(this); }
            catch (Exception ex)
            {
                _lastAsyncError = ex;
                RemoveTop(id);
                _isExiting = false;
                try { asyncProc.OnExit(this); } catch { }
                afterExit?.Invoke();
                return;
            }

            if (task.IsCompleted)
            {
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                RemoveTop(id);
                try { asyncProc.OnExit(this); } catch { }
                _isExiting = false;
                afterExit?.Invoke();
                return;
            }

            task.GetAwaiter().OnCompleted(() =>
            {
                bool isCurrentExit = version == _exitVersion;

                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; }
                if (isCurrentExit)
                    RemoveTop(id);
                try { asyncProc.OnExit(this); } catch { }
                _isExiting = false;
                if (isCurrentExit)
                    afterExit?.Invoke();
            });
        }

        private void RemoveTop(string id)
        {
            int index = _stack.Count - 1;
            if (index >= 0 && _stack[index] == id)
                _stack.RemoveAt(index);
        }

        private void BeginAsyncEnter(IAsyncProcedure asyncProc)
        {
            _isEntering = true;
            TGTask task;
            try { task = asyncProc.OnEnterAsync(this); }
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

        private void ThrowIfAsync()
        {
            if (_isEntering || _isExiting)
                throw new InvalidOperationException(
                    "Cannot perform stack operation while async Enter/Exit is in progress.");
        }
    }
}
