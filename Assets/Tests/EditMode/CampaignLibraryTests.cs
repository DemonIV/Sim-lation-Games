using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// Campaign shape: the authored ramp and the fact that levels reuse the shipped scenario data
    /// instead of inventing their own. Asserts RELATIONSHIPS (ramp direction, structure) rather than
    /// literal enemy counts, so the levels can be retuned without rewriting the suite.
    /// </summary>
    public class CampaignLibraryTests
    {
        [Test]
        public void Campaign_HasAtLeastSixLevelsWithSequentialIndices()
        {
            Assert.GreaterOrEqual(CampaignLibrary.Count, 6);
            Assert.AreEqual(CampaignLibrary.Count, CampaignLibrary.All.Count);

            for (int i = 0; i < CampaignLibrary.Count; i++)
            {
                CampaignLevel level = CampaignLibrary.All[i];
                Assert.AreEqual(i + 1, level.Index);
                Assert.IsFalse(string.IsNullOrEmpty(level.Name));
                Assert.IsFalse(string.IsNullOrEmpty(level.Brief));
                Assert.GreaterOrEqual(level.TotalWaves, 1);
            }
        }

        [Test]
        public void Ramp_DifficultyAndRewardsRiseWithEveryLevel()
        {
            for (int index = 2; index <= CampaignLibrary.Count; index++)
            {
                CampaignLevel prev = CampaignLibrary.Get(index - 1);
                CampaignLevel cur = CampaignLibrary.Get(index);

                Assert.Greater(cur.DifficultyMultiplier, prev.DifficultyMultiplier);
                Assert.Greater(cur.BaseReward, prev.BaseReward);
                Assert.GreaterOrEqual(cur.RewardPerKill, prev.RewardPerKill);

                // Length and how deep into the wave ramp a level starts never go BACKWARDS.
                Assert.GreaterOrEqual(cur.TotalWaves, prev.TotalWaves);
                Assert.GreaterOrEqual(cur.StartWaveOffset, prev.StartWaveOffset);
            }
        }

        [Test]
        public void Ramp_FirstLevelIsShortestAndLastIsLongest()
        {
            Assert.AreEqual(1f, CampaignLibrary.First.DifficultyMultiplier, 1e-4f);
            Assert.AreEqual(0, CampaignLibrary.First.StartWaveOffset);

            CampaignLevel last = CampaignLibrary.Get(CampaignLibrary.Count);
            Assert.Greater(last.TotalWaves, CampaignLibrary.First.TotalWaves);
            Assert.Greater(last.TotalHostiles, CampaignLibrary.First.TotalHostiles);
        }

        [Test]
        public void EarlyLevels_HaveNoAirDefenceAndNoFighters()
        {
            for (int index = 1; index <= 2; index++)
            {
                CampaignLevel level = CampaignLibrary.Get(index);
                for (int w = 0; w < level.TotalWaves; w++)
                {
                    WaveComposition c = level.Composition(w);
                    Assert.AreEqual(0, c.Sams, $"level {index} wave {w} should have no SAM");
                    Assert.AreEqual(0, c.Fighters, $"level {index} wave {w} should have no fighter");
                }
            }
        }

        [Test]
        public void LaterLevels_IntroduceAirDefence()
        {
            bool anySam = false;
            for (int index = 4; index <= CampaignLibrary.Count; index++)
            {
                CampaignLevel level = CampaignLibrary.Get(index);
                for (int w = 0; w < level.TotalWaves; w++)
                {
                    if (level.Composition(w).Sams > 0) anySam = true;
                }
            }
            Assert.IsTrue(anySam, "the back half of the campaign must field SAM batteries");
        }

        [Test]
        public void Composition_ReusesScenarioLibraryWithTheLevelOffset()
        {
            foreach (CampaignLevel level in CampaignLibrary.All)
            {
                for (int w = 0; w < level.TotalWaves; w++)
                {
                    WaveComposition expected =
                        ScenarioLibrary.Composition(level.Scenario, level.StartWaveOffset + w);
                    Assert.AreEqual(expected.Total, level.Composition(w).Total);
                }
            }
        }

        [Test]
        public void Composition_ClampsNegativeWaveIndex()
        {
            CampaignLevel level = CampaignLibrary.Get(5);
            Assert.AreEqual(level.Composition(0).Total, level.Composition(-4).Total);
        }

        [Test]
        public void Get_RejectsOutOfRangeIndexWithoutThrowing()
        {
            Assert.IsNull(CampaignLibrary.Get(0));
            Assert.IsNull(CampaignLibrary.Get(-3));
            Assert.IsNull(CampaignLibrary.Get(CampaignLibrary.Count + 1));

            Assert.IsFalse(CampaignLibrary.IsValidIndex(0));
            Assert.IsTrue(CampaignLibrary.IsValidIndex(1));
            Assert.IsTrue(CampaignLibrary.IsValidIndex(CampaignLibrary.Count));

            // GetOrFirst never returns null.
            Assert.AreEqual(CampaignLibrary.First, CampaignLibrary.GetOrFirst(99));
            Assert.AreEqual(CampaignLibrary.First, CampaignLibrary.GetOrFirst(1));
        }
    }
}
