using System;
using System.Collections.Generic;
using TryGet.Async;

namespace TryGet
{
    public sealed class ProcedureModule : IProcedureModule
    {
        private readonly Dictionary<string, IProcedure> _procedures = new Dictionary<string, IProcedure>();
        private readonly List<string> _stack = new List<string>();
        private IModuleSystem _host;

        private bool _isEntering;
        private bool _isExiting;
        private int _pendingExitCount;
        private int _exitVersion;
        private Exception _lastAsyncError;
        private TGTaskCompletionSource _activeTransition;

        public int Priority => -200;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string CurrentProcedure => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool IsRunning => _stack.Count > 0;
        public int StackDepth => _stack.Count;
        public IModuleSystem Host => _host;
        public bool IsEntering => _isEntering;
        public bool IsExiting => _isExiting;
        public Exception LastAsyncError => _lastAsyncError;

        public void OnInit(IModuleSystem host) { _host = host; }

        public void Shutdown()
        {
            CancelActiveTransition();
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

        public TGTask Start(string initial)
        {
            if (_stack.Count > 0)
                throw new InvalidOperationException(
                    $"ProcedureModule already started (current: '{CurrentProcedure}'). Call Stop first.");
            return RunTransition(onDone => EnterProcedure(initial, onDone));
        }

        public TGTask Push(string target)
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");
            if (!_procedures.ContainsKey(target))
                throw new InvalidOperationException($"Procedure '{target}' not registered.");

            return RunTransition(onDone =>
            {
                // 先暂停当前 Procedure 再进入 target。OnPause 抛异常时：与异步失败一致——经切换 task
                // 上报错误（不逃逸），中止本次 Push，current 仍为栈顶（栈未变）。
                var currentProc = _procedures[_stack[_stack.Count - 1]];
                try { currentProc.OnPause(this); }
                catch (Exception ex)
                {
                    _lastAsyncError = ex;
                    onDone(ex);
                    return;
                }
                EnterProcedure(target, onDone);
            });
        }

        public TGTask Pop()
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule stack is empty.");

            return RunTransition(onDone =>
                ExitTop(err =>
                {
                    if (err == null && _stack.Count > 0)
                    {
                        // 栈顶已退出，resumed 已是新栈顶。OnResume 抛异常时：与异步失败一致——经切换
                        // task 上报错误（不逃逸），栈状态保持（已退出不可逆，恢复用 Stop）。
                        var resumed = _procedures[_stack[_stack.Count - 1]];
                        try { resumed.OnResume(this); }
                        catch (Exception ex)
                        {
                            _lastAsyncError = ex;
                            err = ex;
                        }
                    }
                    onDone(err);
                }));
        }

        public TGTask Replace(string target)
        {
            ThrowIfAsync();
            if (_stack.Count == 0)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");
            if (!_procedures.ContainsKey(target))
                throw new InvalidOperationException($"Procedure '{target}' not registered.");

            return RunTransition(onDone =>
                ExitTop(exitErr =>
                {
                    // exit 失败：不进入替换流程，错误直接结束本次切换
                    if (exitErr != null) { onDone(exitErr); return; }
                    EnterProcedure(target, onDone);
                }));
        }

        public void Stop()
        {
            if (_stack.Count == 0) return;

            CancelActiveTransition();
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

        /// <summary>
        /// C4：把一次「可能含异步 enter/exit」的切换包装成可 await 的 TGTask。
        /// 同步全程走完 → 立即完成（CompletedTask / FromException）；异步未完成 → 返回延迟完成的 tcs.Task，
        /// 并存入 <see cref="_activeTransition"/> 以便 <see cref="Stop"/> / <see cref="Shutdown"/> 打断时取消。
        /// onComplete(error) 由切换链在其真正完成点（同步立即 / 异步 OnCompleted / 错误）调用一次。
        /// </summary>
        private TGTask RunTransition(Action<Action<Exception>> start)
        {
            bool syncDone = false;
            Exception syncError = null;
            TGTaskCompletionSource tcs = null;

            void OnComplete(Exception error)
            {
                if (tcs == null)
                {
                    // 切换在 start() 内同步完成
                    syncDone = true;
                    syncError = error;
                    return;
                }

                // 正常完成：先摘除 active 引用（避免后续 Stop 误取消已完成的切换）
                if (ReferenceEquals(_activeTransition, tcs))
                    _activeTransition = null;
                if (error != null)
                    tcs.SetException(error);
                else
                    tcs.SetResult();
            }

            start(OnComplete);

            if (syncDone)
                return syncError != null ? TGTask.FromException(syncError) : TGTask.CompletedTask;

            // 异步未完成：建立 tcs，OnComplete 将在未来帧完成它；记录为活动切换以便打断时取消
            tcs = new TGTaskCompletionSource();
            _activeTransition = tcs;
            return tcs.Task;
        }

        /// <summary>
        /// 取消当前进行中的异步切换（被 Stop / Shutdown 打断时调用），让其 transition task 以
        /// OperationCanceledException 完成，避免 await 者永久挂起。
        /// </summary>
        private void CancelActiveTransition()
        {
            var pending = _activeTransition;
            if (pending == null) return;
            _activeTransition = null;
            pending.SetCanceled();
        }

        private void EnterProcedure(string id, Action<Exception> onComplete)
        {
            if (!_procedures.TryGetValue(id, out var proc))
                throw new InvalidOperationException($"Procedure '{id}' not registered.");

            _lastAsyncError = null;
            _stack.Add(id);
            try { proc.OnEnter(this); }
            catch (Exception ex)
            {
                // 同步 OnEnter 失败：与 BeginAsyncEnter 的异步失败一致——记录错误、经切换 task 上报
                // （不逃逸），proc 保留在栈上（"已进入但出错"，恢复用 Stop）；跳过 OnEnterAsync。
                _lastAsyncError = ex;
                onComplete(ex);
                return;
            }

            if (proc is IAsyncProcedure asyncProc)
                BeginAsyncEnter(asyncProc, onComplete);
            else
                onComplete(null);
        }

        private void ExitTop(Action<Exception> onComplete)
        {
            var id = _stack[_stack.Count - 1];
            var proc = _procedures[id];

            if (proc is IAsyncProcedure asyncProc)
            {
                BeginSyncExit(id, asyncProc, onComplete);
            }
            else
            {
                // 同步 OnExit 失败：与 BeginSyncExit 的异步失败一致——栈顶照常移除（退出不可逆），
                // 记录错误、经切换 task 上报（不逃逸）。Replace 在 err != null 时不会进入替换目标。
                RemoveTop(id);
                Exception err = null;
                try { proc.OnExit(this); }
                catch (Exception ex) { _lastAsyncError = ex; err = ex; }
                onComplete(err);
            }
        }

        private void BeginSyncExit(string id, IAsyncProcedure asyncProc, Action<Exception> onComplete)
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
                onComplete(ex);
                return;
            }

            if (task.IsCompleted)
            {
                Exception err = null;
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; err = ex; }
                RemoveTop(id);
                try { asyncProc.OnExit(this); } catch { }
                _isExiting = false;
                onComplete(err);
                return;
            }

            task.GetAwaiter().OnCompleted(() =>
            {
                bool isCurrentExit = version == _exitVersion;

                Exception err = null;
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; err = ex; }
                if (isCurrentExit)
                    RemoveTop(id);
                try { asyncProc.OnExit(this); } catch { }
                _isExiting = false;
                if (isCurrentExit)
                    onComplete(err);
            });
        }

        private void RemoveTop(string id)
        {
            int index = _stack.Count - 1;
            if (index >= 0 && _stack[index] == id)
                _stack.RemoveAt(index);
        }

        private void BeginAsyncEnter(IAsyncProcedure asyncProc, Action<Exception> onComplete)
        {
            _isEntering = true;
            TGTask task;
            try { task = asyncProc.OnEnterAsync(this); }
            catch (Exception ex)
            {
                _lastAsyncError = ex;
                _isEntering = false;
                onComplete(ex);
                return;
            }

            if (task.IsCompleted)
            {
                Exception err = null;
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; err = ex; }
                _isEntering = false;
                onComplete(err);
                return;
            }

            task.GetAwaiter().OnCompleted(() =>
            {
                Exception err = null;
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { _lastAsyncError = ex; err = ex; }
                _isEntering = false;
                onComplete(err);
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
