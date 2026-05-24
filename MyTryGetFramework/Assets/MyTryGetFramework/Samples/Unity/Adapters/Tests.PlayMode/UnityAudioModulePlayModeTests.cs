using System.Collections;
using NUnit.Framework;
using TryGet;
using TryGet.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TryGet.Tests.PlayMode
{
    /// <summary>
    /// UnityAudioModule（IAudioModule Unity Adapter）的 PlayMode 测试。
    ///
    /// 用 PlayMode 而非 EditMode 因 Adapter 创建 GameObject + AudioSource，
    /// 需要 Unity 运行时环境。
    /// </summary>
    [TestFixture]
    public class UnityAudioModulePlayModeTests
    {
        private AudioClip MakeTestClip(float seconds = 1f)
        {
            // 生成 1 秒静音 AudioClip 用于测试
            int sampleRate = 44100;
            int sampleCount = (int)(seconds * sampleRate);
            var clip = AudioClip.Create("TestClip", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            // 静音（全 0）
            clip.SetData(data, 0);
            return clip;
        }

        [Test]
        public void Priority_SameAsMemoryImpl()
        {
            var a = new UnityAudioModule();
            Assert.AreEqual(-380, a.Priority);
        }

        [Test]
        public void Initialize_CreatesPoolGameObjects()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            var root = GameObject.Find("[UnityAudioModule]");
            Assert.NotNull(root, "Initialize 应创建 root GameObject");
            Assert.AreEqual(4, root.transform.childCount, "应创建 poolSize 个 AudioSource 子物体");

            host.Shutdown();
            Assert.IsNull(GameObject.Find("[UnityAudioModule]"), "Shutdown 应销毁 root");
        }

        [UnityTest]
        public IEnumerator PlayThenIsPlaying_True()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("test", MakeTestClip());
            host.Get<IAudioModule>().Play("test", AudioCategory.SFX);

            // 让 Unity 走一帧让 AudioSource 真正开始播放
            yield return null;

            Assert.IsTrue(host.Get<IAudioModule>().IsPlaying("test"));
            Assert.AreEqual(1, host.Get<IAudioModule>().PlayingCount);

            host.Shutdown();
        }

        [Test]
        public void Play_UnregisteredCue_Throws()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            Assert.Throws<System.InvalidOperationException>(
                () => host.Get<IAudioModule>().Play("missing"));

            host.Shutdown();
        }

        [Test]
        public void Stop_RemovesFromPlaying()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("test", MakeTestClip());
            host.Get<IAudioModule>().Play("test");
            host.Get<IAudioModule>().Stop("test");

            Assert.IsFalse(host.Get<IAudioModule>().IsPlaying("test"));

            host.Shutdown();
        }

        [Test]
        public void PauseResume_TogglesIsPaused()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("test", MakeTestClip());
            var audio = host.Get<IAudioModule>();
            audio.Play("test");

            Assert.IsTrue(audio.Pause("test"));
            Assert.IsTrue(audio.IsPaused("test"));
            Assert.IsTrue(audio.IsPlaying("test"), "Pause 后仍 IsPlaying（与 Memory 实现契约一致）");

            Assert.IsTrue(audio.Resume("test"));
            Assert.IsFalse(audio.IsPaused("test"));

            host.Shutdown();
        }

        [Test]
        public void PauseAll_Category_OnlyPausesThatCategory()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("bgm", MakeTestClip());
            ua.RegisterClip("sfx", MakeTestClip());
            var audio = host.Get<IAudioModule>();
            audio.Play("bgm", AudioCategory.BGM);
            audio.Play("sfx", AudioCategory.SFX);

            audio.PauseAll(AudioCategory.BGM);

            Assert.IsTrue(audio.IsPaused("bgm"));
            Assert.IsFalse(audio.IsPaused("sfx"));

            host.Shutdown();
        }

        [Test]
        public void Volume_MasterAndCategoryCombined()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("test", MakeTestClip());
            var audio = host.Get<IAudioModule>();
            audio.Play("test", AudioCategory.BGM);
            audio.SetMasterVolume(0.5f);
            audio.SetCategoryVolume(AudioCategory.BGM, 0.6f);

            // 找到该 cue 对应的 AudioSource 验证 effective volume = 0.5 * 0.6 = 0.3
            var root = GameObject.Find("[UnityAudioModule]");
            float? observed = null;
            foreach (Transform child in root.transform)
            {
                var src = child.GetComponent<AudioSource>();
                if (src.isPlaying || src.clip != null)
                {
                    observed = src.volume;
                    break;
                }
            }
            Assert.IsTrue(observed.HasValue);
            Assert.AreEqual(0.3f, observed.Value, 0.001f);

            host.Shutdown();
        }

        [Test]
        public void PoolOverflow_RecyclesOldestCue()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 2);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            for (int i = 0; i < 3; i++)
                ua.RegisterClip($"cue{i}", MakeTestClip());

            var audio = host.Get<IAudioModule>();
            audio.Play("cue0");
            audio.Play("cue1");

            Assert.AreEqual(2, audio.PlayingCount);

            // 第三个：池满，应复用最旧（cue0）
            audio.Play("cue2");

            Assert.AreEqual(2, audio.PlayingCount);
            Assert.IsFalse(audio.IsPlaying("cue0"), "cue0 应被溢出策略停掉");
            Assert.IsTrue(audio.IsPlaying("cue1"));
            Assert.IsTrue(audio.IsPlaying("cue2"));

            host.Shutdown();
        }

        [Test]
        public void RegisterClip_NullOrInvalid_Throws()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            Assert.Throws<System.ArgumentException>(() => ua.RegisterClip(null, MakeTestClip()));
            Assert.Throws<System.ArgumentException>(() => ua.RegisterClip("", MakeTestClip()));
            Assert.Throws<System.ArgumentNullException>(() => ua.RegisterClip("x", null));

            host.Shutdown();
        }

        [Test]
        public void UnregisterClip_PlayingCue_StopsThenRemoves()
        {
            var host = new ModuleHost();
            var ua = new UnityAudioModule(poolSize: 4);
            host.Register<IAudioModule>(ua);
            host.Initialize();

            ua.RegisterClip("test", MakeTestClip());
            host.Get<IAudioModule>().Play("test");

            Assert.IsTrue(ua.UnregisterClip("test"));
            Assert.IsFalse(host.Get<IAudioModule>().IsPlaying("test"));
            // 再 Play 应抛
            Assert.Throws<System.InvalidOperationException>(
                () => host.Get<IAudioModule>().Play("test"));

            host.Shutdown();
        }
    }
}
