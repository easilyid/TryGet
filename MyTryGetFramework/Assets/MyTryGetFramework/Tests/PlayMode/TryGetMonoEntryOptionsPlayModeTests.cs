using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryOptionsPlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_UsesCustomGameLauncherOptions()
        {
            var gameObject = new GameObject("TryGetMonoEntry Options Test");
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<OptionsTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.SetupCalled, Is.True);
                Assert.That(entry.LoggerFromHost, Is.SameAs(entry.Logger));
                Assert.That(entry.ClockFromHost, Is.SameAs(entry.Clock));
                Assert.That(entry.SchedulerFromHost, Is.SameAs(entry.Scheduler));
                Assert.That(entry.Logger.MinimumLevel, Is.EqualTo(LogLevel.Warn));
                Assert.That(entry.Logger.InitCount, Is.EqualTo(1));
                Assert.That(entry.Clock.InitCount, Is.EqualTo(1));
                Assert.That(entry.Scheduler.InitCount, Is.EqualTo(1));
                Assert.That(entry.Module.InitCount, Is.EqualTo(1));

                for (int i = 0; i < 10 && entry.Scheduler.UpdateCount == 0; i++)
                    yield return null;

                Assert.That(entry.Scheduler.UpdateCount, Is.GreaterThanOrEqualTo(1));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

                Assert.That(entry.Logger.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.Clock.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.Scheduler.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.Module.ShutdownCount, Is.EqualTo(1));
                Assert.That(entry.HasHost, Is.False);
            }
            finally
            {
                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
