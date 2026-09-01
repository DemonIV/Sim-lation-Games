using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>Unlock ordering, idempotent completion, best-grade-only-improves and bad indices.</summary>
    public class CampaignProgressTests
    {
        [Test]
        public void FreshProgress_OnlyLevelOneIsUnlockedAndNothingIsCompleted()
        {
            var p = new CampaignProgress();

            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));
            Assert.IsFalse(p.IsCompleted(1));
            Assert.AreEqual(0, p.BestStars(1));
            Assert.AreEqual(0, p.TotalStars);
            Assert.AreEqual(1, p.NextUnclearedLevel());
        }

        [Test]
        public void CompletingLevel_UnlocksTheNextOneOnly()
        {
            var p = new CampaignProgress();

            Assert.IsTrue(p.Complete(1, 2));

            Assert.IsTrue(p.IsCompleted(1));
            Assert.IsTrue(p.IsUnlocked(2));
            Assert.IsFalse(p.IsUnlocked(3));
            Assert.AreEqual(2, p.NextUnclearedLevel());
        }

        [Test]
        public void Unlocks_AdvanceOneStepAtATimeThroughTheCampaign()
        {
            var p = new CampaignProgress();

            for (int index = 1; index <= CampaignLibrary.Count; index++)
            {
                Assert.IsTrue(p.IsUnlocked(index), $"level {index} should be unlocked by now");
                p.Complete(index, 3);
            }

            Assert.AreEqual(CampaignLibrary.Count, p.HighestUnlocked);
            Assert.AreEqual(3 * CampaignLibrary.Count, p.TotalStars);
            Assert.AreEqual(CampaignLibrary.Count, p.NextUnclearedLevel());
        }

        [Test]
        public void CompletingLevelOne_DoesNotSkipAhead()
        {
            var p = new CampaignProgress();
            p.Complete(1, 3);
            p.Complete(1, 3);
            p.Complete(1, 3);

            Assert.AreEqual(2, p.HighestUnlocked);
            Assert.IsFalse(p.IsUnlocked(3));
        }

        [Test]
        public void BestGrade_OnlyEverImproves()
        {
            var p = new CampaignProgress();

            p.Complete(1, 1);
            Assert.AreEqual(1, p.BestStars(1));

            p.Complete(1, 3);
            Assert.AreEqual(3, p.BestStars(1));

            // A worse replay must not erase the record.
            p.Complete(1, 0);
            Assert.AreEqual(3, p.BestStars(1));
            Assert.IsTrue(p.IsCompleted(1));
        }

        [Test]
        public void Grade_IsClampedToZeroThree()
        {
            var p = new CampaignProgress();

            p.Complete(1, 99);
            Assert.AreEqual(3, p.BestStars(1));

            p.Complete(2, -5);
            Assert.AreEqual(0, p.BestStars(2));
            Assert.IsTrue(p.IsCompleted(2));
        }

        [Test]
        public void OutOfRangeIndex_IsRejectedNotThrown()
        {
            var p = new CampaignProgress();

            Assert.IsFalse(p.Complete(0, 3));
            Assert.IsFalse(p.Complete(-2, 3));
            Assert.IsFalse(p.Complete(CampaignLibrary.Count + 1, 3));

            Assert.IsFalse(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(CampaignLibrary.Count + 1));
            Assert.IsFalse(p.IsCompleted(0));
            Assert.AreEqual(0, p.BestStars(CampaignLibrary.Count + 1));

            // Nothing above may have moved the state.
            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.AreEqual(0, p.TotalStars);
        }

        [Test]
        public void Restore_RebuildsUnlocksFromCompletionsAndSurvivesGarbage()
        {
            var p = new CampaignProgress();

            // Completions for levels 1..3 with a stored unlock value that is far too low, and arrays
            // that are the wrong length / hold nonsense grades.
            var completed = new bool[] { true, true, true };
            var stars = new int[] { 9, -4, 2 };
            p.Restore(completed, stars, 1);

            Assert.AreEqual(3, p.BestStars(1));
            Assert.AreEqual(0, p.BestStars(2));
            Assert.AreEqual(2, p.BestStars(3));
            Assert.AreEqual(4, p.HighestUnlocked);   // re-derived from completions, not trusted
            Assert.IsFalse(p.IsCompleted(4));
        }

        [Test]
        public void Restore_NullArraysYieldAFreshState()
        {
            var p = new CampaignProgress();
            p.Complete(1, 3);

            p.Restore(null, null, 0);

            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.IsFalse(p.IsCompleted(1));
            Assert.AreEqual(0, p.TotalStars);
        }

        [Test]
        public void Snapshots_RoundTripThroughRestore()
        {
            var p = new CampaignProgress();
            p.Complete(1, 3);
            p.Complete(2, 1);

            var restored = new CampaignProgress();
            restored.Restore(p.CompletionSnapshot(), p.StarsSnapshot(), p.HighestUnlocked);

            Assert.AreEqual(p.HighestUnlocked, restored.HighestUnlocked);
            Assert.AreEqual(p.TotalStars, restored.TotalStars);
            Assert.IsTrue(restored.IsCompleted(2));
            Assert.AreEqual(1, restored.BestStars(2));
        }
    }

    /// <summary>Reward maths: grade/difficulty scaling, no negative money, no replay farming.</summary>
    public class CampaignRewardTests
    {
        private static CampaignLevel L1 => CampaignLibrary.Get(1);
        private static CampaignLevel Last => CampaignLibrary.Get(CampaignLibrary.Count);

        [Test]
        public void StarFactor_ZeroStarsPaysNoClearBonusAndRisesWithGrade()
        {
            Assert.AreEqual(0f, CampaignReward.StarFactor(0), 1e-4f);
            Assert.Less(CampaignReward.StarFactor(1), CampaignReward.StarFactor(2));
            Assert.Less(CampaignReward.StarFactor(2), CampaignReward.StarFactor(3));

            // Clamped, never extrapolated.
            Assert.AreEqual(CampaignReward.StarFactor(3), CampaignReward.StarFactor(9), 1e-4f);
            Assert.AreEqual(CampaignReward.StarFactor(0), CampaignReward.StarFactor(-2), 1e-4f);
        }

        [Test]
        public void FirstClear_PaysTheFullFormula()
        {
            // (BaseReward * StarFactor(3) + kills * RewardPerKill) * DifficultyMultiplier
            int expected = Mathf_RoundToInt((L1.BaseReward * CampaignReward.StarFactor(3)
                                             + 5 * L1.RewardPerKill) * L1.DifficultyMultiplier);
            Assert.AreEqual(expected, CampaignReward.Money(L1, 5, 0, 3, true));
        }

        [Test]
        public void MoreKillsAndBetterGrade_PayMore()
        {
            int few = CampaignReward.Money(L1, 2, 0, 3, true);
            int many = CampaignReward.Money(L1, 8, 0, 3, true);
            Assert.Greater(many, few);

            int oneStar = CampaignReward.Money(L1, 5, 0, 1, true);
            int threeStar = CampaignReward.Money(L1, 5, 0, 3, true);
            Assert.Greater(threeStar, oneStar);
        }

        [Test]
        public void FriendlyLosses_ReduceThePayoutAndCanNeverGoNegative()
        {
            int clean = CampaignReward.Money(L1, 4, 0, 3, true);
            int costly = CampaignReward.Money(L1, 4, 1, 3, true);
            Assert.Less(costly, clean);

            // A failed mission with heavy losses pays nothing at all — never negative money.
            Assert.AreEqual(0, CampaignReward.Money(L1, 0, 20, 0, true));
        }

        [Test]
        public void HarderLevels_PayMoreForTheSamePerformance()
        {
            Assert.Greater(CampaignReward.Money(Last, 5, 0, 3, true),
                           CampaignReward.Money(L1, 5, 0, 3, true));
        }

        [Test]
        public void Replay_PaysOnlyAFractionSoGrindingIsNotAFaucet()
        {
            int first = CampaignReward.Money(L1, 5, 0, 3, true);
            int replay = CampaignReward.Money(L1, 5, 0, 3, false);

            Assert.Greater(first, 0);
            Assert.Less(replay, first);
            Assert.AreEqual(Mathf_RoundToInt(first * CampaignReward.ReplayFactor), replay, 1);

            // Four replays must still be worth less than one fresh clear of the same level.
            Assert.LessOrEqual(replay * 4, first);
        }

        [Test]
        public void NullLevel_PaysZeroInsteadOfThrowing()
        {
            Assert.AreEqual(0, CampaignReward.Money(null, 10, 0, 3, true));
        }

        /// <summary>Local mirror of UnityEngine.Mathf.RoundToInt, so the test states its own maths.</summary>
        private static int Mathf_RoundToInt(float value)
        {
            return (int)System.Math.Round(value, System.MidpointRounding.ToEven);
        }
    }
}
