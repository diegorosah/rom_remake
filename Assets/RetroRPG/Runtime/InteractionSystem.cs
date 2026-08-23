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

        public PlayerController Player => player;
        public DialogueController DialogueController => dialogueController;

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
            if (player == null || !player.InputEnabled || player.IsMoving || dialogueController == null || dialogueController.IsOpen ||
                (mapTransitions != null && mapTransitions.IsTransitioning) ||
                interactionCatalog == null || !GridDirections.IsCardinal(player.Facing))
            {
                return false;
            }

            MapRuntimeRoot activeMap = ResolveActiveMap();
            if (activeMap == null || !activeMap.IsRuntimeActive || !interactionCatalog.TryResolve(activeMap, out MapInteractionCatalog targets))
            {
                return false;
            }

            Vector2Int targetCell = player.CurrentCell + GridDirections.ToOffset(player.Facing);
            if (!targets.TryFindAt(targetCell, player.Elevation, out IInteractionTarget target) ||
                !dialogueController.TryOpen(target.InteractionKey, activeMap))
            {
                return false;
            }

            if (dialogueController.Session != null && dialogueController.Session.Definition.FaceTarget &&
                target is IInteractionFacingTarget facingTarget)
            {
                facingTarget.FaceInteractor(player.Facing);
            }

            return true;
        }

        private void Update()
        {
            if (!readInteractionInput || dialogueController == null || dialogueController.IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
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
