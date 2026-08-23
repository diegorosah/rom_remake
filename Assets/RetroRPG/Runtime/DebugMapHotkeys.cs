using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
    /// <summary>Generated-scene developer bridge for maps whose native connections are not parsed yet.</summary>
    public sealed class DebugMapHotkeys : MonoBehaviour
    {
        [SerializeField] private MapTransitionSystem transitions;
        [SerializeField] private string routeMapId;
        [SerializeField] private Vector2Int routeCell;
        [SerializeField] private byte routeElevation;
        [SerializeField] private string returnMapId;
        [SerializeField] private Vector2Int returnCell;
        [SerializeField] private byte returnElevation;

        public void Configure(
            MapTransitionSystem configuredTransitions,
            string configuredRouteMapId,
            Vector2Int configuredRouteCell,
            byte configuredRouteElevation,
            string configuredReturnMapId,
            Vector2Int configuredReturnCell,
            byte configuredReturnElevation)
        {
            transitions = configuredTransitions ?? throw new ArgumentNullException(nameof(configuredTransitions));
            if (string.IsNullOrWhiteSpace(configuredRouteMapId) || string.IsNullOrWhiteSpace(configuredReturnMapId)) throw new ArgumentException("Debug map IDs are required.");
            routeMapId = configuredRouteMapId;
            routeCell = configuredRouteCell;
            routeElevation = configuredRouteElevation;
            returnMapId = configuredReturnMapId;
            returnCell = configuredReturnCell;
            returnElevation = configuredReturnElevation;
        }

        public bool EnterRoute() => CanActivate() && transitions.TryActivateMapImmediately(routeMapId, routeCell, routeElevation, GridDirection.Right);
        public bool ReturnToTown() => CanActivate() && transitions.TryActivateMapImmediately(returnMapId, returnCell, returnElevation, GridDirection.Down);

        private bool CanActivate() => transitions != null && transitions.Player != null && transitions.Player.InputEnabled && !transitions.IsTransitioning;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) EnterRoute();
            if (keyboard.pKey.wasPressedThisFrame) ReturnToTown();
        }
    }
}
