using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    /// <summary>Stable top-down map-cell coordinate: X grows east and Y grows south.</summary>
    [Serializable]
    public struct MapCellCoordinate : IEquatable<MapCellCoordinate>
    {
        public MapCellCoordinate(int x, int y)
        {
            if (x < 0 || y < 0) throw new ArgumentOutOfRangeException();
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(MapCellCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is MapCellCoordinate && Equals((MapCellCoordinate)obj);
        public override int GetHashCode() => (X * 397) ^ Y;
    }

    public enum EncounterMethod
    {
        Land,
        Water,
        RockSmash,
        Fishing
    }

    /// <summary>A method-specific collection of map cells, stored in top-down (Y, X) order.</summary>
    [Serializable]
    public sealed class EncounterZoneDefinition
    {
        public EncounterZoneDefinition(string id, string mapId, EncounterMethod method, IList<MapCellCoordinate> cells)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("Encounter zone and map ids are required.");
            if (method != EncounterMethod.Land && method != EncounterMethod.Water && method != EncounterMethod.RockSmash && method != EncounterMethod.Fishing) throw new ArgumentOutOfRangeException(nameof(method));
            if (cells == null || cells.Count == 0) throw new ArgumentException("An encounter zone needs at least one cell.", nameof(cells));

            var copied = new List<MapCellCoordinate>(cells);
            copied.Sort(CompareTopDown);
            for (var i = 1; i < copied.Count; i++)
            {
                if (copied[i - 1].Equals(copied[i])) throw new ArgumentException("Encounter-zone cells must be unique.", nameof(cells));
            }

            Id = id;
            MapId = mapId;
            Method = method;
            Cells = new ReadOnlyCollection<MapCellCoordinate>(copied);
        }

        public string Id { get; }
        public string MapId { get; }
        public EncounterMethod Method { get; }
        public IReadOnlyList<MapCellCoordinate> Cells { get; }

        private static int CompareTopDown(MapCellCoordinate left, MapCellCoordinate right)
        {
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }
    }

    [Serializable]
    public sealed class EncounterWeightedEntryDefinition
    {
        public EncounterWeightedEntryDefinition(int slotIndex, int weight, int speciesId, int minimumLevel, int maximumLevel)
        {
            if (slotIndex < 0 || weight <= 0 || speciesId <= 0 || minimumLevel <= 0 || maximumLevel < minimumLevel) throw new ArgumentOutOfRangeException();
            SlotIndex = slotIndex;
            Weight = weight;
            SpeciesId = speciesId;
            MinimumLevel = minimumLevel;
            MaximumLevel = maximumLevel;
        }

        public int SlotIndex { get; }
        public int Weight { get; }
        public int SpeciesId { get; }
        public int MinimumLevel { get; }
        public int MaximumLevel { get; }
    }

    /// <summary>Declarative encounter rate and weighted species/level entries for one map method.</summary>
    [Serializable]
    public sealed class EncounterTableDefinition
    {
        public EncounterTableDefinition(string id, string mapId, EncounterMethod method, int baseRate, IList<EncounterWeightedEntryDefinition> entries)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("Encounter table and map ids are required.");
            if (method != EncounterMethod.Land && method != EncounterMethod.Water && method != EncounterMethod.RockSmash && method != EncounterMethod.Fishing) throw new ArgumentOutOfRangeException(nameof(method));
            if (baseRate < 0 || baseRate > 255) throw new ArgumentOutOfRangeException(nameof(baseRate));
            if (entries == null || entries.Count == 0) throw new ArgumentException("An encounter table needs entries.", nameof(entries));

            var copied = new List<EncounterWeightedEntryDefinition>(entries.Count);
            var slots = new HashSet<int>();
            var totalWeight = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i] ?? throw new ArgumentException("Encounter entries cannot contain null.", nameof(entries));
                if (!slots.Add(entry.SlotIndex)) throw new ArgumentException("Encounter slot indexes must be unique.", nameof(entries));
                totalWeight = checked(totalWeight + entry.Weight);
                copied.Add(entry);
            }

            copied.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            Id = id;
            MapId = mapId;
            Method = method;
            BaseRate = baseRate;
            TotalWeight = totalWeight;
            Entries = new ReadOnlyCollection<EncounterWeightedEntryDefinition>(copied);
        }

        public string Id { get; }
        public string MapId { get; }
        public EncounterMethod Method { get; }
        public int BaseRate { get; }
        public int TotalWeight { get; }
        public IReadOnlyList<EncounterWeightedEntryDefinition> Entries { get; }
    }

    [Serializable]
    public sealed class EncounterCatalogDefinition
    {
        private readonly Dictionary<string, EncounterZoneDefinition> zonesById;
        private readonly Dictionary<string, EncounterTableDefinition> tablesById;

        public EncounterCatalogDefinition(IList<EncounterZoneDefinition> zones, IList<EncounterTableDefinition> tables)
        {
            if (zones == null || tables == null) throw new ArgumentNullException(zones == null ? nameof(zones) : nameof(tables));
            zonesById = new Dictionary<string, EncounterZoneDefinition>(StringComparer.Ordinal);
            tablesById = new Dictionary<string, EncounterTableDefinition>(StringComparer.Ordinal);
            var copiedZones = new List<EncounterZoneDefinition>(zones.Count);
            var copiedTables = new List<EncounterTableDefinition>(tables.Count);
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i] ?? throw new ArgumentException("Encounter zones cannot contain null.", nameof(zones));
                if (zonesById.ContainsKey(zone.Id)) throw new ArgumentException("Encounter zone ids must be unique.", nameof(zones));
                zonesById.Add(zone.Id, zone);
                copiedZones.Add(zone);
            }
            for (var i = 0; i < tables.Count; i++)
            {
                var table = tables[i] ?? throw new ArgumentException("Encounter tables cannot contain null.", nameof(tables));
                if (tablesById.ContainsKey(table.Id)) throw new ArgumentException("Encounter table ids must be unique.", nameof(tables));
                tablesById.Add(table.Id, table);
                copiedTables.Add(table);
            }

            copiedZones.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            copiedTables.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            Zones = new ReadOnlyCollection<EncounterZoneDefinition>(copiedZones);
            Tables = new ReadOnlyCollection<EncounterTableDefinition>(copiedTables);
        }

        public IReadOnlyList<EncounterZoneDefinition> Zones { get; }
        public IReadOnlyList<EncounterTableDefinition> Tables { get; }
        public bool TryGetZone(string id, out EncounterZoneDefinition zone) => zonesById.TryGetValue(id, out zone);
        public bool TryGetTable(string id, out EncounterTableDefinition table) => tablesById.TryGetValue(id, out table);
    }
}
