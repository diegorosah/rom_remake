using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Stable-ID lookup for scene-resident runtime maps.</summary>
    public sealed class RuntimeMapCatalog : MonoBehaviour
    {
        [SerializeField] private List<MapRuntimeRoot> maps = new List<MapRuntimeRoot>();

        public IReadOnlyList<MapRuntimeRoot> Maps => maps;

        public void Configure(IList<MapRuntimeRoot> configuredMaps)
        {
            if (configuredMaps == null)
            {
                throw new ArgumentNullException(nameof(configuredMaps));
            }

            maps = new List<MapRuntimeRoot>(configuredMaps);
            ValidateUniqueMaps();
        }

        public bool TryResolve(string mapId, out MapRuntimeRoot map)
        {
            map = null;
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return false;
            }

            for (int index = 0; index < maps.Count; index++)
            {
                MapRuntimeRoot candidate = maps[index];
                if (candidate != null && string.Equals(candidate.MapId, mapId, StringComparison.Ordinal))
                {
                    map = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveMap(string mapId, out MapRuntimeRoot map)
        {
            return TryResolve(mapId, out map);
        }

        public MapRuntimeRoot ResolveRequired(string mapId)
        {
            if (!TryResolve(mapId, out MapRuntimeRoot map))
            {
                throw new KeyNotFoundException("No runtime map is registered with ID '" + mapId + "'.");
            }

            return map;
        }

        public MapRuntimeRoot ResolveRequiredMap(string mapId)
        {
            return ResolveRequired(mapId);
        }

        private void OnValidate()
        {
            if (maps == null)
            {
                maps = new List<MapRuntimeRoot>();
            }
        }

        private void ValidateUniqueMaps()
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < maps.Count; index++)
            {
                MapRuntimeRoot map = maps[index];
                if (map == null || string.IsNullOrWhiteSpace(map.MapId) || !knownIds.Add(map.MapId))
                {
                    throw new ArgumentException("Runtime maps must be non-null and have unique stable IDs.", nameof(maps));
                }
            }
        }
    }
}
