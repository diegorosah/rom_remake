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
}
