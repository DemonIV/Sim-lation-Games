using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>
    /// The balistik füze's target numbering and selection: a STABLE 1..N ordering that does not move
    /// under the shooter, wrapping cycle keys, and sane fallback when the selected target dies.
    /// </summary>
    public class TargetDesignationTests
    {
        private static List<DetectableTarget> Snapshot(params int[] ids)
        {
            var list = new List<DetectableTarget>();
            for (int i = 0; i < ids.Length; i++)
                list.Add(new DetectableTarget(ids[i], new Vector3(ids[i] * 10f, 20f, ids[i] * 5f)));
            return list;
        }

        // ---------------------------------------------------------------- ordering

        [Test]
        public void Numbering_IsAscendingById_WhateverOrderTheSnapshotArrivesIn()
        {
            var a = new TargetDesignator();
            var b = new TargetDesignator();

            a.Refresh(Snapshot(7, 2, 5));
            b.Refresh(Snapshot(5, 7, 2));

            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(3, b.Count);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(a.Targets[i].Id, b.Targets[i].Id, $"slot {i} must be order-independent");

            Assert.AreEqual(2, a.Targets[0].Id);
            Assert.AreEqual(5, a.Targets[1].Id);
            Assert.AreEqual(7, a.Targets[2].Id);
        }

        [Test]
        public void Numbering_DoesNotMoveWhenOnlyThePositionsChange()
        {
            // The shooter flying past a formation must NOT renumber it: the rule is id-based, so the
            // same ids with completely different positions produce the same numbers.
            var d = new TargetDesignator();
            d.Refresh(Snapshot(3, 9, 4));

            Assert.IsTrue(d.TryGetNumber(3, out int firstBefore));
            Assert.IsTrue(d.TryGetNumber(4, out int secondBefore));
            Assert.IsTrue(d.TryGetNumber(9, out int thirdBefore));

            var moved = new List<DetectableTarget>
            {
                new DetectableTarget(9, new Vector3(-500f, 5f, 900f)),
                new DetectableTarget(4, new Vector3(12f, 300f, -7f)),
                new DetectableTarget(3, new Vector3(0f, 0f, 0f))
            };
            d.Refresh(moved);

            Assert.IsTrue(d.TryGetNumber(3, out int firstAfter));
            Assert.IsTrue(d.TryGetNumber(4, out int secondAfter));
            Assert.IsTrue(d.TryGetNumber(9, out int thirdAfter));

            Assert.AreEqual(firstBefore, firstAfter);
            Assert.AreEqual(secondBefore, secondAfter);
            Assert.AreEqual(thirdBefore, thirdAfter);
            Assert.AreEqual(1, firstAfter);
            Assert.AreEqual(2, secondAfter);
            Assert.AreEqual(3, thirdAfter);
        }

        [Test]
        public void NewTarget_AppearsLastAndRenumbersNothing()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(4, 6));

            // A hostile spawned later always has a HIGHER id (TargetRegistry.NextId is monotonic).
            d.Refresh(Snapshot(4, 6, 11));

            Assert.IsTrue(d.TryGetNumber(4, out int n4));
            Assert.IsTrue(d.TryGetNumber(6, out int n6));
            Assert.IsTrue(d.TryGetNumber(11, out int n11));
            Assert.AreEqual(1, n4);
            Assert.AreEqual(2, n6);
            Assert.AreEqual(3, n11);
        }

        [Test]
        public void RemovingATarget_MovesOnlyTheOnesBehindItDownByOne()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(2, 5, 8, 13));

            d.Refresh(Snapshot(2, 8, 13));   // number 2 (id 5) destroyed

            Assert.IsTrue(d.TryGetNumber(2, out int n2));
            Assert.IsTrue(d.TryGetNumber(8, out int n8));
            Assert.IsTrue(d.TryGetNumber(13, out int n13));
            Assert.AreEqual(1, n2, "targets ahead of the loss keep their number");
            Assert.AreEqual(2, n8);
            Assert.AreEqual(3, n13);
            Assert.IsFalse(d.TryGetNumber(5, out _));
        }

        [Test]
        public void EmptyOrNullSnapshot_ClearsTheNumbering()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(1, 2));
            Assert.AreEqual(2, d.Count);

            d.Refresh(new List<DetectableTarget>());
            Assert.AreEqual(0, d.Count);
            Assert.IsFalse(d.HasSelection);
            Assert.AreEqual(-1, d.SelectedId);
            Assert.AreEqual(0, d.SelectedNumber);

            d.Refresh(null);
            Assert.AreEqual(0, d.Count);
            Assert.IsFalse(d.HasSelection);
        }

        // ---------------------------------------------------------------- selection

        [Test]
        public void FirstRefresh_SelectsNumberOne()
        {
            var d = new TargetDesignator();
            Assert.IsFalse(d.HasSelection);

            d.Refresh(Snapshot(9, 4, 6));

            Assert.IsTrue(d.HasSelection);
            Assert.AreEqual(4, d.SelectedId);
            Assert.AreEqual(1, d.SelectedNumber);
            Assert.AreEqual(0, d.SelectedIndex);
        }

        [Test]
        public void CycleNextAndPrevious_WalkTheNumberingAndWrapBothWays()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(4, 6, 9));

            Assert.AreEqual(1, d.SelectedNumber);
            d.CycleNext();
            Assert.AreEqual(2, d.SelectedNumber);
            Assert.AreEqual(6, d.SelectedId);
            d.CycleNext();
            Assert.AreEqual(3, d.SelectedNumber);
            d.CycleNext();
            Assert.AreEqual(1, d.SelectedNumber, "next past the last target wraps to number 1");

            d.CyclePrevious();
            Assert.AreEqual(3, d.SelectedNumber, "previous past number 1 wraps to the last target");
            d.CyclePrevious();
            Assert.AreEqual(2, d.SelectedNumber);
        }

        [Test]
        public void Cycling_OnAnEmptyPictureIsANoOp()
        {
            var d = new TargetDesignator();
            d.CycleNext();
            d.CyclePrevious();

            Assert.IsFalse(d.HasSelection);
            Assert.AreEqual(-1, d.SelectedId);
            Assert.AreEqual(0, d.Count);
        }

        [Test]
        public void SelectionSurvivesRenumbering_WhenTheSelectedTargetIsStillAlive()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(2, 5, 8));
            d.CycleNext();
            d.CycleNext();
            Assert.AreEqual(8, d.SelectedId);
            Assert.AreEqual(3, d.SelectedNumber);

            // Number 1 dies: id 8 keeps the selection but slides down to number 2.
            d.Refresh(Snapshot(5, 8));

            Assert.AreEqual(8, d.SelectedId, "the pilot stays on the target they picked");
            Assert.AreEqual(2, d.SelectedNumber);
        }

        [Test]
        public void SelectedTargetDies_FallsBackToTheTargetThatTakesItsSlot()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(2, 5, 8));
            d.CycleNext();                      // number 2 = id 5
            Assert.AreEqual(5, d.SelectedId);

            d.Refresh(Snapshot(2, 8));          // id 5 destroyed

            Assert.IsTrue(d.HasSelection, "a dead selection must not leave the launcher aiming at nothing");
            Assert.AreEqual(8, d.SelectedId);
            Assert.AreEqual(2, d.SelectedNumber);
        }

        [Test]
        public void LastTargetDies_FallsBackToTheNewLastTarget()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(2, 5, 8));
            d.CycleNext();
            d.CycleNext();                      // number 3 = id 8
            Assert.AreEqual(8, d.SelectedId);

            d.Refresh(Snapshot(2, 5));          // the tail of the list is gone

            Assert.IsTrue(d.HasSelection);
            Assert.AreEqual(5, d.SelectedId);
            Assert.AreEqual(2, d.SelectedNumber);
        }

        [Test]
        public void PictureEmptiesThenFillsAgain_SelectsNumberOneRatherThanAStaleId()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(2, 5));
            d.CycleNext();
            Assert.AreEqual(5, d.SelectedId);

            d.Refresh(new List<DetectableTarget>());
            Assert.IsFalse(d.HasSelection);

            d.Refresh(Snapshot(11, 14));
            Assert.IsTrue(d.HasSelection);
            Assert.AreEqual(11, d.SelectedId);
            Assert.AreEqual(1, d.SelectedNumber);
        }

        [Test]
        public void Select_TakesALiveIdAndRefusesAnUnknownOne()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(3, 6, 9));

            Assert.IsTrue(d.Select(9));
            Assert.AreEqual(9, d.SelectedId);
            Assert.AreEqual(3, d.SelectedNumber);

            Assert.IsFalse(d.Select(1234), "an unknown id must change nothing");
            Assert.AreEqual(9, d.SelectedId);
        }

        [Test]
        public void Clear_DropsBothTheNumberingAndTheSelection()
        {
            var d = new TargetDesignator();
            d.Refresh(Snapshot(3, 6));
            d.Clear();

            Assert.AreEqual(0, d.Count);
            Assert.IsFalse(d.HasSelection);
            Assert.AreEqual(-1, d.SelectedId);
        }

        [Test]
        public void Numbering_IsDeterministicForTheSameInputSet()
        {
            var first = new TargetDesignator();
            var second = new TargetDesignator();

            for (int pass = 0; pass < 3; pass++)
            {
                first.Refresh(Snapshot(21, 3, 14, 8));
                second.Refresh(Snapshot(8, 14, 3, 21));
            }

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first.Targets[i].Id, second.Targets[i].Id);
            Assert.AreEqual(first.SelectedId, second.SelectedId);
        }
    }
}
