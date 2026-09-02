using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Covers the master volume / mute rules (<see cref="SoundSettings"/>) the audio key binding and
    /// the persistence layer both sit on.
    /// </summary>
    public class SoundSettingsTests
    {
        [Test]
        public void FreshSettings_AreAudible()
        {
            var s = new SoundSettings();
            Assert.AreEqual(SoundSettings.DefaultVolume, s.MasterVolume, 1e-4f);
            Assert.IsFalse(s.Muted);
            Assert.IsFalse(s.IsSilent);
            Assert.AreEqual(s.MasterVolume, s.EffectiveVolume, 1e-4f);
        }

        [Test]
        public void Volume_IsAlwaysClamped()
        {
            var s = new SoundSettings();

            s.SetVolume(5f);
            Assert.AreEqual(SoundSettings.MaxVolume, s.MasterVolume, 1e-4f);

            s.SetVolume(-5f);
            Assert.AreEqual(SoundSettings.MinVolume, s.MasterVolume, 1e-4f);

            // NaN / infinity leave the setting alone instead of poisoning the mixer.
            s.SetVolume(0.5f);
            s.SetVolume(float.NaN);
            s.SetVolume(float.PositiveInfinity);
            Assert.AreEqual(0.5f, s.MasterVolume, 1e-4f);
        }

        [Test]
        public void Mute_RemembersTheLevelUnderneath()
        {
            var s = new SoundSettings();
            s.SetVolume(0.4f);

            Assert.IsTrue(s.ToggleMute());
            Assert.AreEqual(0f, s.EffectiveVolume, 1e-4f);
            Assert.IsTrue(s.IsSilent);
            // The STORED level survives the mute — this is why mute is not "set volume to zero".
            Assert.AreEqual(0.4f, s.MasterVolume, 1e-4f);

            Assert.IsFalse(s.ToggleMute());
            Assert.AreEqual(0.4f, s.EffectiveVolume, 1e-4f);
        }

        [Test]
        public void UnMuting_FromZero_CannotLookLikeADeadKey()
        {
            var s = new SoundSettings();
            s.SetVolume(0f);
            s.ToggleMute();     // muted, stored level 0
            s.ToggleMute();     // un-muted...

            Assert.IsFalse(s.Muted);
            Assert.Greater(s.EffectiveVolume, 0f);
            Assert.AreEqual(SoundSettings.DefaultVolume, s.MasterVolume, 1e-4f);
        }

        [Test]
        public void Stepping_MovesByOneStepAndUnMutes()
        {
            var s = new SoundSettings();
            s.SetVolume(0.5f);

            Assert.AreEqual(0.5f + SoundSettings.VolumeStep, s.StepVolume(1), 1e-4f);
            Assert.AreEqual(0.5f, s.StepVolume(-1), 1e-4f);

            // A player pressing "louder" on a muted game means "I want to hear it".
            s.ToggleMute();
            Assert.IsTrue(s.Muted);
            s.StepVolume(1);
            Assert.IsFalse(s.Muted);
            Assert.Greater(s.EffectiveVolume, 0f);

            // A zero step is a no-op in both respects.
            s.ToggleMute();
            s.StepVolume(0);
            Assert.IsTrue(s.Muted);
        }

        [Test]
        public void Stepping_StopsAtTheEnds()
        {
            var s = new SoundSettings();
            for (int i = 0; i < 40; i++) s.StepVolume(1);
            Assert.AreEqual(SoundSettings.MaxVolume, s.MasterVolume, 1e-4f);

            for (int i = 0; i < 40; i++) s.StepVolume(-1);
            Assert.AreEqual(SoundSettings.MinVolume, s.MasterVolume, 1e-4f);
            Assert.IsTrue(s.IsSilent);
            Assert.IsFalse(s.Muted);    // silent by level, not by mute
        }

        [Test]
        public void Restore_IsTotal()
        {
            var s = new SoundSettings();

            s.Restore(0.35f, true);
            Assert.AreEqual(0.35f, s.MasterVolume, 1e-4f);
            Assert.IsTrue(s.Muted);

            // Corrupt or impossible stored values fall back instead of throwing.
            s.Restore(float.NaN, false);
            Assert.AreEqual(SoundSettings.DefaultVolume, s.MasterVolume, 1e-4f);

            s.Restore(12f, false);
            Assert.AreEqual(SoundSettings.MaxVolume, s.MasterVolume, 1e-4f);

            s.Restore(-3f, false);
            Assert.AreEqual(SoundSettings.MinVolume, s.MasterVolume, 1e-4f);
        }

        [Test]
        public void Label_ReadsAsTheHudShowsIt()
        {
            var s = new SoundSettings();
            s.SetVolume(0.7f);
            Assert.AreEqual(70, s.VolumePercent);
            Assert.AreEqual("SES %70", s.Label());

            s.ToggleMute();
            Assert.AreEqual("SES KAPALI", s.Label());
        }
    }
}
