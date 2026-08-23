using NUnit.Framework;
using RetroRPG.Runtime;
using UnityEngine;

namespace RetroRPG.Tests.EditMode
{
    public sealed class GridCollisionMapTests
    {
        [Test]
        public void Configure_UsesBottomUpCellCentersAndDefensivelyCopiesArrays()
        {
            var go = new GameObject("collision-map-test");
            try
            {
                go.transform.position = new Vector3(2f, 3f, 0f);
                var map = go.AddComponent<GridCollisionMap>();
                var collision = new byte[] { 0, 1, 0, 0, 0, 0 };
                var elevation = new byte[] { 3, 4, 0, 15, 0, 7 };
                var edges = new[] { GridDirectionMask.None, GridDirectionMask.Right, GridDirectionMask.None, GridDirectionMask.None, GridDirectionMask.None, GridDirectionMask.None };
                map.Configure(3, 2, collision, elevation, edges);
                collision[0] = 1;
                elevation[0] = 15;
                edges[0] = GridDirectionMask.Left;

                Assert.That(map.Width, Is.EqualTo(3));
                Assert.That(map.Height, Is.EqualTo(2));
                Assert.That(map.CellCenter(new Vector2Int(0, 0)), Is.EqualTo(new Vector3(2.5f, 3.5f, 0f)));
                Assert.That(map.CellCenter(new Vector2Int(2, 1)), Is.EqualTo(new Vector3(4.5f, 4.5f, 0f)));
                Assert.That(map.GetCollision(new Vector2Int(0, 0)), Is.Zero);
                Assert.That(map.GetElevation(new Vector2Int(0, 0)), Is.EqualTo(3));
                Assert.That(map.GetDirectionalEdges(new Vector2Int(1, 0)), Is.EqualTo(GridDirectionMask.Right));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AccessorsAndMovement_RejectOutOfBoundsAndBlockedCells()
        {
            var go = new GameObject("collision-map-test");
            try
            {
                var map = go.AddComponent<GridCollisionMap>();
                map.Configure(2, 2, new byte[] { 0, 1, 0, 0 }, new byte[] { 3, 3, 3, 3 }, new GridDirectionMask[4]);
                Assert.That(map.IsInBounds(new Vector2Int(-1, 0)), Is.False);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => map.GetCollision(new Vector2Int(-1, 0)));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => map.CellCenter(new Vector2Int(2, 0)));

                Assert.That(map.CanMove(new Vector2Int(0, 0), 3, GridDirection.Right, out var target, out var elevation), Is.False);
                Assert.That(target, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(elevation, Is.EqualTo(3));
                Assert.That(map.CanMove(new Vector2Int(0, 0), 3, GridDirection.Down, out _, out _), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CanMove_EnforcesBothDirectionalEdgesAndElevationRules()
        {
            var go = new GameObject("collision-map-test");
            try
            {
                var map = go.AddComponent<GridCollisionMap>();
                var edges = new GridDirectionMask[6];
                edges[0] = GridDirectionMask.Right;
                edges[1] = GridDirectionMask.Left;
                map.Configure(3, 2, new byte[6], new byte[] { 3, 3, 7, 3, 15, 0 }, edges);

                Assert.That(map.CanMove(new Vector2Int(0, 0), 3, GridDirection.Right, out _, out _), Is.False, "outgoing edge");
                edges[0] = GridDirectionMask.None;
                map.Configure(3, 2, new byte[6], new byte[] { 3, 3, 7, 3, 15, 0 }, edges);
                Assert.That(map.CanMove(new Vector2Int(0, 0), 3, GridDirection.Right, out _, out _), Is.False, "incoming edge");

                edges[1] = GridDirectionMask.None;
                map.Configure(3, 2, new byte[6], new byte[] { 3, 3, 7, 3, 15, 0 }, edges);
                Assert.That(map.CanMove(new Vector2Int(0, 0), 3, GridDirection.Right, out _, out var sameElevation), Is.True);
                Assert.That(sameElevation, Is.EqualTo(3));
                Assert.That(map.CanMove(new Vector2Int(1, 0), 3, GridDirection.Right, out _, out _), Is.False, "mismatched non-pass-through elevation");
                Assert.That(map.CanMove(new Vector2Int(0, 1), 3, GridDirection.Right, out _, out var passThrough), Is.True);
                Assert.That(passThrough, Is.EqualTo(3), "elevation 15 preserves current elevation");
                Assert.That(map.CanMove(new Vector2Int(1, 1), 3, GridDirection.Right, out _, out var zeroElevation), Is.True);
                Assert.That(zeroElevation, Is.EqualTo(3), "elevation 0 preserves current elevation");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
