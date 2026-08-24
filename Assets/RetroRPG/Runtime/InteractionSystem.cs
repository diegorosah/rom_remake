using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// Resolves a cardinal interaction cell against the active map, then delegates
    /// keyed dialogue selection to <see cref="DialogueController"/>. It has no ROM,
    /// IR, or presentation dependency.
    /// </summary>
    public sealed class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private MapTransitionSystem mapTransitions;
        [SerializeField] private RuntimeMapCatalog mapCatalog;
        [SerializeField] private RuntimeInteractionCatalog interactionCatalog;
        [SerializeField] private DialogueController dialogueController;
        [SerializeField] private bool readInteractionInput = true;
        [SerializeField] private bool logInteractionDiagnostics = true;

        private string lastDiagnostic;

        public PlayerController Player => player;
        public DialogueController DialogueController => dialogueController;
        public string LastDiagnostic => lastDiagnostic;

        public void Configure(
            PlayerController configuredPlayer,
            MapTransitionSystem configuredMapTransitions,
            RuntimeMapCatalog configuredMapCatalog,
            RuntimeInteractionCatalog configuredInteractionCatalog,
            DialogueController configuredDialogueController)
        {
            player = configuredPlayer ?? throw new ArgumentNullException(nameof(configuredPlayer));
            mapTransitions = configuredMapTransitions;
            mapCatalog = configuredMapCatalog;
            interactionCatalog = configuredInteractionCatalog ?? throw new ArgumentNullException(nameof(configuredInteractionCatalog));
            dialogueController = configuredDialogueController ?? throw new ArgumentNullException(nameof(configuredDialogueController));
        }

        /// <summary>Attempts interaction on the cardinal cell immediately in front of the player.</summary>
        public bool TryInteract()
        {
            lastDiagnostic = null;
            if (player == null)
            {
                return Fail("PlayerController is not configured.");
            }
            if (!player.InputEnabled)
            {
                return Fail("Player input is disabled.");
            }
            if (player.IsMoving)
            {
                return Fail("Player is still moving.");
            }
            if (dialogueController == null)
            {
                return Fail("DialogueController is not configured.");
            }
            if (dialogueController.IsOpen)
            {
                return Fail("A dialogue is already open.");
            }
            if (mapTransitions != null && mapTransitions.IsTransitioning)
            {
                return Fail("A map transition is in progress.");
            }
            if (interactionCatalog == null)
            {
                return Fail("RuntimeInteractionCatalog is not configured.");
            }
            if (!GridDirections.IsCardinal(player.Facing))
            {
                return Fail("Player facing is not cardinal.");
            }

            MapRuntimeRoot activeMap = ResolveActiveMap();
            if (activeMap == null)
            {
                return Fail("No active runtime map could be resolved.");
            }
            if (!activeMap.IsRuntimeActive)
            {
                return Fail("Resolved map is not runtime-active: " + activeMap.MapId + ".");
            }
            if (!interactionCatalog.TryResolve(activeMap, out MapInteractionCatalog targets) || targets == null)
            {
                return Fail("No interaction catalog is registered for active map " + activeMap.MapId + ".");
            }

            Vector2Int targetCell = player.CurrentCell + GridDirections.ToOffset(player.Facing);
            bool usedElevationFallback = false;
            if (!targets.TryFindAt(targetCell, player.Elevation, out IInteractionTarget target))
            {
                if (!targets.TryFindAtAnyElevation(targetCell, out target))
                {
                    return Fail(
                        "No interaction target at " + targetCell + " in map " + activeMap.MapId +
                        " (player=" + player.CurrentCell + ", facing=" + player.Facing +
                        ", elevation=" + player.Elevation + ", registeredTargets=" + targets.Targets.Count + ").");
                }

                usedElevationFallback = true;
            }

            if (!dialogueController.TryOpen(target.InteractionKey, activeMap))
            {
                return Fail(
                    "Target " + target.InteractionKey + " was found at " + targetCell +
                    " but dialogue did not open: " + dialogueController.LastFailure);
            }

            if (dialogueController.Session != null && dialogueController.Session.Definition.FaceTarget &&
                target is IInteractionFacingTarget facingTarget)
            {
                facingTarget.FaceInteractor(player.Facing);
            }

            lastDiagnostic =
                "[INTERACT] opened key=" + target.InteractionKey +
                " map=" + activeMap.MapId +
                " player=" + player.CurrentCell +
                " target=" + targetCell +
                " facing=" + player.Facing +
                " elevation=" + player.Elevation +
                (usedElevationFallback ? " elevationFallback=true" : string.Empty);
            if (logInteractionDiagnostics)
            {
                Debug.Log(lastDiagnostic, this);
            }

            return true;
        }

        private bool Fail(string reason)
        {
            lastDiagnostic = "[INTERACT] " + reason;
            if (logInteractionDiagnostics)
            {
                Debug.LogWarning(lastDiagnostic, this);
            }
            return false;
        }

        private void Update()
        {
            if (!readInteractionInput || dialogueController == null || dialogueController.IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.zKey.wasPressedThisFrame ||
                 keyboard.xKey.wasPressedThisFrame ||
                 keyboard.eKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame))
            {
                TryInteract();
            }
        }

        private MapRuntimeRoot ResolveActiveMap()
        {
            if (mapTransitions != null && mapTransitions.ActiveMap != null)
            {
                return mapTransitions.ActiveMap;
            }

            if (mapCatalog != null && player != null)
            {
                foreach (MapRuntimeRoot map in mapCatalog.Maps)
                {
                    if (map != null && map.IsRuntimeActive && map.CollisionMap == player.CollisionMap)
                    {
                        return map;
                    }
                }
            }

            return null;
        }
    }
}
