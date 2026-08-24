using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    public sealed class MapInteractionCatalog : MonoBehaviour
    {
        [SerializeField] private MapRuntimeRoot mapRoot;
        [SerializeField] private List<MonoBehaviour> targetComponents = new List<MonoBehaviour>();

        private readonly List<IInteractionTarget> targets = new List<IInteractionTarget>();

        public MapRuntimeRoot MapRoot => mapRoot;
        public IReadOnlyList<IInteractionTarget> Targets => targets;

        public void Configure(MapRuntimeRoot configuredMapRoot, IList<MonoBehaviour> configuredTargetComponents)
        {
            if (configuredMapRoot == null)
            {
                throw new ArgumentNullException(nameof(configuredMapRoot));
            }

            mapRoot = configuredMapRoot;
            targetComponents = configuredTargetComponents == null
                ? new List<MonoBehaviour>()
                : new List<MonoBehaviour>(configuredTargetComponents);
            RebuildTargets();
        }

        public void Register(IInteractionTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.InteractionKey))
            {
                throw new ArgumentException("A target with an interaction key is required.", nameof(target));
            }

            if (!targets.Contains(target))
            {
                targets.Add(target);
            }
        }

        public bool TryFindAt(Vector2Int cell, byte elevation, out IInteractionTarget target)
        {
            target = null;
            if (mapRoot == null || !mapRoot.IsRuntimeActive)
            {
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                IInteractionTarget candidate = targets[index];
                if (candidate != null && candidate.IsInteractionAvailable && candidate.InteractionCell == cell &&
                    candidate.InteractionElevation == elevation)
                {
                    target = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Compatibility fallback for object-event elevations that do not match the
        /// walkable map-cell elevation. Returns a target only when the cell is unambiguous.
        /// </summary>
        public bool TryFindAtAnyElevation(Vector2Int cell, out IInteractionTarget target)
        {
            target = null;
            if (mapRoot == null || !mapRoot.IsRuntimeActive)
            {
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                IInteractionTarget candidate = targets[index];
                if (candidate == null || !candidate.IsInteractionAvailable || candidate.InteractionCell != cell)
                {
                    continue;
                }

                if (target != null)
                {
                    // Two available targets on the same cell at different elevations
                    // are ambiguous; do not guess.
                    target = null;
                    return false;
                }

                target = candidate;
            }

            return target != null;
        }

        private void Awake()
        {
            if (mapRoot == null)
            {
                mapRoot = GetComponentInParent<MapRuntimeRoot>();
            }

            RebuildTargets();
        }

        private void OnValidate()
        {
            if (targetComponents == null)
            {
                targetComponents = new List<MonoBehaviour>();
            }
        }

        private void RebuildTargets()
        {
            targets.Clear();
            for (int index = 0; index < targetComponents.Count; index++)
            {
                IInteractionTarget target = targetComponents[index] as IInteractionTarget;
                if (target != null)
                {
                    Register(target);
                }
            }

            // Generated scene references can be rebuilt after scripts move files or
            // Unity reserializes the scene. Discovering live targets from the map
            // hierarchy makes interaction resilient without relying solely on the
            // serialized MonoBehaviour list.
            if (mapRoot != null)
            {
                MonoBehaviour[] discovered = mapRoot.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < discovered.Length; index++)
                {
                    IInteractionTarget target = discovered[index] as IInteractionTarget;
                    if (target != null && !string.IsNullOrWhiteSpace(target.InteractionKey))
                    {
                        Register(target);
                    }
                }
            }
        }
    }
}
