using System;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// AudioModule V0.5 增强（Pause/Resume/PauseAll/ResumeAll）的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class AudioModuleV05Tests
    {
        // —— Pause / Resume ——

        [Test]
        public void Pause_Playing_SetsIsPaused()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);

            Assert.IsTrue(a.Pause("bgm"));
            Assert.IsTrue(a.IsPaused("bgm"));
            Assert.IsTrue(a.IsPlaying("bgm"), "Paused 仍算 Playing（保留在播放列表）");
            Assert.AreEqual(1, a.PlayingCount);
        }

        [Test]
        public void Pause_NotPlaying_ReturnsFalse()
        {
            var a = new MemoryAudioModule();
            Assert.IsFalse(a.Pause("ghost"));
        }

        [Test]
        public void Pause_AlreadyPaused_ReturnsFalse()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Pause("bgm");
            Assert.IsFalse(a.Pause("bgm"), "重复 Pause 返 false");
        }

        [Test]
        public void Pause_NullOrEmpty_ReturnsFalse()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm");
            Assert.IsFalse(a.Pause(null));
            Assert.IsFalse(a.Pause(""));
            Assert.IsFalse(a.IsPaused("bgm"), "实际 cue 未被误暂停");
        }

        [Test]
        public void Resume_Paused_UnsetsIsPaused()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Pause("bgm");

            Assert.IsTrue(a.Resume("bgm"));
            Assert.IsFalse(a.IsPaused("bgm"));
            Assert.IsTrue(a.IsPlaying("bgm"));
        }

        [Test]
        public void Resume_NotPaused_ReturnsFalse()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm");
            Assert.IsFalse(a.Resume("bgm"), "未暂停 Resume 返 false");
        }

        [Test]
        public void Resume_NotPlaying_ReturnsFalse()
        {
            var a = new MemoryAudioModule();
            Assert.IsFalse(a.Resume("ghost"));
        }

        [Test]
        public void IsPaused_NotPlaying_False()
        {
            var a = new MemoryAudioModule();
            Assert.IsFalse(a.IsPaused("ghost"));
            Assert.IsFalse(a.IsPaused(null));
        }

        // —— PauseAll / ResumeAll ——

        [Test]
        public void PauseAll_Category_PausesOnlyThatCategory()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm1", AudioCategory.BGM);
            a.Play("bgm2", AudioCategory.BGM);
            a.Play("sfx1", AudioCategory.SFX);

            a.PauseAll(AudioCategory.BGM);

            Assert.IsTrue(a.IsPaused("bgm1"));
            Assert.IsTrue(a.IsPaused("bgm2"));
            Assert.IsFalse(a.IsPaused("sfx1"), "SFX 不应被 PauseAll(BGM) 暂停");
        }

        [Test]
        public void PauseAll_EmptyCategory_NoThrow()
        {
            var a = new MemoryAudioModule();
            Assert.DoesNotThrow(() => a.PauseAll(AudioCategory.BGM));
        }

        [Test]
        public void PauseAll_AlreadyPaused_NoEffect()
        {
            // 重复 PauseAll 已暂停的应仍是 paused
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.PauseAll(AudioCategory.BGM);
            Assert.DoesNotThrow(() => a.PauseAll(AudioCategory.BGM));
            Assert.IsTrue(a.IsPaused("bgm"));
        }

        [Test]
        public void ResumeAll_Category_ResumesOnlyPaused()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm1", AudioCategory.BGM);
            a.Play("bgm2", AudioCategory.BGM);
            a.Pause("bgm1");
            // bgm2 仍在播放，没暂停

            a.ResumeAll(AudioCategory.BGM);

            Assert.IsFalse(a.IsPaused("bgm1"));
            Assert.IsFalse(a.IsPaused("bgm2"));
        }

        // —— 重新 Play 隐含 Resume ——

        [Test]
        public void Play_RePlayPausedCue_ClearsPausedState()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Pause("bgm");
            Assert.IsTrue(a.IsPaused("bgm"));

            // 重新 Play 应隐含恢复（业务"重启"语义）
            a.Play("bgm", AudioCategory.BGM);
            Assert.IsFalse(a.IsPaused("bgm"), "重新 Play 清 paused 状态");
        }

        // —— Stop 暂停的 cue ——

        [Test]
        public void Stop_PausedCue_RemovesFromList()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Pause("bgm");

            a.Stop("bgm");

            Assert.IsFalse(a.IsPlaying("bgm"));
            Assert.IsFalse(a.IsPaused("bgm"));
        }

        // —— StopAll 包括暂停的 ——

        [Test]
        public void StopAll_IncludesPaused()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm1", AudioCategory.BGM);
            a.Play("bgm2", AudioCategory.BGM);
            a.Pause("bgm1");

            a.StopAll(AudioCategory.BGM);

            Assert.IsFalse(a.IsPlaying("bgm1"));
            Assert.IsFalse(a.IsPlaying("bgm2"));
        }

        // —— Shutdown 重置 ——

        [Test]
        public void Shutdown_ClearsPausedState()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm", AudioCategory.BGM);
            a.Pause("bgm");

            a.Shutdown();

            Assert.AreEqual(0, a.PlayingCount);
            Assert.IsFalse(a.IsPaused("bgm"));
        }

        // —— 实际业务场景：游戏暂停菜单 ——

        [Test]
        public void GamePauseMenuScenario_PauseBgmAndSfxButNotUi()
        {
            var a = new MemoryAudioModule();
            a.Play("bgm/main", AudioCategory.BGM);
            a.Play("sfx/walk_loop", AudioCategory.SFX);
            a.Play("ui/menu_open", AudioCategory.UI);

            // 玩家打开暂停菜单：暂停 BGM + SFX，UI 仍播放（按钮反馈)
            a.PauseAll(AudioCategory.BGM);
            a.PauseAll(AudioCategory.SFX);

            Assert.IsTrue(a.IsPaused("bgm/main"));
            Assert.IsTrue(a.IsPaused("sfx/walk_loop"));
            Assert.IsFalse(a.IsPaused("ui/menu_open"));

            // 玩家关闭暂停菜单
            a.ResumeAll(AudioCategory.BGM);
            a.ResumeAll(AudioCategory.SFX);

            Assert.IsFalse(a.IsPaused("bgm/main"));
            Assert.IsFalse(a.IsPaused("sfx/walk_loop"));
        }
    }
}
