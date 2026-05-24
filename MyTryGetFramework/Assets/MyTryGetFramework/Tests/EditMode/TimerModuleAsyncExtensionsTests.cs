using System;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.6 Iter 6 — ITimerModule 的 WaitAsync 扩展测试。
    /// </summary>
    [TestFixture]
    public class TimerModuleAsyncExtensionsTests
    {
        [Test]
        public void WaitAsync_OneSecond_CompletesAfterAccumulatedDt()
        {
            var host = new ModuleHost();
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            host.Initialize();

            var task = timer.WaitAsync(1.0f);
            Assert.IsFalse(task.IsCompleted);

            host.Update(0.5f, 0.5f);
            Assert.IsFalse(task.IsCompleted);

            host.Update(0.5f, 0.5f);
            Assert.IsTrue(task.IsCompleted);

            host.Shutdown();
        }

        [Test]
        public void WaitAsync_NegativeSeconds_Throws()
        {
            var timer = new TimerModule();
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.WaitAsync(-0.5f));
        }

        [Test]
        public void WaitAsync_NullTimer_Throws()
        {
            ITimerModule timer = null;
            Assert.Throws<ArgumentNullException>(() => timer.WaitAsync(1f));
        }

        [Test]
        public void WaitUnscaledAsync_Works()
        {
            var host = new ModuleHost();
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            host.Initialize();

            var task = timer.WaitUnscaledAsync(0.5f);
            host.Update(0.5f, 0.5f);
            Assert.IsTrue(task.IsCompleted);

            host.Shutdown();
        }

        [Test]
        public void AsyncTGTask_AwaitingTimer_ResumesAfterTimer()
        {
            var host = new ModuleHost();
            var timer = new TimerModule();
            host.Register<ITimerModule>(timer);
            host.Initialize();

            bool reached = false;
            var outer = AsyncBody(timer, () => reached = true);

            Assert.IsFalse(reached);
            host.Update(0.5f, 0.5f);
            Assert.IsFalse(reached);
            host.Update(0.5f, 0.5f);
            Assert.IsTrue(reached);
            Assert.IsTrue(outer.IsCompleted);

            host.Shutdown();
        }

        private static async TGTask AsyncBody(ITimerModule timer, Action onReached)
        {
            await timer.WaitAsync(1.0f);
            onReached();
        }
    }
}
