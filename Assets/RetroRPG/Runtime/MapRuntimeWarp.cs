using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>How a map-local warp is considered for a player movement request.</summary>
    public enum MapRuntimeWarpActivation
    {
        /// <summary>The ROM record is retained for identity/arrival but cannot activate.</summary>
        Inactive = 0,
        /// <summary>A door in the adjacent cell, normally entered by an Up request.</summary>
        AdjacentAttempt = 1,
        /// <summary>An arrow or stair under the player, activated in its configured direction.</summary>
        CurrentCellDirection = 2,
    }

    /// <summary>
    /// Game-agnostic runtime representation of one already-imported warp. It contains
    /// no ROM offsets or IR references and is safe to serialize in a generated scene.
    /// </summary>
    [Serializable]
    public sealed class MapRuntimeWarp
    {
        [SerializeField] private string warpId;
        [SerializeField] private MapRuntimeWarpActivation activation;
        [SerializeField] private Vector2Int activationCell;
        [SerializeField] private GridDirection activationDirection = GridDirection.Up;
        [SerializeField] private string destinationMapId;
        [SerializeField] private string destinationWarpId;
        [SerializeField] private Vector2Int arrivalCell;
        [SerializeField] private byte arrivalElevation;
        [SerializeField] private GridDirection arrivalFacing = GridDirection.Down;

        public string WarpId => warpId;
        public MapRuntimeWarpActivation Activation => activation;
        public Vector2Int ActivationCell => activationCell;
        public GridDirection ActivationDirection => activationDirection;
        public string DestinationMapId => destinationMapId;
        public string DestinationWarpId => destinationWarpId;
        public Vector2Int ArrivalCell => arrivalCell;
        public byte ArrivalElevation => arrivalElevation;
        public GridDirection ArrivalFacing => arrivalFacing;

        public void Configure(
            string configuredWarpId,
            MapRuntimeWarpActivation configuredActivation,
            Vector2Int configuredActivationCell,
            GridDirection configuredActivationDirection,
            string configuredDestinationMapId,
            string configuredDestinationWarpId,
            Vector2Int configuredArrivalCell,
            byte configuredArrivalElevation,
            GridDirection configuredArrivalFacing)
        {
            if (string.IsNullOrWhiteSpace(configuredWarpId))
            {
                throw new ArgumentException("Warp ID is required.", nameof(configuredWarpId));
            }

            if (!GridDirections.IsCardinal(configuredActivationDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredActivationDirection), "Warp activation direction must be cardinal.");
            }

            if (string.IsNullOrWhiteSpace(configuredDestinationMapId))
            {
                throw new ArgumentException("Destination map ID is required.", nameof(configuredDestinationMapId));
            }

            if (string.IsNullOrWhiteSpace(configuredDestinationWarpId))
            {
                throw new ArgumentException("Destination warp ID is required.", nameof(configuredDestinationWarpId));
            }

            if (!GridDirections.IsCardinal(configuredArrivalFacing))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredArrivalFacing), "Warp arrival facing must be cardinal.");
            }

            warpId = configuredWarpId;
            activation = configuredActivation;
            activationCell = configuredActivationCell;
            activationDirection = configuredActivationDirection;
            destinationMapId = configuredDestinationMapId;
            destinationWarpId = configuredDestinationWarpId;
            arrivalCell = configuredArrivalCell;
            arrivalElevation = configuredArrivalElevation;
            arrivalFacing = configuredArrivalFacing;
        }

        public bool MatchesMovement(Vector2Int playerCell, GridDirection requestedDirection)
        {
            if (!GridDirections.IsCardinal(requestedDirection) || requestedDirection != activationDirection)
            {
                return false;
            }

            switch (activation)
            {
                case MapRuntimeWarpActivation.AdjacentAttempt:
                    return playerCell + GridDirections.ToOffset(requestedDirection) == activationCell;
                case MapRuntimeWarpActivation.CurrentCellDirection:
                    return playerCell == activationCell;
                case MapRuntimeWarpActivation.Inactive:
                default:
                    return false;
            }
        }

        public bool HasValidIdentity()
        {
            return !string.IsNullOrWhiteSpace(warpId) && !string.IsNullOrWhiteSpace(destinationMapId) &&
                   !string.IsNullOrWhiteSpace(destinationWarpId) &&
                   GridDirections.IsCardinal(activationDirection) && GridDirections.IsCardinal(arrivalFacing);
        }
    }
}
