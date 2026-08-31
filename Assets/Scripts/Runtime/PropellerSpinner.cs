using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Spins the model's "Propeller" part around its local Z axis. The rate follows the owner's
    /// airspeed when an <see cref="IhaController"/> is present, otherwise it runs at a constant
    /// cruise rate.
    ///
    /// Purely cosmetic — it only rotates a child transform and never touches the root or any
    /// gameplay state. Uses scaled time so pausing the game stops the blades.
    /// </summary>
    public class PropellerSpinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecondAtCruise = 2400f;

        // Speed (m/s) treated as "cruise" when scaling the spin rate.
        [SerializeField] private float cruiseSpeed = 30f;

        private Transform _propeller;
        private IhaController _owner;

        private void Start()
        {
            _propeller = FindChild("Propeller");
            _owner = GetComponent<IhaController>();
        }

        private void Update()
        {
            if (_propeller == null) return;

            float rate = degreesPerSecondAtCruise;
            if (_owner != null && cruiseSpeed > 1e-3f)
            {
                // Faster drone, faster blades — clamped so it never stops dead or strobes wildly.
                float scale = Mathf.Clamp(_owner.Speed / cruiseSpeed, 0.25f, 2f);
                rate *= scale;
            }

            _propeller.Rotate(0f, 0f, rate * Time.deltaTime, Space.Self);
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
