using UnityEngine;

namespace Sim.Core
{
    /// <summary>Turns a finished mission into a 0..3 star rating. Pure logic.</summary>
    public static class MissionGrade
    {
        /// <summary>0 stars if not won; otherwise 3 stars minus one for any friendly loss and one for a slow mission, floored at 1.</summary>
        public static int Stars(MissionStatus status, int friendliesLost, float elapsedSeconds)
        {
            if (status != MissionStatus.Won) return 0;
            int stars = 3;
            if (friendliesLost > 0) stars--;
            if (elapsedSeconds > 120f) stars--;
            return Mathf.Clamp(stars, 1, 3);
        }
    }
}
