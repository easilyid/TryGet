using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// IProcedureModule 默认实现。基于 string 状态键的跨帧状态机。
    /// </summary>
    public sealed class ProcedureModule : IProcedureModule
    {
        private readonly Dictionary<string, IProcedure> _procedures = new Dictionary<string, IProcedure>();
        private IProcedure _current;
        private string _currentId;

        // 介于 EntityWorld (-100) 与业务 Module (0) 之间：
        // Procedure 在 World 启动后驱动游戏流程，但业务 Module 可依赖 IProcedureModule 拉取当前状态。
        public int Priority => -200;
        public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();

        public string CurrentState => _currentId;
        public bool IsRunning => _current != null;

        public void OnInit(IModuleHost host) { }

        public void Shutdown()
        {
            if (_current != null)
            {
                try { _current.OnExit(this); }
                catch { /* Shutdown 路径吞异常，让其他 Module Shutdown 继续走 */ }
            }
            _current = null;
            _currentId = null;
            _procedures.Clear();
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
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
            _current.OnEnter(this);
        }

        public void TransitionTo(string target)
        {
            if (_current == null)
                throw new InvalidOperationException("ProcedureModule not started. Call Start first.");
            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("Target procedure id must be non-empty.", nameof(target));
            if (!_procedures.TryGetValue(target, out var next))
                throw new InvalidOperationException($"Procedure '{target}' not registered.");

            // 同状态切换允许（Exit → Enter 重启语义）
            var prev = _current;
            prev.OnExit(this);
            _current = next;
            _currentId = target;
            _current.OnEnter(this);
        }

        public void Stop()
        {
            if (_current == null)
                return;

            _current.OnExit(this);
            _current = null;
            _currentId = null;
        }
    }
}
