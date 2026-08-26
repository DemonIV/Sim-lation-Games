using UnityEngine;

namespace Sim.Core
{
    /// <summary>Lightweight snapshot of a detectable object for the targeting system.</summary>
    public struct DetectableTarget
    {
        public int Id;
        public Vector3 Position;
        public Vector3 Velocity;

        public DetectableTarget(int id, Vector3 position, Vector3 velocity = default)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
        }
    }
}
