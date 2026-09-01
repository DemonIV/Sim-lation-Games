using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Which campaign levels the player has unlocked/completed and the best grade earned on each.
    ///
    /// <para>
    /// Rules: level 1 is ALWAYS unlocked; completing level N unlocks N+1; a completed level stays
    /// completed and can be replayed freely; the stored grade only ever improves. Every method takes
    /// a 1-based level index and REJECTS an out-of-range one (returns false / 0) instead of throwing,
    /// so a corrupt save can never crash the menu.
    /// </para>
    ///
    /// Pure logic; no Unity serialization attributes (the Runtime save layer maps to its own DTO).
    /// </summary>
    public class CampaignProgress
    {
        private readonly bool[] _completed;
        private readonly int[] _bestStars;

        /// <summary>Highest level number the player may enter (at least 1).</summary>
        public int HighestUnlocked { get; private set; }

        /// <summary>Number of levels tracked — always <see cref="CampaignLibrary.Count"/>.</summary>
        public int Count => _completed.Length;

        public CampaignProgress()
        {
            _completed = new bool[Mathf.Max(1, CampaignLibrary.Count)];
            _bestStars = new int[_completed.Length];
            HighestUnlocked = 1;
        }

        /// <summary>True when the player may fly this level.</summary>
        public bool IsUnlocked(int levelIndex)
        {
            if (!CampaignLibrary.IsValidIndex(levelIndex)) return false;
            return levelIndex <= HighestUnlocked;
        }

        /// <summary>True when this level has been cleared at least once.</summary>
        public bool IsCompleted(int levelIndex)
        {
            if (!CampaignLibrary.IsValidIndex(levelIndex)) return false;
            return _completed[levelIndex - 1];
        }

        /// <summary>Best star grade (0..3) recorded for this level; 0 for an unknown index.</summary>
        public int BestStars(int levelIndex)
        {
            if (!CampaignLibrary.IsValidIndex(levelIndex)) return 0;
            return _bestStars[levelIndex - 1];
        }

        /// <summary>
        /// Records a clear of <paramref name="levelIndex"/> with a 0..3 star grade: marks it
        /// completed, unlocks the next level and keeps the BEST grade seen. Returns false without
        /// touching anything for an out-of-range index; calling it twice is idempotent apart from the
        /// grade, which can only rise.
        /// </summary>
        public bool Complete(int levelIndex, int stars)
        {
            if (!CampaignLibrary.IsValidIndex(levelIndex)) return false;

            int i = levelIndex - 1;
            _completed[i] = true;

            int clamped = Mathf.Clamp(stars, 0, 3);
            if (clamped > _bestStars[i]) _bestStars[i] = clamped;

            int next = levelIndex + 1;
            if (CampaignLibrary.IsValidIndex(next) && next > HighestUnlocked) HighestUnlocked = next;

            return true;
        }

        /// <summary>The lowest level that has not been cleared yet, or the last level once all are done.</summary>
        public int NextUnclearedLevel()
        {
            for (int i = 0; i < _completed.Length; i++)
            {
                if (!_completed[i]) return i + 1;
            }
            return _completed.Length;
        }

        /// <summary>Sum of the best grades across the campaign — the "toplam yıldız" headline.</summary>
        public int TotalStars
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _bestStars.Length; i++) sum += _bestStars[i];
                return sum;
            }
        }

        /// <summary>
        /// Restores a saved state defensively: every value is clamped/re-derived, so a truncated,
        /// over-long or nonsense array (a hand-edited save) still yields a consistent progress
        /// object. Passing null for either array simply leaves that part at its fresh value.
        /// </summary>
        public void Restore(bool[] completed, int[] bestStars, int highestUnlocked)
        {
            for (int i = 0; i < _completed.Length; i++)
            {
                _completed[i] = completed != null && i < completed.Length && completed[i];
                int stars = bestStars != null && i < bestStars.Length ? bestStars[i] : 0;
                _bestStars[i] = Mathf.Clamp(stars, 0, 3);
            }

            // Unlocks are never trusted from the save: they are re-derived from the completions and
            // then merged with the stored value, so the two can never disagree.
            int derived = 1;
            for (int i = 0; i < _completed.Length; i++)
            {
                if (_completed[i]) derived = Mathf.Min(_completed.Length, i + 2);
            }
            HighestUnlocked = Mathf.Clamp(Mathf.Max(derived, highestUnlocked), 1, _completed.Length);
        }

        /// <summary>Copy of the completion flags, for the save layer.</summary>
        public bool[] CompletionSnapshot()
        {
            var copy = new bool[_completed.Length];
            for (int i = 0; i < _completed.Length; i++) copy[i] = _completed[i];
            return copy;
        }

        /// <summary>Copy of the best-grade array, for the save layer.</summary>
        public int[] StarsSnapshot()
        {
            var copy = new int[_bestStars.Length];
            for (int i = 0; i < _bestStars.Length; i++) copy[i] = _bestStars[i];
            return copy;
        }
    }
}
