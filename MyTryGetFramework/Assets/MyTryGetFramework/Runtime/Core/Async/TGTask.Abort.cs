using System;

namespace TryGet.Async
{
    public readonly partial struct TGTask
    {
        /// <summary>
        /// 句柄式取消（ADR-0021 D2，对标 hsenl <c>HTask.Abort</c>）：把仍 pending 的**自建（Manual）** task
        /// 置为已取消（以 <see cref="TGTaskAbortException"/> 完成），await 处会抛出并展开整条 async 链。
        ///
        /// - 已完成 / 已过期（version 不匹配）/ <see langword="default"/> 为 no-op。
        /// - 对系统构建的 <see cref="TGTaskType.Builder"/> task（async 方法返回值）调用会抛
        ///   <see cref="InvalidOperationException"/>——外部不应取消别人 async 方法的返回 task，
        ///   要取消请改用 <see cref="TGCancelToken"/> 或在方法体内响应取消。
        /// </summary>
        public void Abort()
        {
            if (Body == null) return;
            if (Version != Body.Version) return; // 过期：已被消费/回收
            if (Body.IsCompleted) return;
            if (TaskType == TGTaskType.Builder)
                throw new InvalidOperationException(
                    "Cannot Abort a builder-created TGTask (an async method's return value). " +
                    "Abort only a self-created (Manual) task, e.g. one returned by a scheduler API " +
                    "or TGTaskCompletionSource; or cancel via TGCancelToken.");
            Body.SetException(new TGTaskAbortException());
        }
    }

    public readonly partial struct TGTask<T>
    {
        /// <summary>泛型版本，语义同 <see cref="TGTask.Abort"/>。</summary>
        public void Abort()
        {
            if (Body == null) return;
            if (Version != Body.Version) return;
            if (Body.IsCompleted) return;
            if (TaskType == TGTaskType.Builder)
                throw new InvalidOperationException(
                    "Cannot Abort a builder-created TGTask<T> (an async method's return value). " +
                    "Abort only a self-created (Manual) task; or cancel via TGCancelToken.");
            Body.SetException(new TGTaskAbortException());
        }
    }
}
