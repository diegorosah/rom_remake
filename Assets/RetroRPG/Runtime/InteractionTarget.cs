using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
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
