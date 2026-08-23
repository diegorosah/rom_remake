using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// A configurable, bottom-up grid of collision, elevation, and directional
    /// edge data. The data arrays use <c>index = y * Width + x</c>, so index zero
    /// represents the lower-left cell of the map.
    /// </summary>
    public sealed class GridCollisionMap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int height = 1;
        [SerializeField] private byte[] collision = { 0 };
        [SerializeField] private byte[] elevation = { 0 };
        [SerializeField] private GridDirectionMask[] directionalEdges = { GridDirectionMask.None };

        public int Width => width;
        public int Height => height;
        public Rect LocalBounds => new Rect(0f, 0f, width, height);
        public Rect WorldBounds
        {
            get
            {
                Vector3 origin = transform.TransformPoint(Vector3.zero);
                Vector3 extent = transform.TransformPoint(new Vector3(width, height, 0f));
                return Rect.MinMaxRect(
                    Mathf.Min(origin.x, extent.x), Mathf.Min(origin.y, extent.y),
                    Mathf.Max(origin.x, extent.x), Mathf.Max(origin.y, extent.y));
            }
        }

        public void Configure(
            int configuredWidth,
            int configuredHeight,
            byte[] bottomUpCollision,
            byte[] bottomUpElevation,
            GridDirectionMask[] bottomUpDirectionalEdges)
        {
            ValidateConfiguration(
                configuredWidth,
                configuredHeight,
                bottomUpCollision,
                bottomUpElevation,
                bottomUpDirectionalEdges);

            width = configuredWidth;
            height = configuredHeight;
            collision = (byte[])bottomUpCollision.Clone();
            elevation = (byte[])bottomUpElevation.Clone();
            directionalEdges = (GridDirectionMask[])bottomUpDirectionalEdges.Clone();
        }

        public bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        }

        public Vector3 CellCenter(Vector2Int cell)
        {
            if (!IsInBounds(cell))
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside this collision map.");
            }

            return transform.TransformPoint(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
        }

        public byte GetCollision(Vector2Int cell)
        {
            return collision[GetRequiredIndex(cell)];
        }

        public byte GetElevation(Vector2Int cell)
        {
            return elevation[GetRequiredIndex(cell)];
        }

        public GridDirectionMask GetDirectionalEdges(Vector2Int cell)
        {
            return directionalEdges[GetRequiredIndex(cell)];
        }

        /// <summary>
        /// Tests a cardinal edge. Out-of-bounds cells, nonzero target collision,
        /// blockers on either edge side, and incompatible elevations are blocked.
        /// Elevations 0 and 15 are pass-through values and preserve the current one.
        /// </summary>
        public bool CanMove(
            Vector2Int currentCell,
            byte currentElevation,
            GridDirection direction,
            out Vector2Int targetCell,
            out byte resultingElevation)
        {
            targetCell = currentCell;
            resultingElevation = currentElevation;

            if (!GridDirections.IsCardinal(direction) || !IsInBounds(currentCell) || !HasCompleteData())
            {
                return false;
            }

            targetCell = currentCell + GridDirections.ToOffset(direction);
            if (!IsInBounds(targetCell))
            {
                return false;
            }

            int currentIndex = GetIndexUnchecked(currentCell);
            int targetIndex = GetIndexUnchecked(targetCell);
            GridDirectionMask outgoingMask = GridDirections.ToMask(direction);
            GridDirectionMask incomingMask = GridDirections.ToMask(GridDirections.Opposite(direction));
            if (collision[targetIndex] != 0 ||
                (directionalEdges[currentIndex] & outgoingMask) != 0 ||
                (directionalEdges[targetIndex] & incomingMask) != 0)
            {
                return false;
            }

            byte targetElevation = elevation[targetIndex];
            if (currentElevation != 0 && targetElevation != 0 && targetElevation != 15 && currentElevation != targetElevation)
            {
                return false;
            }

            if (targetElevation != 0 && targetElevation != 15)
            {
                resultingElevation = targetElevation;
            }

            return true;
        }

        private bool HasCompleteData()
        {
            long expectedLength = (long)width * height;
            return width > 0 && height > 0 && expectedLength <= int.MaxValue &&
                   collision != null && elevation != null && directionalEdges != null &&
                   collision.Length == expectedLength && elevation.Length == expectedLength &&
                   directionalEdges.Length == expectedLength;
        }

        private int GetRequiredIndex(Vector2Int cell)
        {
            if (!IsInBounds(cell) || !HasCompleteData())
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell data is unavailable or outside this collision map.");
            }

            return GetIndexUnchecked(cell);
        }

        private int GetIndexUnchecked(Vector2Int cell)
        {
            return cell.y * width + cell.x;
        }

        private static void ValidateConfiguration(
            int configuredWidth,
            int configuredHeight,
            byte[] configuredCollision,
            byte[] configuredElevation,
            GridDirectionMask[] configuredDirectionalEdges)
        {
            if (configuredWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredWidth), "Width must be positive.");
            }

            if (configuredHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredHeight), "Height must be positive.");
            }

            long expectedLength = (long)configuredWidth * configuredHeight;
            if (expectedLength > int.MaxValue || configuredCollision == null || configuredElevation == null ||
                configuredDirectionalEdges == null || configuredCollision.Length != expectedLength ||
                configuredElevation.Length != expectedLength || configuredDirectionalEdges.Length != expectedLength)
            {
                throw new ArgumentException("Collision, elevation, and edge arrays must all match width * height.");
            }
        }
    }
}
