using UnityEngine;

namespace Sim.Core
{
    /// <summary>Rapid-fire gun/cannon: ammo belt, rate of fire, effective range, dispersion. Pure logic.</summary>
    public class GunSystem
    {
        public int MagazineSize;
        public float RoundsPerSecond;
        public float EffectiveRange;
        public float DispersionDeg;

        public int Ammo { get; private set; }
        public float Cooldown { get; private set; }

        public GunSystem(int magazineSize = 300, float roundsPerSecond = 10f,
                         float effectiveRange = 60f, float dispersionDeg = 2f)
        {
            MagazineSize = Mathf.Max(0, magazineSize);
            RoundsPerSecond = Mathf.Max(0.01f, roundsPerSecond);
            EffectiveRange = Mathf.Max(0f, effectiveRange);
            DispersionDeg = Mathf.Max(0f, dispersionDeg);
            Ammo = MagazineSize;
        }

        public bool CanFire => Ammo > 0 && Cooldown <= 0f;
        public float AmmoFraction => MagazineSize > 0 ? (float)Ammo / MagazineSize : 0f;
        public bool InRange(float distance) => distance <= EffectiveRange;

        /// <summary>Fires one round if possible: consumes ammo and starts the cooldown.</summary>
        public bool TryFire()
        {
            if (!CanFire) return false;
            Ammo--;
            Cooldown = 1f / RoundsPerSecond;
            return true;
        }

        public void Tick(float dt)
        {
            if (dt > 0f && Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt);
        }

        public void Reload() => Ammo = MagazineSize;
    }
}
