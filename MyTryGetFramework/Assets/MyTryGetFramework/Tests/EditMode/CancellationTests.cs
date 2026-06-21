using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// TGTask 取消模型（ADR-0021）阶段 1 测试：核心类型 TGCancelToken/TGCancelSource/TGCancelRegistration
    /// 与 TGTaskScheduler 的 TGCancelToken 重载。
    ///
    /// async 测试采用同步驱动：启动 async TGTask（Forget），手动 <see cref="TGTaskScheduler.Update"/> 推进，
    /// 用 catch 到的 OperationCanceledException 标志位断言（TGTask 非 BCL Task，无法用 NUnit async）。
    /// </summary>
    [TestFixture]
    public class CancellationTests
    {
        // ========== 核心类型（issue 02） ==========

        [Test]
        public void CancelToken_Default_IsNone()
        {
            Assert.IsFalse(TGCancelToken.None.IsCancellationRequested);
            Assert.IsFalse(default(TGCancelToken).IsCancellationRequested);
            // None.Register 返回 inert registration，Dispose 不抛
            var reg = TGCancelToken.None.Register(() => { });
            Assert.DoesNotThrow(() => reg.Dispose());
        }

        [Test]
        public void Register_AfterCancelled_InvokesImmediately()
        {
            var s = TGCancelSource.Rent();
            s.Cancel();
            bool called = false;
            s.Token.Register(() => called = true);
            Assert.IsTrue(called, "已取消的 token 上 Register 应立即同步调用");
        }

        [Test]
        public void Cancel_InvokesAllOnce_SecondCancelNoOp()
        {
            var s = TGCancelSource.Rent();
            int c = 0;
            s.Token.Register(() => c++);
            s.Token.Register(() => c++);

            s.Cancel();
            Assert.AreEqual(2, c, "两个回调各调一次");

            s.Cancel();
            Assert.AreEqual(2, c, "第二次 Cancel 为 no-op");
        }

        [Test]
        public void Cancel_CallbackThrows_OthersStillInvoked()
        {
            var s = TGCancelSource.Rent();
            int c = 0;
            s.Token.Register(() => c++);
            s.Token.Register(() => throw new InvalidOperationException("boom"));

            Assert.DoesNotThrow(() => s.Cancel());
            Assert.AreEqual(1, c, "单个取消回调异常不应阻断其余回调");
        }

        [Test]
        public void Cancel_RegisterInsideCallback_InvokesImmediately()
        {
            var s = TGCancelSource.Rent();
            bool innerCalled = false;
            s.Token.Register(() => s.Token.Register(() => innerCalled = true));

            s.Cancel();

            Assert.IsTrue(innerCalled, "取消回调内再次 Register 时，因 token 已取消应立即同步触发");
        }

        [Test]
        public void Cancel_DisposeOtherRegistrationInsideCallback_DoesNotCrash()
        {
            var s = TGCancelSource.Rent();
            var other = s.Token.Register(() => { });
            s.Token.Register(() => other.Dispose());

            Assert.DoesNotThrow(() => s.Cancel(), "取消回调内注销其他 registration 不应破坏 Cancel 遍历");
        }

        [Test]
        public void Registration_Dispose_RemovesCallback()
        {
            var s = TGCancelSource.Rent();
            int c = 0;
            var r = s.Token.Register(() => c++);
            r.Dispose();
            s.Cancel();
            Assert.AreEqual(0, c, "已 Dispose 的注册不应被触发");
        }

        [Test]
        public void Source_Recycle_VersionInvalidatesOldTokens()
        {
            var s = TGCancelSource.Rent();
            var oldToken = s.Token;
            s.Recycle();
            Assert.IsFalse(oldToken.IsCancellationRequested, "Recycle 后旧 token 视为死 token（非误判已取消）");

            var s2 = TGCancelSource.Rent(); // 可能复用同一对象
            s2.Cancel();
            Assert.IsFalse(oldToken.IsCancellationRequested, "source 复用并取消后，旧世代 token 仍为死 token");
        }

        [Test]
        public void Token_ThrowIfCancellationRequested_ThrowsWhenCancelled()
        {
            var s = TGCancelSource.Rent();
            Assert.DoesNotThrow(() => s.Token.ThrowIfCancellationRequested(), "未取消时 no-op");
            s.Cancel();
            Assert.Throws<TGTaskAbortException>(() => s.Token.ThrowIfCancellationRequested(), "取消后应抛 OperationCanceledException 子类");
        }

        // ========== Scheduler token 重载（issue 03） ==========

        [Test]
        public void Delay_WithToken_AlreadyCancelled_NoEnqueue()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            s.Cancel();

            var t = sched.Delay(5f, s.Token);

            Assert.IsTrue(t.IsCompleted, "已取消 token 的 Delay 立即完成");
            Assert.AreEqual(0, sched.PendingDelayCount, "不入队");
        }

        [Test]
        public void Delay_WithToken_CancelWhilePending_AwaitThrowsOCE()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            bool caught = false;
            async TGTask Run()
            {
                try { await sched.Delay(5f, s.Token); }
                catch (OperationCanceledException) { caught = true; }
            }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.IsFalse(caught, "pending 时尚未取消");

            s.Cancel();
            Assert.IsTrue(caught, "取消 pending Delay → await 抛 OCE");
        }

        [Test]
        public void Yield_WithToken_CancelWhilePending_Cancels()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            bool caught = false;
            async TGTask Run()
            {
                try { await sched.Yield(s.Token); }
                catch (OperationCanceledException) { caught = true; }
            }
            Run().Forget();
            // Yield 排入 NextFrame，取消发生在 ProcessYieldQueue 之前
            s.Cancel();
            Assert.IsTrue(caught, "取消 pending Yield → await 抛 OCE");
        }

        [Test]
        public void WaitForFrames_WithToken_CancelWhilePending_Cancels()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            bool caught = false;
            async TGTask Run()
            {
                try { await sched.WaitForFrames(10, s.Token); }
                catch (OperationCanceledException) { caught = true; }
            }
            Run().Forget();
            sched.Update(1f, 1f);
            s.Cancel();
            Assert.IsTrue(caught, "取消 pending WaitForFrames → await 抛 OCE");
        }

        [Test]
        public void Yield_PhaseToken_CancelWhilePending_Cancels()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            var task = sched.Yield(FramePhase.LateUpdate, s.Token);

            s.Cancel();

            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => sched.LateUpdate(0.016f, 0.016f),
                "取消后再驱动目标 phase 应只清理队列，不应崩溃");
        }

        [Test]
        public void Delay_PhaseTimeModeToken_CancelWhilePending_Cancels()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            var task = sched.Delay(5f, FramePhase.FixedUpdate, TimeMode.Unscaled, s.Token);

            s.Cancel();

            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => sched.FixedUpdate(0.016f, 0.016f),
                "取消后的 full overload Delay entry 应可被目标 phase 清理");
        }

        [Test]
        public void WaitForFrames_PhaseToken_CancelWhilePending_Cancels()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            var task = sched.WaitForFrames(3, FramePhase.EndOfFrame, s.Token);

            s.Cancel();

            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => sched.EndOfFrame(0.016f, 0.016f),
                "取消后的 phase-aware WaitForFrames entry 应可被目标 phase 清理");
        }

        [Test]
        public void CancelSource_CancelsAllDerivedTasks_UnrelatedUnaffected()
        {
            var sched = new TGTaskScheduler();
            var sA = TGCancelSource.Rent();
            var sB = TGCancelSource.Rent();
            bool a1 = false, a2 = false, bCancelled = false;
            async TGTask A1() { try { await sched.Delay(5f, sA.Token); } catch (OperationCanceledException) { a1 = true; } }
            async TGTask A2() { try { await sched.Delay(5f, sA.Token); } catch (OperationCanceledException) { a2 = true; } }
            async TGTask B()  { try { await sched.Delay(5f, sB.Token); } catch (OperationCanceledException) { bCancelled = true; } }
            A1().Forget(); A2().Forget(); B().Forget();
            sched.Update(1f, 1f);

            sA.Cancel();

            Assert.IsTrue(a1 && a2, "sA 的两个派生任务都被取消（1:N）");
            Assert.IsFalse(bCancelled, "绑定 sB 的任务不受影响");
        }

        [Test]
        public void LongLivedSource_AfterManyCompletions_NoRegistrationLeak()
        {
            var sched = new TGTaskScheduler();
            var s = TGCancelSource.Rent();
            for (int i = 0; i < 50; i++)
            {
                sched.Delay(0.5f, s.Token).Forget();
                sched.Update(1f, 1f); // 到期完成 → reg.Dispose 注销
            }
            Assert.AreEqual(0, s.RegistrationCount, "正常完成后长寿命 source 不应累积 registration（D7）");
        }

        [Test]
        public void Delay_NonToken_StillCompletes_NoRegression()
        {
            var sched = new TGTaskScheduler();
            bool done = false;
            async TGTask Run() { await sched.Delay(0.5f); done = true; }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.IsTrue(done, "不带 token 的 Delay 行为不回归");
        }

        // ========== 阶段 2：Abort 句柄 + Forget OCE 静默 + owner-scope（issue 04/05） ==========

        [Test]
        public void Abort_PendingManualTask_AwaitThrowsOCE()
        {
            var sched = new TGTaskScheduler();
            var t = sched.Delay(100f); // Manual task
            bool caught = false;
            async TGTask Run() { try { await t; } catch (OperationCanceledException) { caught = true; } }
            Run().Forget();
            sched.Update(1f, 1f);
            t.Abort();
            Assert.IsTrue(caught, "Abort pending Manual task → await 抛 OCE");
        }

        [Test]
        public void Abort_BuilderTask_Throws()
        {
            var sched = new TGTaskScheduler();
            async TGTask Builder() { await sched.Delay(1f); }
            var bt = Builder();
            Assert.Throws<InvalidOperationException>(() => bt.Abort(), "Builder 构建的 task 不可 Abort");
            bt.Forget();
        }

        [Test]
        public void Abort_CompletedTask_NoOp()
        {
            var t = TGTask.CompletedTask;
            Assert.DoesNotThrow(() => t.Abort(), "已完成 task Abort 为 no-op");
        }

        [Test]
        public void Forget_CanceledTask_NoUnobserved()
        {
            var sched = new TGTaskScheduler();
            Exception unobserved = null;
            Action<Exception> h = ex => unobserved = ex;
            TGTaskScheduler.UnobservedException += h;
            try
            {
                var t = sched.Delay(100f);
                sched.Update(1f, 1f);
                t.Abort();   // 取消（TGTaskAbortException），未 await
                t.Forget();
                Assert.IsNull(unobserved, "取消的 task Forget 不进 UnobservedException");
            }
            finally { TGTaskScheduler.UnobservedException -= h; }
        }

        [Test]
        public void Forget_TokenCanceledTask_NoUnobserved()
        {
            var sched = new TGTaskScheduler();
            var src = TGCancelSource.Rent();
            Exception unobserved = null;
            Action<Exception> h = ex => unobserved = ex;
            TGTaskScheduler.UnobservedException += h;
            try
            {
                var task = sched.Delay(100f, src.Token);
                task.Forget();

                src.Cancel();

                Assert.IsNull(unobserved, "TGCancelToken 触发的预期取消不应进入 UnobservedException");
            }
            finally { TGTaskScheduler.UnobservedException -= h; }
        }

        [Test]
        public void Forget_FaultedTask_StillRaisesUnobserved()
        {
            Exception unobserved = null;
            Action<Exception> h = ex => unobserved = ex;
            TGTaskScheduler.UnobservedException += h;
            try
            {
                var tcs = new TGTaskCompletionSource();
                var t = tcs.Task;
                tcs.SetException(new InvalidOperationException("boom"));
                t.Forget();
                Assert.IsInstanceOf<InvalidOperationException>(unobserved, "真异常 Forget 仍上报 UnobservedException（不过度吞）");
            }
            finally { TGTaskScheduler.UnobservedException -= h; }
        }

        // owner-scope 测试用的 Procedure：OnEnter 发起一个绑定 CancelToken 的 Delay
        private sealed class ScopedProc : ProcedureBase
        {
            private readonly TGTaskScheduler _sched;
            public bool Cancelled;
            public ScopedProc(TGTaskScheduler s) { _sched = s; }
            public override void OnEnter(IProcedureModule m) { Run().Forget(); }
            private async TGTask Run()
            {
                try { await _sched.Delay(100f, CancelToken); }
                catch (OperationCanceledException) { Cancelled = true; }
            }
        }

        [Test]
        public void Procedure_OnExit_CancelsScope()
        {
            var sched = new TGTaskScheduler();
            var proc = new ScopedProc(sched);
            proc.OnEnter(null);
            sched.Update(1f, 1f);
            Assert.IsFalse(proc.Cancelled, "退出前仍 pending");
            proc.OnExit(null); // base.OnExit → CancelScope
            Assert.IsTrue(proc.Cancelled, "Procedure OnExit 自动取消 scope → pending OCE");
        }

        [Test]
        public void Procedure_OnPause_DoesNotCancel()
        {
            var sched = new TGTaskScheduler();
            var proc = new ScopedProc(sched);
            proc.OnEnter(null);
            sched.Update(1f, 1f);
            proc.OnPause(null);
            sched.Update(1f, 1f);
            Assert.IsFalse(proc.Cancelled, "OnPause 不取消（暂停可恢复）");
            proc.OnExit(null); // 清理
        }

        [Test]
        public void ModuleStyle_OwnerSourceCancel_CancelsPending()
        {
            var sched = new TGTaskScheduler();
            var src = TGCancelSource.Rent();
            bool cancelled = false;
            async TGTask Run() { try { await sched.Delay(100f, src.Token); } catch (OperationCanceledException) { cancelled = true; } }
            Run().Forget();
            sched.Update(1f, 1f);
            src.Cancel(); // owner-managed（Module.Shutdown 风格）
            Assert.IsTrue(cancelled, "owner source.Cancel 取消其 pending");
        }

        // ========== 阶段 3：组合子 Timeout/WhenAll/WhenAny（issue 06） ==========

        [Test]
        public void WhenAll_AllComplete_ThenCompletes()
        {
            var sched = new TGTaskScheduler();
            bool done = false;
            async TGTask Run() { await TGTask.WhenAll(sched.Delay(0.5f), sched.Delay(1f), sched.WaitForFrames(2)); done = true; }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.IsFalse(done, "WaitForFrames(2) 未到，WhenAll 未完成");
            sched.Update(1f, 1f);
            Assert.IsTrue(done, "全部完成后 WhenAll 完成");
        }

        [Test]
        public void WhenAll_OneFaults_Aggregates()
        {
            var sched = new TGTaskScheduler();
            bool agg = false;
            async TGTask Faulting() { await sched.Delay(0.5f); throw new InvalidOperationException("boom"); }
            async TGTask Run() { try { await TGTask.WhenAll(sched.Delay(0.5f), Faulting()); } catch (AggregateException) { agg = true; } }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.IsTrue(agg, "含异常子任务 → AggregateException");
        }

        [Test]
        public void WhenAny_FirstWins_ReportsWinner()
        {
            var sched = new TGTaskScheduler();
            int winner = -1;
            async TGTask Run() { winner = await TGTask.WhenAny(sched.Delay(5f), sched.Delay(0.5f), sched.Delay(5f)); }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.AreEqual(1, winner, "WhenAny 返回首个完成的索引");
        }

        [Test]
        public void WhenAny_NullArray_ThrowsArgument()
        {
            Assert.Throws<ArgumentException>(() => TGTask.WhenAny((TGTask[])null));
        }

        [Test]
        public void WhenAny_EmptyArray_ThrowsArgument()
        {
            Assert.Throws<ArgumentException>(() => TGTask.WhenAny());
        }

        [Test]
        public void WhenAll_Empty_CompletesImmediately()
        {
            var t = TGTask.WhenAll();
            Assert.IsTrue(t.IsCompleted, "空 WhenAll 立即完成");
        }

        [Test]
        public void CancelAfter_CancelsAfterDelay()
        {
            var sched = new TGTaskScheduler();
            var src = TGCancelSource.Rent();
            src.CancelAfter(1f, sched);
            bool cancelled = false;
            async TGTask Run() { try { await sched.Delay(100f, src.Token); } catch (OperationCanceledException) { cancelled = true; } }
            Run().Forget();
            sched.Update(0.5f, 0.5f);
            Assert.IsFalse(cancelled, "超时前不取消");
            sched.Update(1f, 1f);
            Assert.IsTrue(cancelled, "CancelAfter 到时取消 pending");
        }

        [Test]
        public void CancelAfter_RecycledBeforeTimeout_DoesNotCancelReusedSource()
        {
            var sched = new TGTaskScheduler();
            var src = TGCancelSource.Rent();

            try
            {
                src.CancelAfter(1f, sched);
                src.Recycle();

                var reused = TGCancelSource.Rent();
                Assert.AreSame(src, reused, "测试依赖 source 池复用同一对象以验证 version 守卫");

                var task = sched.Delay(2f, reused.Token);

                sched.Update(1f, 1f);

                Assert.IsFalse(reused.IsCancellationRequested,
                    "旧世代 CancelAfter 到时后不应误取消已复用的新 source");
                Assert.IsFalse(task.IsCompleted,
                    "绑定新 token 的 pending task 不应被旧世代 timeout 取消");

                sched.Update(1f, 1f);

                Assert.IsTrue(task.IsCompleted);
                Assert.DoesNotThrow(() => task.GetAwaiter().GetResult());

                reused.Recycle();
                src = null;
            }
            finally
            {
                src?.Recycle();
                sched.Shutdown();
            }
        }

        // ========== 边界补充（覆盖缺口） ==========

        [Test]
        public void AbortGeneric_PendingManualTask_AwaitThrowsOCE()
        {
            var tcs = new TGTaskCompletionSource<int>();
            var t = tcs.Task; // TGTask<int> Manual
            bool caught = false;
            async TGTask Run() { try { await t; } catch (OperationCanceledException) { caught = true; } }
            Run().Forget();
            t.Abort();
            Assert.IsTrue(caught, "TGTask<T>.Abort pending → await 抛 OCE");
        }

        [Test]
        public void Delay_WithNoneToken_CompletesNormally()
        {
            var sched = new TGTaskScheduler();
            bool done = false;
            async TGTask Run() { await sched.Delay(0.5f, TGCancelToken.None); done = true; }
            Run().Forget();
            sched.Update(1f, 1f);
            Assert.IsTrue(done, "None token 的 Delay 正常完成（不可取消路径不回归）");
        }

        [Test]
        public void WhenAny_FirstCompletesWithException_Propagates()
        {
            var sched = new TGTaskScheduler();
            bool caught = false;
            async TGTask Faulting() { await sched.Delay(0.5f); throw new InvalidOperationException("boom"); }
            async TGTask Run() { try { await TGTask.WhenAny(Faulting(), sched.Delay(5f)); } catch (InvalidOperationException) { caught = true; } }
            Run().Forget();
            sched.Update(1f, 1f); // Faulting 先完成并抛异常
            Assert.IsTrue(caught, "WhenAny 首个完成抛异常 → 传播该异常");
        }

        [Test]
        public void WhenAny_LosingBranches_AreConsumedWhenTheyComplete()
        {
            TGTaskPool.ClearAll();
            TGTaskCompletionSource.ClearSourcePool();

            var sched = new TGTaskScheduler();
            var first = sched.Delay(0.5f);
            var losing = sched.Delay(1.0f);
            var any = TGTask.WhenAny(first, losing);

            sched.Update(0.5f, 0.5f);
            Assert.AreEqual(0, any.GetAwaiter().GetResult());
            int pooledAfterWinner = TGTaskPool.PooledCount;

            sched.Update(0.5f, 0.5f);

            Assert.Greater(TGTaskPool.PooledCount, pooledAfterWinner,
                "WhenAny 已有胜者后，后完成的 losing branch 仍应被 GetResult 消费并归还 body");
        }

        [Test]
        public void WhenAll_WithCanceledTask_AggregatesOCE()
        {
            var sched = new TGTaskScheduler();
            var src = TGCancelSource.Rent();
            bool agg = false;
            async TGTask Run() { try { await TGTask.WhenAll(sched.Delay(0.5f), sched.Delay(100f, src.Token)); } catch (AggregateException) { agg = true; } }
            Run().Forget();
            sched.Update(1f, 1f); // 第一个完成
            src.Cancel();         // 第二个被取消
            Assert.IsTrue(agg, "WhenAll 含被取消任务 → AggregateException（含 OCE）");
        }
    }
}
