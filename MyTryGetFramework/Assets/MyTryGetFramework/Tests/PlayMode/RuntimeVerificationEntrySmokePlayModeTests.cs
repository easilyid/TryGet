using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class RuntimeVerificationEntrySmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeVerificationEntry_CanRunAsUnitySampleEntry()
        {
            var gameObject = new GameObject("RuntimeVerificationEntry Smoke Test");
            var destroyRequested = false;
            var failureLogCount = 0;
            var completed = false;

            void CaptureSampleLog(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Log)
                    return;

                if (condition.Contains("失败"))
                    failureLogCount++;
                if (condition.Contains("运行时验证完成"))
                    completed = true;
            }

            try
            {
                Application.logMessageReceived += CaptureSampleLog;
                var entry = gameObject.AddComponent<RuntimeVerificationEntry>();

                yield return new WaitForSeconds(1.25f);

                Assert.That(entry != null, Is.True);
                Assert.That(entry.isActiveAndEnabled, Is.True);
                Assert.That(completed, Is.True);
                Assert.That(failureLogCount, Is.EqualTo(0));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;
            }
            finally
            {
                Application.logMessageReceived -= CaptureSampleLog;

                if (!destroyRequested && gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
