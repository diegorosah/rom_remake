using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// A single cardinal direction on a cell grid. The enum deliberately has no
    /// diagonal values: one movement command always targets exactly one cell.
    /// </summary>
    public enum GridDirection
    {
        None = 0,
        Down = 1,
        Up = 2,
        Left = 3,
        Right = 4,
    }

    /// <summary>
    /// Per-cell edge blockers. A bit on either side of an edge prevents crossing it.
    /// </summary>
    [Flags]
    public enum GridDirectionMask : byte
    {
        None = 0,
        Down = 1 << 0,
        Up = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
    }

    public static class GridDirections
    {
        public static bool IsCardinal(GridDirection direction)
        {
            return direction == GridDirection.Down || direction == GridDirection.Up ||
                   direction == GridDirection.Left || direction == GridDirection.Right;
        }

        public static Vector2Int ToOffset(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.Down:
                    return Vector2Int.down;
                case GridDirection.Up:
                    return Vector2Int.up;
                case GridDirection.Left:
                    return Vector2Int.left;
                case GridDirection.Right:
                    return Vector2Int.right;
                default:
                    return Vector2Int.zero;
            }
        }

        public static GridDirection Opposite(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.Down:
                    return GridDirection.Up;
                case GridDirection.Up:
                    return GridDirection.Down;
                case GridDirection.Left:
                    return GridDirection.Right;
                case GridDirection.Right:
                    return GridDirection.Left;
                default:
                    return GridDirection.None;
            }
        }

        public static GridDirectionMask ToMask(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.Down:
                    return GridDirectionMask.Down;
                case GridDirection.Up:
                    return GridDirectionMask.Up;
                case GridDirection.Left:
                    return GridDirectionMask.Left;
                case GridDirection.Right:
                    return GridDirectionMask.Right;
                default:
                    return GridDirectionMask.None;
            }
        }
    }
}
