using System.Collections.Generic;
using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// A spectator camera so the player can watch the simulation. Two modes:
    /// <list type="bullet">
    /// <item>Free-fly: WASD to move relative to the camera, Q/E (or Space/Ctrl) for down/up,
    /// Left Shift to boost, hold right mouse to look around, scroll wheel to zoom.</item>
    /// <item>Follow: press Tab to cycle through friendly drones; the camera trails the selected
    /// drone from behind and above. Press F (or move with WASD) to return to free-fly.</item>
    /// </list>
    /// Attach to the Main Camera. Robust to there being no drones and to the followed drone dying.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 40f;
        [SerializeField] private float boostMultiplier = 3f;
        [SerializeField] private float lookSensitivity = 3f;
        [SerializeField] private float zoomSpeed = 20f;

        // Current camera orientation as yaw/pitch, kept in sync so look and free-fly agree.
        private float _yaw;
        private float _pitch;

        // Follow-mode state.
        private bool _following;
        private Targetable _followTarget;
        private int _followIndex = -1;

        private void Start()
        {
            // Seed yaw/pitch from the camera's current rotation so there is no snap on first look.
            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(euler.x);
            _yaw = euler.y;
        }

        private void Update()
        {
            // Cycle followed drone with Tab.
            if (Input.GetKeyDown(KeyCode.Tab)) CycleFollowTarget();

            // Exit follow mode explicitly with F.
            if (Input.GetKeyDown(KeyCode.F)) StopFollowing();

            if (_following)
                UpdateFollow();
            else
                UpdateFreeFly();
        }

        /// <summary>Free-fly movement + mouse look + scroll zoom, all in unscaled time.</summary>
        private void UpdateFreeFly()
        {
            float dt = Time.unscaledDeltaTime;

            // Mouse look while holding the right mouse button.
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // Gather WASD + vertical input.
            float x = 0f, y = 0f, z = 0f;
            if (Input.GetKey(KeyCode.W)) z += 1f;
            if (Input.GetKey(KeyCode.S)) z -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) y += 1f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) y -= 1f;

            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= boostMultiplier;

            // Move in world space along the camera's own axes.
            Vector3 move = (transform.right * x + transform.forward * z + transform.up * y) * speed * dt;
            transform.position += move;

            // Scroll wheel zooms by sliding along the view direction.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 1e-5f)
                transform.position += transform.forward * scroll * zoomSpeed;
        }

        /// <summary>Smoothly trails the followed drone from behind and above.</summary>
        private void UpdateFollow()
        {
            // Any movement input drops back to free-fly for manual control.
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
            {
                StopFollowing();
                return;
            }

            // Guard against a destroyed/despawned target.
            if (_followTarget == null)
            {
                StopFollowing();
                return;
            }

            Transform t = _followTarget.transform;
            Vector3 desired = t.position - t.forward * 15f + Vector3.up * 6f;
            transform.position = Vector3.Lerp(transform.position, desired, 5f * Time.unscaledDeltaTime);
            transform.LookAt(t.position);

            // Keep yaw/pitch in sync so returning to free-fly doesn't snap the view.
            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(euler.x);
            _yaw = euler.y;
        }

        /// <summary>Advances to the next friendly drone (Faction == 0) in the registry.</summary>
        private void CycleFollowTarget()
        {
            var friendlies = new List<Targetable>();
            var all = TargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                Targetable t = all[i];
                if (t != null && t.Faction == 0) friendlies.Add(t);
            }

            if (friendlies.Count == 0)
            {
                // Nothing to follow — stay/return to free-fly.
                StopFollowing();
                return;
            }

            _followIndex = (_followIndex + 1) % friendlies.Count;
            _followTarget = friendlies[_followIndex];
            _following = true;
        }

        /// <summary>Drops back into free-fly mode.</summary>
        private void StopFollowing()
        {
            _following = false;
            _followTarget = null;
        }

        /// <summary>Maps a 0..360 Euler pitch into a signed -180..180 range for clamping.</summary>
        private static float NormalizePitch(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
