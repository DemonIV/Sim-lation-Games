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

        // Pilot mode: while the player flies a drone the camera chases it and ignores its own input.
        private PlayerDroneController _pilot;
        private float _pilotSearchTimer;
        private const float PilotSearchInterval = 2f;

        // ---------------------------------------------------------------- camera feel (cosmetic)

        [Header("Feel")]
        // SmoothDamp time for the chase/follow position, and how quickly the look direction eases in.
        [SerializeField] private float followSmoothTime = 0.18f;
        [SerializeField] private float rotationLerp = 8f;
        // Extra field of view added while the piloted drone is on afterburner.
        [SerializeField] private float afterburnerFovBoost = 12f;
        [SerializeField] private float fovLerp = 4f;

        // SmoothDamp working state for the follow position.
        private Vector3 _followVelocity;

        // Cached camera + its authored FOV, so the afterburner kick always returns to the same base.
        private Camera _camera;
        private float _baseFov = 60f;

        // ---------------------------------------------------------------- cockpit view (pilot only)

        [Header("Cockpit")]
        // Seat position in the aircraft's own axes: this far up the nose and this far above the spine.
        // Applied along transform.forward/up (NOT TransformPoint) because unit roots are scaled.
        [SerializeField] private float cockpitForward = 1.6f;
        [SerializeField] private float cockpitUp = 0.5f;
        // Much stiffer than the chase cam: the seat is bolted to the airframe.
        [SerializeField] private float cockpitPositionLerp = 30f;
        [SerializeField] private float cockpitRotationLerp = 25f;
        // Degrees shaved off the base FOV inside the cockpit, for a tighter "inside" feel.
        [SerializeField] private float cockpitFovNarrow = 8f;

        // Renderers of the piloted aircraft's "Model" subtree, hidden while sitting inside it.
        private Renderer[] _hiddenRenderers;
        private Transform _hiddenModelOwner;

        // Edge detection for taking/releasing control, so the view can default to the cockpit on C.
        private bool _pilotWasActive;

        /// <summary>True while the camera is sitting in the piloted aircraft's cockpit.</summary>
        public bool CockpitView { get; private set; }

        /// <summary>Widens the FOV under afterburner and eases it back when the burner is released.</summary>
        private void UpdateFov()
        {
            if (_camera == null) return;

            // The cockpit narrows the base FOV; the afterburner kick still works relative to it.
            float baseFov = CockpitView ? Mathf.Max(20f, _baseFov - cockpitFovNarrow) : _baseFov;

            bool burner = _pilot != null && _pilot.IsActive && _pilot.AfterburnerActive;
            float target = burner ? baseFov + afterburnerFovBoost : baseFov;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target,
                                             Mathf.Clamp01(fovLerp * Time.unscaledDeltaTime));
        }

        // ---------------------------------------------------------------- camera shake (cosmetic)

        // The active rig, so effects can ask for a shake without a scene lookup every explosion.
        private static CameraRig _instance;

        // Current shake impulse: peak amplitude, total duration and how far into it we are.
        private float _shakeStrength;
        private float _shakeDuration;
        private float _shakeElapsed;

        // The offset/rotation actually written to the transform last frame. It is undone at the top of
        // Update so the follow/free-fly logic always works on the clean, un-shaken pose.
        private Vector3 _appliedShakeOffset;
        private Quaternion _appliedShakeRot = Quaternion.identity;

        /// <summary>
        /// Asks the active camera rig to shake with the given peak amplitude (world units) for the
        /// given duration. Safe to call when there is no camera rig in the scene.
        /// </summary>
        public static void RequestShake(float strength, float duration)
        {
            CameraRig rig = _instance;
            if (rig == null)
            {
                rig = FindAnyObjectByType<CameraRig>();
                _instance = rig;
            }
            if (rig == null) return;
            rig.AddShake(strength, duration);
        }

        /// <summary>Starts (or replaces with a stronger) shake impulse.</summary>
        private void AddShake(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f) return;
            // The strongest concurrent blast wins and restarts the decay.
            if (_shakeDuration <= 0f || strength >= _shakeStrength)
            {
                _shakeStrength = strength;
                _shakeDuration = duration;
                _shakeElapsed = 0f;
            }
        }

        /// <summary>Adds the decaying shake offset AFTER the normal camera placement for this frame.</summary>
        private void ApplyShake()
        {
            if (_shakeDuration <= 0f) return;

            _shakeElapsed += Time.unscaledDeltaTime;
            if (_shakeElapsed >= _shakeDuration)
            {
                _shakeDuration = 0f;
                _shakeStrength = 0f;
                return;
            }

            float amp = _shakeStrength * (1f - _shakeElapsed / _shakeDuration);
            _appliedShakeOffset = Random.insideUnitSphere * amp;
            _appliedShakeRot = Quaternion.Euler(Random.Range(-amp, amp) * 8f,
                                                Random.Range(-amp, amp) * 8f,
                                                Random.Range(-amp, amp) * 8f);

            transform.position += _appliedShakeOffset;
            transform.rotation = transform.rotation * _appliedShakeRot;
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            // Never leave an aircraft invisible because the rig went away while in the cockpit.
            RestoreHiddenModel();
        }

        private void OnDisable()
        {
            SetCockpitView(false);
            // Re-enabling the rig mid-sortie should drop the player back into the cockpit.
            _pilotWasActive = false;
        }

        private void Start()
        {
            // Seed yaw/pitch from the camera's current rotation so there is no snap on first look.
            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(euler.x);
            _yaw = euler.y;

            // Cache the camera and its authored FOV for the afterburner kick.
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _baseFov = _camera.fieldOfView;
        }

        private void LateUpdate()
        {
            UpdateFov();
            ApplyShake();
        }

        private void Update()
        {
            // Undo last frame's shake before doing anything else, so the placement logic below never
            // sees (or accumulates) the cosmetic offset.
            if (_appliedShakeOffset != Vector3.zero || _appliedShakeRot != Quaternion.identity)
            {
                transform.position -= _appliedShakeOffset;
                transform.rotation = transform.rotation * Quaternion.Inverse(_appliedShakeRot);
                _appliedShakeOffset = Vector3.zero;
                _appliedShakeRot = Quaternion.identity;
            }

            // Pilot mode wins: chase the drone the player is flying and swallow ALL camera input
            // (Tab/F/WASD belong to the pilot then). Normal behaviour returns on release.
            if (PilotActive())
            {
                _following = false;
                _followTarget = null;

                // Taking control (C) drops the player straight into the cockpit.
                if (!_pilotWasActive)
                {
                    _pilotWasActive = true;
                    CockpitView = true;
                }

                // V swaps between the chase cam and the cockpit while flying.
                if (Input.GetKeyDown(KeyCode.V)) SetCockpitView(!CockpitView);

                // PilotActive() already proved the piloted drone is live; re-read it into a local and
                // check again so a drone destroyed in this very frame cannot be dereferenced.
                IhaController piloted = _pilot.Controlled;
                if (piloted != null)
                {
                    if (CockpitView) Cockpit(piloted.transform);
                    else Chase(piloted.transform);
                }
                return;
            }

            // Control released (or the piloted drone is gone): leave the cockpit and give the aircraft
            // its model back before returning to the normal spectator behaviour.
            if (_pilotWasActive)
            {
                _pilotWasActive = false;
                SetCockpitView(false);
            }

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

            Chase(_followTarget.transform);
        }

        /// <summary>
        /// Shared trailing behaviour: smoothly settle behind and above <paramref name="t"/> and look at
        /// it, keeping yaw/pitch in sync so returning to free-fly doesn't snap the view.
        /// </summary>
        private void Chase(Transform t)
        {
            if (t == null) return;

            float dt = Time.unscaledDeltaTime;

            // Same trailing offset as before, but critically damped instead of a per-frame Lerp so
            // the camera never snaps when the drone jinks.
            Vector3 desired = t.position - t.forward * 15f + Vector3.up * 6f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _followVelocity,
                                                    followSmoothTime, Mathf.Infinity, dt);

            // Ease the look direction in as well (replaces the hard LookAt snap).
            Vector3 look = t.position - transform.position;
            if (look.sqrMagnitude > 1e-6f)
            {
                Quaternion want = Quaternion.LookRotation(look.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want,
                                                      Mathf.Clamp01(rotationLerp * dt));
            }

            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(euler.x);
            _yaw = euler.y;
        }

        /// <summary>
        /// Sits the camera at the piloted aircraft's nose, looking down its boresight. Follows the
        /// airframe far more tightly than <see cref="Chase"/> so it feels rigidly attached.
        /// </summary>
        private void Cockpit(Transform t)
        {
            if (t == null)
            {
                SetCockpitView(false);
                return;
            }

            float dt = Time.unscaledDeltaTime;

            // Looking out of the canopy, not at the inside of the fuselage.
            HideOwnModel(t);

            // Direction vectors, not TransformPoint: unit roots carry a non-uniform scale that the
            // "Model" child cancels, and it would distort a local-space offset.
            Vector3 desired = t.position + t.forward * cockpitForward + t.up * cockpitUp;
            transform.position = Vector3.Lerp(transform.position, desired,
                                              Mathf.Clamp01(cockpitPositionLerp * dt));

            if (t.forward.sqrMagnitude > 1e-6f)
            {
                Quaternion want = Quaternion.LookRotation(t.forward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want,
                                                      Mathf.Clamp01(cockpitRotationLerp * dt));
            }

            // Keep the free-fly state and the chase cam's damper in step so leaving the cockpit
            // (V, or releasing control) neither snaps the view nor flings the camera.
            Vector3 euler = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(euler.x);
            _yaw = euler.y;
            _followVelocity = Vector3.zero;
        }

        /// <summary>Enters/leaves the cockpit, restoring the aircraft's model on the way out.</summary>
        private void SetCockpitView(bool on)
        {
            CockpitView = on;
            if (!on) RestoreHiddenModel();
        }

        /// <summary>
        /// Disables the renderers of the given aircraft's "Model" subtree, restoring whatever was
        /// hidden before (the pilot can switch aircraft with Tab while flying).
        /// </summary>
        private void HideOwnModel(Transform aircraft)
        {
            // Unity's overloaded == is what makes this safe: a DESTROYED transform compares equal to
            // null, so a dead owner never matches a live aircraft and the stale entry is dropped.
            if (aircraft == null)
            {
                RestoreHiddenModel();
                return;
            }
            if (_hiddenModelOwner == aircraft) return;

            RestoreHiddenModel();

            Transform model = aircraft.Find("Model");
            if (model == null) return;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.enabled = false;
            }

            _hiddenRenderers = renderers;
            _hiddenModelOwner = aircraft;
        }

        /// <summary>
        /// Re-enables any renderers this rig hid. Every entry is null-checked because the aircraft may
        /// have been destroyed (shot down, or torn down by a rebuild) while the player was inside it.
        /// </summary>
        private void RestoreHiddenModel()
        {
            if (_hiddenRenderers != null)
            {
                for (int i = 0; i < _hiddenRenderers.Length; i++)
                {
                    Renderer r = _hiddenRenderers[i];
                    if (r == null) continue;
                    r.enabled = true;
                }
            }

            _hiddenRenderers = null;
            _hiddenModelOwner = null;
        }

        /// <summary>
        /// True when a <see cref="PlayerDroneController"/> is currently flying a drone. The lookup is
        /// cached and only retried every couple of seconds, so a scene without one costs nothing.
        /// </summary>
        private bool PilotActive()
        {
            if (_pilot == null)
            {
                _pilotSearchTimer -= Time.unscaledDeltaTime;
                if (_pilotSearchTimer <= 0f)
                {
                    _pilotSearchTimer = PilotSearchInterval;
                    _pilot = FindAnyObjectByType<PlayerDroneController>();
                }
            }
            return _pilot != null && _pilot.IsActive && _pilot.Controlled != null;
        }

        /// <summary>Advances to the next friendly drone (Faction == 0) in the registry.</summary>
        private void CycleFollowTarget()
        {
            // Drop dead entries first, then still null-check each one: a destroyed Targetable compares
            // equal to null but reading .Faction off it throws.
            TargetRegistry.Prune();

            var friendlies = new List<Targetable>();
            var all = TargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                Targetable t = all[i];
                if (t == null) continue;
                if (t.Faction == 0) friendlies.Add(t);
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
