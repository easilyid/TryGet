using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// TimerModule Catch-up Policy 测试。
    /// 验证周期 timer 在长帧时的补偿行为。
    /// </summary>
    [TestFixture]
    public class TimerModuleCatchUpTests
    {
        /// <summary>
        /// Tracer bullet: 默认行为（maxCatchUp = 0）保持向后兼容。
        /// 长帧只触发一次，不补偿丢失的触发。
        /// </summary>
        [Test]
        public void ScheduleRepeat_DefaultMaxCatchUpZero_DoesNotCompensate()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++, maxCatchUp: 0);

            // 长帧：deltaTime = 5f，理论丢失 5 次触发
            t.Update(5f, 5f);

            Assert.AreEqual(1, hits, "maxCatchUp=0 时只触发一次（当前行为）");
            Assert.AreEqual(1, t.PendingCount, "周期 timer 继续存在");
        }

        /// <summary>
        /// 单次补偿：maxCatchUp = 1，长帧补偿 1 次后停止。
        /// </summary>
        [Test]
        public void ScheduleRepeat_MaxCatchUpOne_CompensatesOnce()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++, maxCatchUp: 1);

            // 长帧：deltaTime = 3.5f，理论丢失 3 次触发
            t.Update(3.5f, 3.5f);

            Assert.AreEqual(2, hits, "maxCatchUp=1 时触发 2 次（主触发 + 1 次补偿）");
            Assert.AreEqual(1, t.PendingCount);
        }

        /// <summary>
        /// 完全补偿：maxCatchUp = int.MaxValue，补偿所有丢失触发。
        /// </summary>
        [Test]
        public void ScheduleRepeat_MaxCatchUpUnlimited_CompensatesAll()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++, maxCatchUp: int.MaxValue);

            // 长帧：deltaTime = 5.2f
            // RemainingSeconds = 1 - 5.2 = -4.2
            // 主触发后：-4.2 + 1 = -3.2
            // 补偿循环：-3.2→-2.2→-1.2→-0.2→0.8 (4 次补偿)
            t.Update(5.2f, 5.2f);

            Assert.AreEqual(5, hits, "maxCatchUp=MaxValue 时完全补偿（主触发 1 次 + 4 次补偿）");
            Assert.AreEqual(1, t.PendingCount);
        }

        /// <summary>
        /// 边界测试：maxCatchUp = 2，只补偿 2 次。
        /// </summary>
        [Test]
        public void ScheduleRepeat_MaxCatchUpTwo_StopsAt2()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () => hits++, maxCatchUp: 2);

            // 长帧：deltaTime = 6f，理论 6 次触发
            t.Update(6f, 6f);

            Assert.AreEqual(3, hits, "maxCatchUp=2 时触发 3 次（主触发 + 2 次补偿）");
            Assert.AreEqual(1, t.PendingCount);
        }

        /// <summary>
        /// 异常隔离：补偿期间 callback 抛异常，应聚合所有异常。
        /// </summary>
        [Test]
        public void ScheduleRepeat_CatchUpWithExceptions_AggregatesAll()
        {
            var t = new TimerModule();
            int hits = 0;
            t.ScheduleRepeat(1f, () =>
            {
                hits++;
                throw new InvalidOperationException($"Error #{hits}");
            }, maxCatchUp: 2);

            // 长帧：触发 3 次，每次都抛异常
            var ex = Assert.Throws<AggregateException>(() => t.Update(3f, 3f));

            Assert.AreEqual(3, ex.InnerExceptions.Count, "应聚合 3 个异常");
            Assert.AreEqual(3, hits, "即使有异常，所有触发都应完成");
        }

        /// <summary>
        /// 负值验证：maxCatchUp < 0 应抛异常。
        /// </summary>
        [Test]
        public void ScheduleRepeat_NegativeMaxCatchUp_Throws()
        {
            var t = new TimerModule();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                t.ScheduleRepeat(1f, () => { }, maxCatchUp: -1));
        }
    }
}
