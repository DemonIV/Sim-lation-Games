using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Banks a drone's silhouette into its turns. Measures how fast the ROOT is yawing and rolls the
    /// "Model" child by a proportional amount, smoothed over time.
    ///
    /// Purely cosmetic: ONLY the "Model" child's local rotation is written — the root transform (which
    /// the flight model owns) is never touched, so guidance, collision and targeting are unaffected.
    /// </summary>
    public class BankingVisual : MonoBehaviour
    {
        [Header("Banking")]
        // Degrees of roll per degree/second of yaw rate.
        [SerializeField] private float bankPerDegPerSec = 0.6f;
        [SerializeField] private float maxBankDeg = 45f;
        // Higher = snappier response to a change in turn rate.
        [SerializeField] private float smoothing = 5f;

        private Transform _model;
        private Vector3 _prevForward = Vector3.forward;
        private float _roll;

        private void Start()
        {
            _model = transform.Find("Model");
            _prevForward = transform.forward;
        }

        private void Update()
        {
            // The model child is built by VehicleModelBuilder; retry the lookup if it appeared later.
            if (_model == null)
            {
                _model = transform.Find("Model");
                if (_model == null) return;
            }

            float dt = Time.deltaTime;
            if (dt <= 1e-5f) return;

            // Signed yaw rate on the horizontal plane: positive when turning to the right.
            Vector3 prev = new Vector3(_prevForward.x, 0f, _prevForward.z);
            Vector3 now = new Vector3(transform.forward.x, 0f, transform.forward.z);
            _prevForward = transform.forward;

            float targetRoll = 0f;
            if (prev.sqrMagnitude > 1e-6f && now.sqrMagnitude > 1e-6f)
            {
                prev = prev.normalized;
                now = now.normalized;

                float deltaDeg = Vector3.Angle(prev, now);
                float sign = Vector3.Dot(Vector3.Cross(prev, now), Vector3.up) < 0f ? -1f : 1f;
                float yawRate = (deltaDeg * sign) / dt;

                // Rolling right (right wing down) is a NEGATIVE rotation about the forward/Z axis.
                targetRoll = Mathf.Clamp(-yawRate * bankPerDegPerSec, -maxBankDeg, maxBankDeg);
            }

            _roll = Mathf.Lerp(_roll, targetRoll, Mathf.Clamp01(smoothing * dt));
            _model.localRotation = Quaternion.Euler(0f, 0f, _roll);
        }
    }
}
