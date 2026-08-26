using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>Follows an ordered list of waypoints, advancing on arrival. Pure logic.</summary>
    public class WaypointNavigator
    {
        private readonly List<Vector3> _waypoints;
        public float ArrivalRadius = 5f;
        public bool Loop;
        public int CurrentIndex { get; private set; }
        public bool IsComplete { get; private set; }

        public WaypointNavigator(IEnumerable<Vector3> waypoints, float arrivalRadius = 5f, bool loop = false)
        {
            _waypoints = new List<Vector3>(waypoints);
            ArrivalRadius = arrivalRadius;
            Loop = loop;
            CurrentIndex = 0;
            IsComplete = _waypoints.Count == 0;
        }

        public int Count => _waypoints.Count;

        public Vector3 CurrentWaypoint =>
            (_waypoints.Count == 0 || IsComplete) ? Vector3.zero : _waypoints[CurrentIndex];

        public Vector3 DesiredDirection(Vector3 currentPosition)
        {
            if (_waypoints.Count == 0 || IsComplete) return Vector3.zero;
            Vector3 d = _waypoints[CurrentIndex] - currentPosition;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }

        /// <summary>Advances the target waypoint when within ArrivalRadius. Returns true if a waypoint was reached this call.</summary>
        public bool Update(Vector3 currentPosition)
        {
            if (_waypoints.Count == 0 || IsComplete) return false;
            float dist = Vector3.Distance(currentPosition, _waypoints[CurrentIndex]);
            if (dist <= ArrivalRadius)
            {
                if (CurrentIndex >= _waypoints.Count - 1)
                {
                    if (Loop) CurrentIndex = 0;
                    else IsComplete = true;
                }
                else CurrentIndex++;
                return true;
            }
            return false;
        }
    }
}
