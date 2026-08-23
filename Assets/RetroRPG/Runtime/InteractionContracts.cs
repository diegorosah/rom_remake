using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Game-agnostic identity and grid location of something the player may interact with.</summary>
    public interface IInteractionTarget
    {
        string InteractionKey { get; }
        Vector2Int InteractionCell { get; }
        byte InteractionElevation { get; }
        bool IsInteractionAvailable { get; }
    }

    /// <summary>Optional target capability used by NPCs; static props need not implement it.</summary>
    public interface IInteractionFacingTarget
    {
        void FaceInteractor(GridDirection interactorFacing);
    }

    /// <summary>
    /// Scene component for a static prop or another non-NPC interaction target. It
    /// exposes data only; dialogue selection remains in the interaction/catalog layer.
    /// </summary>
    public sealed class InteractionTarget : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private string interactionKey;
        [SerializeField] private Vector2Int interactionCell;
        [SerializeField] private byte interactionElevation;
        [SerializeField] private bool isAvailable = true;

        public string InteractionKey => interactionKey;
        public Vector2Int InteractionCell => interactionCell;
        public byte InteractionElevation => interactionElevation;
        public bool IsInteractionAvailable => isAvailable && isActiveAndEnabled;

        public void Configure(string configuredInteractionKey, Vector2Int configuredCell, byte configuredElevation, bool configuredAvailable)
        {
            if (string.IsNullOrWhiteSpace(configuredInteractionKey))
            {
                throw new ArgumentException("An interaction key is required.", nameof(configuredInteractionKey));
            }

            interactionKey = configuredInteractionKey;
            interactionCell = configuredCell;
            interactionElevation = configuredElevation;
            isAvailable = configuredAvailable;
        }

        public void SetAvailable(bool available)
        {
            isAvailable = available;
        }
    }
}
