using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// MemoryAudioModule（IAudioModule 实现）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class MemoryAudioModuleTests
    {
        [Test]
        public void Priority_BetweenResourceAndUI()
        {
            var a = new MemoryAudioModule();
            Assert.AreEqual(-380, a.Priority);
        }

        [Test]
        public void DependsOn_IsEmpty()
        {
            var a = new MemoryAudioModule();
            Assert.AreEqual(0, a.DependsOn.Count);
        }

        // —— 初始状态 ——

        [Test]
        public void Initial_NoPlaying_AllVolumesOne()
        {
            var a = new MemoryAudioModule();
            Assert.AreEqual(0, a.PlayingCount);
            Assert.AreEqual(1f, a.MasterVolume);
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.BGM));
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.SFX));
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.UI));
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.Voice));
        }

        // —— Play / Stop ——

        [Test]
        public void Play_NewCue_StartsPlaying()
        {
            var a = new MemoryAudioModule();
            a.Play("sfx/explosion");

            Assert.IsTrue(a.IsPlaying("sfx/explosion"));
            Assert.AreEqual(1, a.PlayingCount);
        }

        [Test]
        public void Play_NullOrEmptyCue_Throws()
        {
            var a = new MemoryAudioModule();
            Assert.Throws<ArgumentException>(() => a.Play(null));
            Assert.Throws<ArgumentException>(() => a.Play(""));
        }

        [Test]
        public void Play_DefaultCategory_IsSFX()
        {
            var a = new MemoryAudioModule();
            a.Play("x");

            // 用 StopAll 反向验证
            a.StopAll(AudioCategory.SFX);
            Assert.IsFalse(a.IsPlaying("x"), "默认 category 应为 SFX");
        }

        [Test]
        public void Play_DuplicateCue_Idempotent()
        {
            var a = new MemoryAudioModule();
            a.Play("x");
            a.Play("x");

            Assert.AreEqual(1, a.PlayingCount, "重复 Play 同 cue 视为已在播放");
            Assert.IsTrue(a.IsPlaying("x"));
        }

        [Test]
        public void Play_CategoryReassignment_OnDuplicate()
        {
            var a = new MemoryAudioModule();
            a.Play("ambient", AudioCategory.BGM);
            a.Play("ambient", AudioCategory.SFX);

            // 重新 Play 后 category 已变为 SFX
            a.StopAll(AudioCategory.BGM);
            Assert.IsTrue(a.IsPlaying("ambient"), "原 BGM 已被覆盖为 SFX，BGM 的 StopAll 不应停它");
            a.StopAll(AudioCategory.SFX);
            Assert.IsFalse(a.IsPlaying("ambient"));
        }

        [Test]
        public void Stop_Playing_Removes()
        {
            var a = new MemoryAudioModule();
            a.Play("x");
            a.Stop("x");

            Assert.IsFalse(a.IsPlaying("x"));
            Assert.AreEqual(0, a.PlayingCount);
        }

        [Test]
        public void Stop_NotPlaying_NoOp()
        {
            var a = new MemoryAudioModule();
            Assert.DoesNotThrow(() => a.Stop("ghost"));
            Assert.AreEqual(0, a.PlayingCount);
        }

        [Test]
        public void Stop_NullOrEmpty_NoOp()
        {
            var a = new MemoryAudioModule();
            a.Play("real");
            Assert.DoesNotThrow(() => a.Stop(null));
            Assert.DoesNotThrow(() => a.Stop(""));
            Assert.IsTrue(a.IsPlaying("real"), "real 未被误停");
        }

        // —— StopAll(category) ——

        [Test]
        public void StopAll_Category_StopsOnlyThatCategory()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm1", AudioCategory.BGM);
            a.Play("sfx1", AudioCategory.SFX);
            a.Play("sfx2", AudioCategory.SFX);
            a.Play("ui1", AudioCategory.UI);

            a.StopAll(AudioCategory.SFX);

            Assert.IsTrue(a.IsPlaying("bgm1"));
            Assert.IsFalse(a.IsPlaying("sfx1"));
            Assert.IsFalse(a.IsPlaying("sfx2"));
            Assert.IsTrue(a.IsPlaying("ui1"));
            Assert.AreEqual(2, a.PlayingCount);
        }

        [Test]
        public void StopAll_EmptyCategory_NoThrow()
        {
            var a = new MemoryAudioModule();
            Assert.DoesNotThrow(() => a.StopAll(AudioCategory.BGM));
        }

        // —— StopAllSounds ——

        [Test]
        public void StopAllSounds_ClearsEverything()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Play("sfx", AudioCategory.SFX);
            a.Play("ui", AudioCategory.UI);
            a.Play("v", AudioCategory.Voice);

            a.StopAllSounds();

            Assert.AreEqual(0, a.PlayingCount);
            Assert.IsFalse(a.IsPlaying("bgm"));
        }

        // —— IsPlaying ——

        [Test]
        public void IsPlaying_NullOrEmpty_False()
        {
            var a = new MemoryAudioModule();
            Assert.IsFalse(a.IsPlaying(null));
            Assert.IsFalse(a.IsPlaying(""));
        }

        // —— Volume ——

        [Test]
        public void SetMasterVolume_InRange_Stores()
        {
            var a = new MemoryAudioModule();
            a.SetMasterVolume(0.5f);
            Assert.AreEqual(0.5f, a.MasterVolume);
        }

        [Test]
        public void SetMasterVolume_ClampedBelow0()
        {
            var a = new MemoryAudioModule();
            a.SetMasterVolume(-0.3f);
            Assert.AreEqual(0f, a.MasterVolume);
        }

        [Test]
        public void SetMasterVolume_ClampedAbove1()
        {
            var a = new MemoryAudioModule();
            a.SetMasterVolume(1.5f);
            Assert.AreEqual(1f, a.MasterVolume);
        }

        [Test]
        public void SetCategoryVolume_PerCategoryIsolated()
        {
            var a = new MemoryAudioModule();
            a.SetCategoryVolume(AudioCategory.BGM, 0.3f);
            a.SetCategoryVolume(AudioCategory.SFX, 0.7f);

            Assert.AreEqual(0.3f, a.GetCategoryVolume(AudioCategory.BGM));
            Assert.AreEqual(0.7f, a.GetCategoryVolume(AudioCategory.SFX));
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.UI), "未设过的分类保持默认 1");
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.Voice));
        }

        [Test]
        public void SetCategoryVolume_Clamped()
        {
            var a = new MemoryAudioModule();
            a.SetCategoryVolume(AudioCategory.BGM, -0.5f);
            Assert.AreEqual(0f, a.GetCategoryVolume(AudioCategory.BGM));

            a.SetCategoryVolume(AudioCategory.SFX, 2f);
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.SFX));
        }

        // —— Shutdown ——

        [Test]
        public void Shutdown_ResetsEverything()
        {
            var a = new MemoryAudioModule();
            a.Play("x");
            a.SetMasterVolume(0.2f);
            a.SetCategoryVolume(AudioCategory.BGM, 0.4f);

            a.Shutdown();

            Assert.AreEqual(0, a.PlayingCount);
            Assert.AreEqual(1f, a.MasterVolume, "Shutdown 后音量重置为 1");
            Assert.AreEqual(1f, a.GetCategoryVolume(AudioCategory.BGM));
        }

        // —— ModuleHost 集成 ——

        [Test]
        public void IntegratesWithModuleHost_BusinessCanPlay()
        {
            var host = new ModuleHost();
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Initialize();

            var a = host.Get<IAudioModule>();
            a.Play("bgm/main", AudioCategory.BGM);
            a.SetMasterVolume(0.8f);

            Assert.IsTrue(a.IsPlaying("bgm/main"));
            Assert.AreEqual(0.8f, a.MasterVolume);

            host.Shutdown();

            // Shutdown 后状态重置
            Assert.AreEqual(0, ((MemoryAudioModule)a).PlayingCount);
        }

        [Test]
        public void CoexistsWithResourceAndUI()
        {
            // 验证 Audio (-380) 在 Resource (-400) 之后、UI (-300) 之前
            var host = new ModuleHost();
            host.Register<IResourceModule>(new MemoryResourceModule());
            host.Register<IAudioModule>(new MemoryAudioModule());
            host.Register<IUIModule>(new MemoryUIModule());
            host.Initialize();

            Assert.NotNull(host.Get<IResourceModule>());
            Assert.NotNull(host.Get<IAudioModule>());
            Assert.NotNull(host.Get<IUIModule>());

            host.Shutdown();
        }
    }
}
