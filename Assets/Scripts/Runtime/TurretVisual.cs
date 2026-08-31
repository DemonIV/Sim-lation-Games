using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Animates a SAM/AAA site's silhouette: the "Radar" dish sweeps continuously and the "Turret"
    /// slews to face whatever the owning <see cref="AirDefenseSite"/> currently holds, idling with a
    /// slow scan when nothing is tracked.
    ///
    /// Purely cosmetic: only the named CHILD transforms are rotated (yaw only for the turret) — the
    /// site's root, its detection logic and its firing solution are untouched.
    /// </summary>
    public class TurretVisual : MonoBehaviour
    {
        [SerializeField] private float radarSpinDegPerSec = 60f;
        [SerializeField] private float turretSlewDegPerSec = 90f;
        [SerializeField] private float idleScanDegPerSec = 18f;

        private Transform _turret;
        private Transform _radar;
        private AirDefenseSite _site;

        private void Start()
        {
            _turret = FindChild("Turret");
            _radar = FindChild("Radar");
            _site = GetComponent<AirDefenseSite>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // The dish sweeps regardless of whether anything is being tracked.
            if (_radar != null) _radar.Rotate(0f, radarSpinDegPerSec * dt, 0f, Space.Self);

            if (_turret == null) return;

            Targetable tracked = null;
            if (_site != null && _site.CurrentTargetId >= 0)
                tracked = TargetRegistry.FindById(_site.CurrentTargetId);

            if (tracked == null)
            {
                // Nothing held: idle scan.
                _turret.Rotate(0f, idleScanDegPerSec * dt, 0f, Space.Self);
                return;
            }

            // Yaw-only aim: flatten the line of sight onto the horizontal plane.
            Vector3 los = tracked.transform.position - _turret.position;
            los.y = 0f;
            if (los.sqrMagnitude <= 1e-4f) return;

            Quaternion want = Quaternion.LookRotation(los.normalized, Vector3.up);
            _turret.rotation = Quaternion.RotateTowards(_turret.rotation, want, turretSlewDegPerSec * dt);
        }

        /// <summary>Finds the first descendant transform with the given name, or null.</summary>
        private Transform FindChild(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                if (t.name == childName) return t;
            }
            return null;
        }
    }
}
