using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    /// <summary>Immutable metadata for one importable map, independent of a source game's binary format.</summary>
    [Serializable]
    public sealed class MapImportDescriptorDefinition
    {
        public MapImportDescriptorDefinition(
            string id,
            string name,
            int width,
            int height,
            bool isInterior,
            IList<string> requiredMapIds,
            IList<string> externalDependencyIds)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Map descriptor id and name are required.");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            IsInterior = isInterior;
            RequiredMapIds = CopyDistinctIds(requiredMapIds, nameof(requiredMapIds));
            ExternalDependencyIds = CopyDistinctIds(externalDependencyIds, nameof(externalDependencyIds));
        }

        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsInterior { get; }
        public IReadOnlyList<string> RequiredMapIds { get; }
        public IReadOnlyList<string> ExternalDependencyIds { get; }

        private static IReadOnlyList<string> CopyDistinctIds(IList<string> ids, string parameterName)
        {
            if (ids == null) throw new ArgumentNullException(parameterName);
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            var copied = new List<string>(ids.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Dependency ids cannot be blank.", parameterName);
                if (!distinct.Add(id)) throw new ArgumentException("Dependency ids must be unique.", parameterName);
                copied.Add(id);
            }

            copied.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(copied);
        }
    }

    /// <summary>Deterministic catalog of currently supported maps and their safe import closures.</summary>
    [Serializable]
    public sealed class MapCatalogDefinition
    {
        private readonly Dictionary<string, MapImportDescriptorDefinition> mapsById;

        public MapCatalogDefinition(IList<MapImportDescriptorDefinition> maps)
        {
            if (maps == null || maps.Count == 0) throw new ArgumentException("A map catalog needs at least one descriptor.", nameof(maps));
            mapsById = new Dictionary<string, MapImportDescriptorDefinition>(StringComparer.Ordinal);
            var copied = new List<MapImportDescriptorDefinition>(maps.Count);
            for (var index = 0; index < maps.Count; index++)
            {
                var map = maps[index] ?? throw new ArgumentException("Map descriptors cannot contain null.", nameof(maps));

                if (mapsById.ContainsKey(map.Id))
                {
                    throw new ArgumentException("Map descriptor ids must be unique.", nameof(maps));
                }

                mapsById.Add(map.Id, map);
                copied.Add(map);
            }

            ValidateDependencies(copied);
            copied.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            Maps = new ReadOnlyCollection<MapImportDescriptorDefinition>(copied);
        }

        public IReadOnlyList<MapImportDescriptorDefinition> Maps { get; }

        public bool TryGetMap(string id, out MapImportDescriptorDefinition map)
        {
            map = null;

            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return mapsById.TryGetValue(id, out map);
        }

        public MapImportDescriptorDefinition GetMap(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !mapsById.TryGetValue(id, out var map)) throw new KeyNotFoundException("Map is not in this catalog: " + id);
            return map;
        }

        /// <summary>Returns selected maps and every internal dependency in stable ordinal-id order.</summary>
        public IReadOnlyList<MapImportDescriptorDefinition> ResolveDependencyClosure(IList<string> selectedMapIds)
        {
            if (selectedMapIds == null || selectedMapIds.Count == 0) throw new ArgumentException("At least one map id must be selected.", nameof(selectedMapIds));
            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < selectedMapIds.Count; index++)
            {
                var id = selectedMapIds[index];
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Selected map ids cannot be blank.", nameof(selectedMapIds));
                if (!mapsById.ContainsKey(id)) throw new ArgumentException("Selected map is not in this catalog: " + id, nameof(selectedMapIds));
                if (!selected.Add(id)) throw new ArgumentException("Selected map ids must be unique.", nameof(selectedMapIds));
            }

            var pending = new Queue<string>(selected);
            while (pending.Count > 0)
            {
                var map = mapsById[pending.Dequeue()];
                for (var dependencyIndex = 0; dependencyIndex < map.RequiredMapIds.Count; dependencyIndex++)
                {
                    var dependency = map.RequiredMapIds[dependencyIndex];
                    if (selected.Add(dependency)) pending.Enqueue(dependency);
                }
            }

            var resolved = new List<MapImportDescriptorDefinition>(selected.Count);
            foreach (var id in selected) resolved.Add(mapsById[id]);
            resolved.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            return new ReadOnlyCollection<MapImportDescriptorDefinition>(resolved);
        }

        public IReadOnlyList<string> CollectExternalDependencies(IList<MapImportDescriptorDefinition> resolvedMaps)
        {
            if (resolvedMaps == null || resolvedMaps.Count == 0) throw new ArgumentException("Resolved maps are required.", nameof(resolvedMaps));
            var external = new HashSet<string>(StringComparer.Ordinal);
            for (var mapIndex = 0; mapIndex < resolvedMaps.Count; mapIndex++)
            {
                var map = resolvedMaps[mapIndex] ?? throw new ArgumentException("Resolved maps cannot contain null.", nameof(resolvedMaps));
                if (!mapsById.ContainsKey(map.Id)) throw new ArgumentException("Resolved map is not from this catalog: " + map.Id, nameof(resolvedMaps));
                for (var dependencyIndex = 0; dependencyIndex < map.ExternalDependencyIds.Count; dependencyIndex++) external.Add(map.ExternalDependencyIds[dependencyIndex]);
            }

            var ordered = new List<string>(external);
            ordered.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(ordered);
        }

        private void ValidateDependencies(IList<MapImportDescriptorDefinition> maps)
        {
            for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var map = maps[mapIndex];
                for (var dependencyIndex = 0; dependencyIndex < map.RequiredMapIds.Count; dependencyIndex++)
                {
                    var dependency = map.RequiredMapIds[dependencyIndex];
                    if (string.Equals(map.Id, dependency, StringComparison.Ordinal) || !mapsById.ContainsKey(dependency))
                    {
                        throw new ArgumentException("Internal map dependencies must name a different catalog map.", nameof(maps));
                    }
                }

                for (var externalIndex = 0; externalIndex < map.ExternalDependencyIds.Count; externalIndex++)
                {
                    if (mapsById.ContainsKey(map.ExternalDependencyIds[externalIndex]))
                    {
                        throw new ArgumentException("External dependencies cannot name a catalog map.", nameof(maps));
                    }
                }
            }
        }
    }
}
