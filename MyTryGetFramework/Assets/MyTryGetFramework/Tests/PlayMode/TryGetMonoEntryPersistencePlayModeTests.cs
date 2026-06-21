using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    [TestFixture]
    public sealed class TryGetMonoEntryPersistencePlayModeTests
    {
        [UnityTest]
        public IEnumerator TryGetMonoEntry_DefaultsToDontDestroyOnLoad()
        {
            var gameObject = new GameObject("TryGetMonoEntry Persistent Test");
            var originalScene = gameObject.scene;
            var destroyRequested = false;

            try
            {
                var entry = gameObject.AddComponent<PersistentTryGetMonoEntry>();

                yield return null;

                Assert.That(entry.HasHost, Is.True);
                Assert.That(entry.Module.InitCount, Is.EqualTo(1));
                Assert.That(gameObject.scene.handle, Is.Not.EqualTo(originalScene.handle));
                Assert.That(gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                UnityEngine.Object.Destroy(gameObject);
                destroyRequested = true;
                yield return null;

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
