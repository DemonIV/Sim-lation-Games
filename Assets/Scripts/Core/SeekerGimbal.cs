using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Missile seeker head on a gimbal: slews toward the target line of sight, limited by
    /// a maximum slew rate and a maximum off-boresight angle. Pure logic.
    /// </summary>
    public class SeekerGimbal
    {
        public float MaxOffBoresightDeg = 40f;
        public float MaxSlewRateDeg = 30f;   // deg/s
        public Vector3 LookDirection { get; private set; }
        public bool HasTrack { get; private set; }

        public SeekerGimbal(Vector3 initialLook)
        {
            LookDirection = initialLook.sqrMagnitude > 1e-6f ? initialLook.normalized : Vector3.forward;
        }

        /// <summary>
        /// Slew the seeker toward the desired line of sight for one step.
        /// Honors slew-rate and off-boresight limits (relative to boresight).
        /// Returns true when the seeker is pointed at the LOS and within gimbal limits.
        /// </summary>
        public bool Track(Vector3 boresight, Vector3 desiredLos, float dt)
        {
            Vector3 bs = boresight.sqrMagnitude > 1e-6f ? boresight.normalized : Vector3.forward;
            Vector3 los = desiredLos.sqrMagnitude > 1e-6f ? desiredLos.normalized : LookDirection;

            float maxRad = Mathf.Max(0f, MaxSlewRateDeg) * Mathf.Deg2Rad * dt;
            Vector3 newLook = Vector3.RotateTowards(LookDirection, los, maxRad, 0f).normalized;

            float off = Vector3.Angle(bs, newLook);
            if (off > MaxOffBoresightDeg)
            {
                newLook = Vector3.RotateTowards(bs, newLook, MaxOffBoresightDeg * Mathf.Deg2Rad, 0f).normalized;
                HasTrack = false;
            }
            else
            {
                HasTrack = Vector3.Angle(newLook, los) <= 0.5f;
            }

            LookDirection = newLook;
            return HasTrack;
        }
    }
}
