using System;
using System.Collections.Generic;

namespace TryGet.Async
{
    public readonly partial struct TGTask
    {
        /// <summary>
        /// 等待所有 <paramref name="tasks"/> 完成（ADR-0021 D10 组合子）。任一抛异常则以
        /// <see cref="AggregateException"/> 聚合所有异常完成；空集合立即完成。单线程实现，无并发。
        /// </summary>
        public static TGTask WhenAll(params TGTask[] tasks)
        {
            if (tasks == null || tasks.Length == 0) return CompletedTask;

            var tcs = new TGTaskCompletionSource();
            int remaining = tasks.Length;
            List<Exception> errors = null;

            for (int i = 0; i < tasks.Length; i++)
            {
                var awaiter = tasks[i].GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    try { awaiter.GetResult(); }
                    catch (Exception ex)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(ex);
                    }

                    remaining--;
                    if (remaining == 0)
                    {
                        if (errors != null)
                            tcs.SetException(new AggregateException("One or more tasks failed in WhenAll.", errors));
                        else
                            tcs.SetResult();
                    }
                });
            }

            return tcs.Task;
        }

        /// <summary><see cref="WhenAll(TGTask[])"/> 的 IEnumerable 版本。</summary>
        public static TGTask WhenAll(IEnumerable<TGTask> tasks)
        {
            if (tasks == null) return CompletedTask;
            var list = new List<TGTask>(tasks);
            return WhenAll(list.ToArray());
        }

        /// <summary>
        /// 等待 <paramref name="tasks"/> 中第一个完成的任务（ADR-0021 D10 组合子），返回其索引。
        /// 第一个完成者若抛异常，则以该异常完成；其余任务不被强制取消（调用方用自己的 token 决定）。
        /// 至少需要一个任务。
        /// </summary>
        public static TGTask<int> WhenAny(params TGTask[] tasks)
        {
            if (tasks == null || tasks.Length == 0)
                throw new ArgumentException("WhenAny requires at least one task.", nameof(tasks));

            var tcs = new TGTaskCompletionSource<int>();
            bool done = false;

            for (int i = 0; i < tasks.Length; i++)
            {
                int index = i;
                var awaiter = tasks[i].GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    if (done)
                    {
                        // 已有胜者：仍需消费此任务的结果（让其 body 回池），但忽略
                        try { awaiter.GetResult(); } catch { }
                        return;
                    }
                    done = true;
                    try { awaiter.GetResult(); }
                    catch (Exception ex) { tcs.SetException(ex); return; }
                    tcs.SetResult(index);
                });
            }

            return tcs.Task;
        }
    }
}
