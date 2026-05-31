using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// TimerModule（ITimerModule + IUpdateModule）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class TimerModuleTests
    {
        [Test]
        public void Priority_IsHalfTrack_BelowOrdinaryUserModules()
        {
            var t = new TimerModule();
            Assert.AreEqual(-500, t.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var t = new TimerModule();
            Assert.AreEqual(0, t.DependsOn.Count);
        }

        [Test]
        public void Schedule_ReturnsValidHandle()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });
            Assert.IsTrue(h.IsValid);
            Assert.AreEqual(1, t.PendingCount);
        }

        [Test]
        public void Schedule_NullCallback_Throws()
        {
            var t = new TimerModule();
            Assert.Throws<ArgumentNullException>(() => t.Schedule(1f, null));
        }

        [Test]
        public void Schedule_NegativeSeconds_Throws()
        {
            var t = new TimerModule();
            Assert.Throws<ArgumentOutOfRangeException>(() => t.Schedule(-0.1f, () => { }));
        }

        [Test]
        public void Update_TriggersCallback_AfterAccumulatedDelta()
        {
            var t = new TimerModule();
            int fired = 0;
            t.Schedule(1f, () => fired++);

            t.Update(0.4f, 0.4f);
            Assert.AreEqual(0, fired, "未到时不触发");

            t.Update(0.4f, 0.4f);
            Assert.AreEqual(0, fired, "仍未到时");

            t.Update(0.3f, 0.3f);
            Assert.AreEqual(1, fired, "累计超过 1s 触发");
            Assert.AreEqual(0, t.PendingCount, "触发后从列表移除");
        }

        [Test]
        public void Update_DoesNotRefire_AfterTrigger()
        {
            var t = new TimerModule();
            int fired = 0;
            t.Schedule(0.5f, () => fired++);

            t.Update(1f, 1f);
            t.Update(1f, 1f);
            t.Update(1f, 1f);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Schedule_ZeroSeconds_FiresOnNextUpdate()
        {
            var t = new TimerModule();
            int fired = 0;
            t.Schedule(0f, () => fired++);

            t.Update(0f, 0f);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Schedule_UsesScaledDelta()
        {
            var t = new TimerModule();
            int fired = 0;
            t.Schedule(1f, () => fired++);

            // scaled=0 unscaled=2：scaled 路径不该推进
            t.Update(0f, 2f);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void ScheduleUnscaled_UsesUnscaledDelta()
        {
            var t = new TimerModule();
            int fired = 0;
            t.ScheduleUnscaled(1f, () => fired++);

            // scaled=2 unscaled=0：unscaled 路径不该推进
            t.Update(2f, 0f);
            Assert.AreEqual(0, fired);

            t.Update(0f, 1f);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Cancel_NotYetFiredTimer_ReturnsTrue()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });
            Assert.IsTrue(t.Cancel(h));
        }

        [Test]
        public void Cancel_AlreadyFiredTimer_ReturnsFalse()
        {
            var t = new TimerModule();
            var h = t.Schedule(0.1f, () => { });
            t.Update(1f, 1f);

            Assert.IsFalse(t.Cancel(h));
        }

        [Test]
        public void Cancel_AlreadyCancelledTimer_ReturnsFalse()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });
            t.Cancel(h);

            Assert.IsFalse(t.Cancel(h));
        }

        [Test]
        public void Cancel_InvalidHandle_ReturnsFalse()
        {
            var t = new TimerModule();
            Assert.IsFalse(t.Cancel(TimerHandle.Invalid));
        }

        [Test]
        public void Cancel_PreventsCallbackFromFiring()
        {
            var t = new TimerModule();
            int fired = 0;
            var h = t.Schedule(1f, () => fired++);
            t.Cancel(h);

            t.Update(2f, 2f);

            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, t.PendingCount);
        }

        [Test]
        public void PendingCount_TracksScheduleAndCancel()
        {
            var t = new TimerModule();
            var h1 = t.Schedule(1f, () => { });
            var h2 = t.Schedule(1f, () => { });
            var h3 = t.Schedule(1f, () => { });

            Assert.AreEqual(3, t.PendingCount);

            t.Cancel(h2);
            Assert.AreEqual(2, t.PendingCount);
        }

        [Test]
        public void Shutdown_ClearsAllPendingTimers()
        {
            var t = new TimerModule();
            int fired = 0;
            t.Schedule(1f, () => fired++);
            t.Schedule(1f, () => fired++);

            t.Shutdown();

            Assert.AreEqual(0, t.PendingCount);

            // Shutdown 后再 Update 不应触发
            t.Update(5f, 5f);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void MultipleTimers_FireIndependently()
        {
            var t = new TimerModule();
            int firedA = 0, firedB = 0;
            t.Schedule(1f, () => firedA++);
            t.Schedule(2f, () => firedB++);

            t.Update(1f, 1f);
            Assert.AreEqual(1, firedA);
            Assert.AreEqual(0, firedB);

            t.Update(1f, 1f);
            Assert.AreEqual(1, firedA);
            Assert.AreEqual(1, firedB);
        }

        [Test]
        public void IntegratesWithModuleSystem_UpdateDrivesTimers()
        {
            var host = new ModuleSystem();
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            host.Initialize();

            int fired = 0;
            timer.Schedule(0.5f, () => fired++);

            host.Update(1f, 1f);
            Assert.AreEqual(1, fired);
        }

        // —— 来自 Stage-2 review 必改：callback 内 Schedule/Cancel + 异常聚合 ——

        [Test]
        public void CallbackException_DoesNotPreventOtherTimersInSameFrame()
        {
            var t = new TimerModule();
            int firedA = 0, firedC = 0;
            t.Schedule(0.5f, () => firedA++);
            t.Schedule(0.5f, () => throw new InvalidOperationException("B boom"));
            t.Schedule(0.5f, () => firedC++);

            var ex = Assert.Throws<AggregateException>(() => t.Update(1f, 1f));
            Assert.AreEqual(1, ex.InnerExceptions.Count);
            Assert.AreEqual(1, firedA, "A 应在 B 抛出前/后被触发");
            Assert.AreEqual(1, firedC, "C 不应因 B 抛出而被跳过（异常聚合）");
            Assert.AreEqual(0, t.PendingCount, "本帧到期的 3 个 timer 都应被移除");
        }

        [Test]
        public void MultipleCallbackExceptions_AreAggregated()
        {
            var t = new TimerModule();
            t.Schedule(0.5f, () => throw new InvalidOperationException("e1"));
            t.Schedule(0.5f, () => throw new InvalidOperationException("e2"));

            var ex = Assert.Throws<AggregateException>(() => t.Update(1f, 1f));
            Assert.AreEqual(2, ex.InnerExceptions.Count);
        }

        [Test]
        public void Callback_ScheduleNewTimer_NewTimerNotFiredThisFrame()
        {
            var t = new TimerModule();
            int outer = 0, inner = 0;
            t.Schedule(0.5f, () =>
            {
                outer++;
                t.Schedule(0.5f, () => inner++);
            });

            t.Update(1f, 1f);

            Assert.AreEqual(1, outer);
            Assert.AreEqual(0, inner, "新 timer 当帧不应触发");
            Assert.AreEqual(1, t.PendingCount, "新 timer 应在 pending 列表");

            t.Update(1f, 1f);
            Assert.AreEqual(1, inner, "新 timer 下一帧触发");
        }

        [Test]
        public void Callback_CancelAnotherPendingTimer_TargetGetsCancelled()
        {
            var t = new TimerModule();
            int firedA = 0, firedC = 0;
            TimerHandle handleC = default;

            // A 到期时取消 C（C 还没到期）
            t.Schedule(0.5f, () => { firedA++; t.Cancel(handleC); });
            handleC = t.Schedule(1.5f, () => firedC++);

            // 第一帧推进 1f：A 到期触发并 Cancel C
            t.Update(1f, 1f);
            Assert.AreEqual(1, firedA);
            // C 已被 Cancel：下次 Update 应跳过它
            t.Update(2f, 2f);
            Assert.AreEqual(0, firedC);
        }

        [Test]
        public void SameFrame_MultipleTimers_AllFireOnce()
        {
            var t = new TimerModule();
            int countA = 0, countB = 0, countC = 0;
            t.Schedule(0.5f, () => countA++);
            t.Schedule(0.5f, () => countB++);
            t.Schedule(0.5f, () => countC++);

            t.Update(1f, 1f);

            Assert.AreEqual(1, countA);
            Assert.AreEqual(1, countB);
            Assert.AreEqual(1, countC);
            Assert.AreEqual(0, t.PendingCount);
        }
    }
}
