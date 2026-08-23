using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Catalog of interaction targets belonging to one imported map root.</summary>
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
        }
    }

    /// <summary>Stable map-root lookup for interaction catalogs, independent of any dialogue UI.</summary>
    public sealed class RuntimeInteractionCatalog : MonoBehaviour
    {
        [SerializeField] private List<MapInteractionCatalog> mapCatalogs = new List<MapInteractionCatalog>();

        public IReadOnlyList<MapInteractionCatalog> MapCatalogs => mapCatalogs;

        public void Configure(IList<MapInteractionCatalog> configuredCatalogs)
        {
            if (configuredCatalogs == null)
            {
                throw new ArgumentNullException(nameof(configuredCatalogs));
            }

            mapCatalogs = new List<MapInteractionCatalog>(configuredCatalogs);
            ValidateUniqueMaps();
        }

        public bool TryResolve(MapRuntimeRoot mapRoot, out MapInteractionCatalog catalog)
        {
            catalog = null;
            if (mapRoot == null)
            {
                return false;
            }

            for (int index = 0; index < mapCatalogs.Count; index++)
            {
                MapInteractionCatalog candidate = mapCatalogs[index];
                if (candidate != null && candidate.MapRoot == mapRoot)
                {
                    catalog = candidate;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            if (mapCatalogs == null)
            {
                mapCatalogs = new List<MapInteractionCatalog>();
            }
        }

        private void ValidateUniqueMaps()
        {
            var maps = new HashSet<MapRuntimeRoot>();
            for (int index = 0; index < mapCatalogs.Count; index++)
            {
                MapInteractionCatalog catalog = mapCatalogs[index];
                if (catalog == null || catalog.MapRoot == null || !maps.Add(catalog.MapRoot))
                {
                    throw new ArgumentException("Interaction catalogs must be non-null and unique per map.", nameof(mapCatalogs));
                }
            }
        }
    }
}
