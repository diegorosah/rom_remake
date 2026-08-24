using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
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
