using UnityEngine;

namespace Sim.Core
{
    /// <summary>Fire-control for a SİHA munition launcher: ammo, fire rate, cooldown, reload. Pure logic.</summary>
    public class WeaponSystem
    {
        public int MagazineSize = 8;
        public float RoundsPerSecond = 2f;
        public int Ammo { get; private set; }
        public float Cooldown { get; private set; }   // seconds until next allowed shot

        public WeaponSystem(int magazineSize = 8, float roundsPerSecond = 2f)
        {
            MagazineSize = magazineSize;
            RoundsPerSecond = roundsPerSecond;
            Ammo = magazineSize;
            Cooldown = 0f;
        }

        public bool CanFire(bool hasLock) => hasLock && Ammo > 0 && Cooldown <= 0f;

        /// <summary>Attempts to fire. On success decrements ammo, sets cooldown, returns true.</summary>
        public bool TryFire(bool hasLock)
        {
            if (!CanFire(hasLock)) return false;
            Ammo--;
            Cooldown = RoundsPerSecond > 0f ? 1f / RoundsPerSecond : 0f;
            return true;
        }

        public void Tick(float dt)
        {
            if (Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt);
        }

        public void Reload() => Ammo = MagazineSize;
    }
}
