using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// TimerModule V0.5 增强（ScheduleRepeat / Pause / Resume）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class TimerModuleV05Tests
    {
        // —— ScheduleRepeat ——

        [Test]
        public void ScheduleRepeat_TriggersEveryInterval()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++);

            Assert.AreEqual(0, hits);
            t.Update(1f, 1f); // first trigger
            Assert.AreEqual(1, hits);

            t.Update(1f, 1f); // second
            Assert.AreEqual(2, hits);

            t.Update(1f, 1f); // third
            Assert.AreEqual(3, hits);

            Assert.AreEqual(1, t.PendingCount, "周期 timer 不删除，PendingCount 始终为 1");
        }

        [Test]
        public void ScheduleRepeat_FirstFireIsAfterInterval()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(0.5f, () => hits++);

            t.Update(0.4f, 0.4f); // 还未到 0.5
            Assert.AreEqual(0, hits);

            t.Update(0.2f, 0.2f); // 累计 0.6，超过 0.5
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void ScheduleRepeat_CancelStopsAllFutureFires()
        {
            var t = new TimerModule();
            int hits = 0;
            var handle = t.ScheduleRepeat(1f, () => hits++);

            t.Update(1f, 1f);
            Assert.AreEqual(1, hits);

            Assert.IsTrue(t.Cancel(handle));

            t.Update(1f, 1f);
            t.Update(1f, 1f);
            Assert.AreEqual(1, hits, "Cancel 后周期 timer 不再触发");
            Assert.AreEqual(0, t.PendingCount);
        }

        [Test]
        public void ScheduleRepeat_ZeroOrNegativeInterval_Throws()
        {
            var t = new TimerModule();
            Assert.Throws<ArgumentOutOfRangeException>(() => t.ScheduleRepeat(0f, () => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => t.ScheduleRepeat(-1f, () => { }));
        }

        [Test]
        public void ScheduleRepeat_NullCallback_Throws()
        {
            var t = new TimerModule();
            Assert.Throws<ArgumentNullException>(() => t.ScheduleRepeat(1f, null));
        }

        [Test]
        public void ScheduleRepeat_LargeDeltaCatchesOnlyOneTrigger()
        {
            // 边界：单帧 deltaTime 远超 interval（比如 5 秒推进 interval=1）
            // 当前实现：每次 Update 只触发一次（不补帧）。业务知晓即可。
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++);

            t.Update(5f, 5f);
            Assert.AreEqual(1, hits, "单帧大 delta 只触发一次（不补帧）");
            Assert.AreEqual(1, t.PendingCount);
        }

        // —— Pause / Resume ——

        [Test]
        public void Pause_StopsCountdown()
        {
            var t = new TimerModule();
            bool fired = false;
            var h = t.Schedule(1f, () => fired = true);

            t.Update(0.5f, 0.5f);
            Assert.IsTrue(t.Pause(h));
            Assert.IsTrue(t.IsPaused(h));

            // 暂停期间推进任意时间都不触发
            t.Update(10f, 10f);
            Assert.IsFalse(fired);
            Assert.AreEqual(1, t.PendingCount);
        }

        [Test]
        public void Resume_RestartsCountdownFromWhereItPaused()
        {
            var t = new TimerModule();
            bool fired = false;
            var h = t.Schedule(1f, () => fired = true);

            t.Update(0.6f, 0.6f); // 剩 0.4
            t.Pause(h);
            t.Update(5f, 5f);     // 暂停期间不动
            Assert.IsFalse(fired);

            t.Resume(h);
            Assert.IsFalse(t.IsPaused(h));

            t.Update(0.3f, 0.3f); // 累计推进 0.3
            Assert.IsFalse(fired);

            t.Update(0.2f, 0.2f); // 累计推进 0.5，剩 -0.1，触发
            Assert.IsTrue(fired);
        }

        [Test]
        public void Pause_AlreadyPaused_ReturnsFalse()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });

            Assert.IsTrue(t.Pause(h));
            Assert.IsFalse(t.Pause(h), "重复 Pause 应返回 false");
        }

        [Test]
        public void Resume_NotPaused_ReturnsFalse()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });

            Assert.IsFalse(t.Resume(h), "未暂停时 Resume 返回 false");
        }

        [Test]
        public void Pause_InvalidHandle_ReturnsFalse()
        {
            var t = new TimerModule();
            Assert.IsFalse(t.Pause(default(TimerHandle)));
            Assert.IsFalse(t.Resume(default(TimerHandle)));
            Assert.IsFalse(t.IsPaused(default(TimerHandle)));
        }

        [Test]
        public void Pause_CancelledTimer_ReturnsFalse()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });
            t.Cancel(h);

            Assert.IsFalse(t.Pause(h), "已 Cancel 的 timer 不可 Pause");
        }

        [Test]
        public void Pause_NonExistentHandle_ReturnsFalse()
        {
            var t = new TimerModule();
            // 用未注册的 handle id
            var ghost = new TimerHandle(99999);
            Assert.IsFalse(t.Pause(ghost));
        }

        [Test]
        public void Pause_OnRepeatTimer_FreezesAllFutureFires()
        {
            var t = new TimerModule();
            int hits = 0;
            var h = t.ScheduleRepeat(1f, () => hits++);

            t.Update(1f, 1f);
            Assert.AreEqual(1, hits);

            t.Pause(h);
            t.Update(10f, 10f);
            Assert.AreEqual(1, hits, "周期 timer 暂停后不再触发");

            t.Resume(h);
            t.Update(1f, 1f);
            Assert.AreEqual(2, hits);
        }

        [Test]
        public void IsPaused_RegularState_False()
        {
            var t = new TimerModule();
            var h = t.Schedule(1f, () => { });
            Assert.IsFalse(t.IsPaused(h));
        }

        // —— 组合：多 timer 共存（暂停一个不影响其他）——

        [Test]
        public void Pause_OneTimer_OthersStillTick()
        {
            var t = new TimerModule();
            bool fired1 = false, fired2 = false;
            var h1 = t.Schedule(1f, () => fired1 = true);
            var h2 = t.Schedule(1f, () => fired2 = true);

            t.Pause(h1);
            t.Update(1f, 1f);

            Assert.IsFalse(fired1, "h1 暂停未触发");
            Assert.IsTrue(fired2, "h2 正常触发");
        }
    }
}
