using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    public class TerrainFieldTests
    {
        [Test]
        public void AirbaseArea_IsPerfectlyLevel()
        {
            Assert.AreEqual(0f, TerrainField.Height(0f, 0f), 1e-4f);
            Assert.AreEqual(0f, TerrainField.Height(30f, 20f), 1e-4f);   // inside FlatRadius
        }

        [Test]
        public void Height_StaysWithinAmplitude()
        {
            for (int x = -200; x <= 200; x += 37)
                for (int z = -200; z <= 200; z += 41)
                    Assert.LessOrEqual(Mathf.Abs(TerrainField.Height(x, z)), TerrainField.Amplitude + 1e-3f);
        }

        [Test]
        public void Height_IsDeterministic()
        {
            Assert.AreEqual(TerrainField.Height(123f, -87f), TerrainField.Height(123f, -87f), 1e-6f);
        }

        [Test]
        public void FarFromBase_HasRelief()
        {
            // Sample a spread of far points; at least one must be meaningfully non-zero.
            bool anyRelief = false;
            for (int i = 0; i < 40 && !anyRelief; i++)
                if (Mathf.Abs(TerrainField.Height(150f + i * 13f, -150f - i * 7f)) > 0.1f) anyRelief = true;
            Assert.IsTrue(anyRelief);
        }
    }
}
