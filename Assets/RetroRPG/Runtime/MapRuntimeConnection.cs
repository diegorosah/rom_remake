using System;

namespace RetroRPG.Runtime
{
    public enum MapRuntimeConnectionDirection
    {
        South = 1,
        North = 2,
        West = 3,
        East = 4
    }

    /// <summary>Runtime-only cardinal edge connection between two already-loaded maps.</summary>
    [Serializable]
    public sealed class MapRuntimeConnection
    {
        public MapRuntimeConnection(MapRuntimeConnectionDirection direction, int offset, string destinationMapId)
        {
            if (direction != MapRuntimeConnectionDirection.South &&
                direction != MapRuntimeConnectionDirection.North &&
                direction != MapRuntimeConnectionDirection.West &&
                direction != MapRuntimeConnectionDirection.East)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }

            if (string.IsNullOrWhiteSpace(destinationMapId))
            {
                throw new ArgumentException("A connection destination map id is required.", nameof(destinationMapId));
            }

            this.direction = direction;
            this.offset = offset;
            this.destinationMapId = destinationMapId;
        }

        [UnityEngine.SerializeField] private MapRuntimeConnectionDirection direction;
        [UnityEngine.SerializeField] private int offset;
        [UnityEngine.SerializeField] private string destinationMapId;

        public MapRuntimeConnectionDirection Direction => direction;
        public int Offset => offset;
        public string DestinationMapId => destinationMapId;
    }
}
