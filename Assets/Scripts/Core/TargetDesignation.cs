using System.Collections.Generic;
using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// Numbers the visible hostiles 1, 2, 3 … and remembers which one the pilot has picked, for the
    /// fighter jet's balistik füze (see the runtime launcher). Pure logic, deterministic.
    ///
    /// <para>
    /// THE ORDERING RULE IS "ASCENDING TARGET ID", AND THAT CHOICE IS THE WHOLE POINT. The obvious
    /// alternatives — by range, or by bearing off the nose — are useless here: both are functions of
    /// where the SHOOTER is, so simply flying forward renumbers the whole picture and the number the
    /// player is aiming at moves under their thumb between one frame and the next. A
    /// <see cref="DetectableTarget.Id"/> comes from <c>TargetRegistry.NextId()</c>, is handed out once
    /// when the unit spawns and never changes, so:
    /// </para>
    /// <list type="bullet">
    ///   <item>the same set of hostiles always produces the same numbering, whatever order the
    ///         snapshot happens to list them in and wherever the shooter is;</item>
    ///   <item>a hostile keeps its number for as long as no LOWER-numbered hostile leaves the list;</item>
    ///   <item>a target dying (or dropping out of the picture) renumbers predictably: everything
    ///         after it moves down exactly one, everything before it is untouched;</item>
    ///   <item>a newly spawned hostile has the highest id, so it appears at the END of the list and
    ///         never renumbers anything already on screen.</item>
    /// </list>
    ///
    /// <para>
    /// The selection is kept by ID, not by index, so it survives the list changing shape. When the
    /// selected target disappears the designator falls back to whatever now occupies the same slot
    /// (i.e. the next hostile up), clamped to the end of the list — never to "nothing" while there is
    /// still something to shoot at, and never to a stale id.
    /// </para>
    /// </summary>
    public class TargetDesignator
    {
        /// <summary>Sort comparison, cached so <see cref="Refresh"/> allocates no delegate per call.</summary>
        private static readonly System.Comparison<DetectableTarget> ById =
            (a, b) => a.Id.CompareTo(b.Id);

        private readonly List<DetectableTarget> _ordered = new List<DetectableTarget>();
        private int _index = -1;

        /// <summary>The enumerated hostiles, in numbering order: entry <c>i</c> is number <c>i + 1</c>.</summary>
        public IReadOnlyList<DetectableTarget> Targets => _ordered;

        /// <summary>How many hostiles are currently numbered.</summary>
        public int Count => _ordered.Count;

        /// <summary>True when a live, in-list target is selected.</summary>
        public bool HasSelection => _index >= 0 && _index < _ordered.Count;

        /// <summary>Index of the selected target in <see cref="Targets"/>, or -1.</summary>
        public int SelectedIndex => HasSelection ? _index : -1;

        /// <summary>The 1-based number shown on screen for the selection, or 0 when there is none.</summary>
        public int SelectedNumber => HasSelection ? _index + 1 : 0;

        /// <summary>Stable id of the selected target, or -1 when nothing is selected.</summary>
        public int SelectedId => HasSelection ? _ordered[_index].Id : -1;

        /// <summary>The selected snapshot, or <c>default</c> when nothing is selected.</summary>
        public DetectableTarget Selected => HasSelection ? _ordered[_index] : default;

        /// <summary>
        /// Rebuilds the numbering from the current snapshot and reconciles the selection with it.
        ///
        /// <para>
        /// A null or empty snapshot clears both. Otherwise the previously selected ID is looked up
        /// first; when it is gone the OLD SLOT is reused (clamped into range), which is what makes a
        /// target dying under the crosshair hand the launcher the next hostile instead of nothing.
        /// With no previous selection at all, number 1 is selected — so arming the launcher always
        /// leaves the pilot with something designated.
        /// </para>
        /// </summary>
        public void Refresh(IReadOnlyList<DetectableTarget> snapshot)
        {
            int previousId = HasSelection ? _ordered[_index].Id : -1;
            int previousIndex = _index;

            _ordered.Clear();
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.Count; i++) _ordered.Add(snapshot[i]);
                _ordered.Sort(ById);
            }

            if (_ordered.Count == 0)
            {
                _index = -1;
                return;
            }

            if (previousId >= 0)
            {
                int found = IndexOf(previousId);
                if (found >= 0)
                {
                    _index = found;
                    return;
                }

                // The selection is gone: keep the SLOT, so the number the pilot was on now holds the
                // next hostile up (and the last slot falls back onto the new last target).
                _index = Mathf.Clamp(previousIndex, 0, _ordered.Count - 1);
                return;
            }

            // Nothing was selected (first refresh, or the list had emptied): start at number 1.
            _index = 0;
        }

        /// <summary>Steps the selection one target UP the numbering, wrapping past the last one.</summary>
        public void CycleNext()
        {
            Step(1);
        }

        /// <summary>Steps the selection one target DOWN the numbering, wrapping past number 1.</summary>
        public void CyclePrevious()
        {
            Step(-1);
        }

        /// <summary>
        /// Selects the target with the given id. Returns false and changes nothing when that id is not
        /// currently numbered, so a stale id can never leave the launcher aiming at a dead target.
        /// </summary>
        public bool Select(int id)
        {
            int found = IndexOf(id);
            if (found < 0) return false;
            _index = found;
            return true;
        }

        /// <summary>Drops the numbering and the selection (leaving the targeting mode).</summary>
        public void Clear()
        {
            _ordered.Clear();
            _index = -1;
        }

        /// <summary>
        /// The 1-based number shown for a given target id. Returns false for an id that is not in the
        /// current picture.
        /// </summary>
        public bool TryGetNumber(int id, out int number)
        {
            int found = IndexOf(id);
            number = found + 1;
            return found >= 0;
        }

        /// <summary>Position of an id in the numbering, or -1.</summary>
        private int IndexOf(int id)
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].Id == id) return i;
            }
            return -1;
        }

        /// <summary>Wrapping step used by both cycle directions. A no-op on an empty list.</summary>
        private void Step(int delta)
        {
            int n = _ordered.Count;
            if (n <= 0)
            {
                _index = -1;
                return;
            }

            // A selection stepped from "nothing" lands on number 1 rather than skipping past it.
            if (_index < 0 || _index >= n)
            {
                _index = 0;
                return;
            }

            _index = ((_index + delta) % n + n) % n;
        }
    }
}
