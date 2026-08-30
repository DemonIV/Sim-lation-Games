using UnityEngine;

namespace Sim.Core
{
    /// <summary>Flare/chaff dispenser: limited charges with a cooldown between salvos. Pure logic.</summary>
    public class CountermeasureSystem
    {
        public int MaxCharges;
        public float CooldownSeconds;
        /// <summary>Base probability that a well-timed salvo defeats a tracking missile.</summary>
        public float DecoyProbability;

        public int Charges { get; private set; }
        public float Cooldown { get; private set; }

        public CountermeasureSystem(int maxCharges = 8, float cooldownSeconds = 2f, float decoyProbability = 0.6f)
        {
            MaxCharges = Mathf.Max(0, maxCharges);
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            DecoyProbability = Mathf.Clamp01(decoyProbability);
            Charges = MaxCharges;
        }

        public bool CanDeploy => Charges > 0 && Cooldown <= 0f;
        public float ChargeFraction => MaxCharges > 0 ? (float)Charges / MaxCharges : 0f;

        public bool TryDeploy()
        {
            if (!CanDeploy) return false;
            Charges--;
            Cooldown = CooldownSeconds;
            return true;
        }

        public void Tick(float dt)
        {
            if (dt > 0f && Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt);
        }

        public void Reload() => Charges = MaxCharges;
    }
}
