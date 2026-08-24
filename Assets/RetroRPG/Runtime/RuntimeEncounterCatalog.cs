using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    public sealed class RuntimeEncounterCatalog : MonoBehaviour
    {
        [SerializeField] private List<EncounterTableEntry> tableEntries = new List<EncounterTableEntry>();
        [SerializeField] private List<EncounterCellEntry> cellEntries = new List<EncounterCellEntry>();

        private readonly Dictionary<string, EncounterTableDefinition> tables = new Dictionary<string, EncounterTableDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<EncounterCellKey, EncounterCellDefinition> cells = new Dictionary<EncounterCellKey, EncounterCellDefinition>();

        public void Configure(IList<EncounterTableDefinition> configuredTables, IList<EncounterCellDefinition> configuredCells)
        {
            if (configuredTables == null || configuredCells == null) throw new ArgumentNullException(configuredTables == null ? nameof(configuredTables) : nameof(configuredCells));
            tableEntries = new List<EncounterTableEntry>(configuredTables.Count);
            cellEntries = new List<EncounterCellEntry>(configuredCells.Count);
            for (int index = 0; index < configuredTables.Count; index++)
            {
                var entry = new EncounterTableEntry();
                entry.Configure(configuredTables[index]);
                tableEntries.Add(entry);
            }

            for (int index = 0; index < configuredCells.Count; index++)
            {
                var entry = new EncounterCellEntry();
                entry.Configure(configuredCells[index]);
                cellEntries.Add(entry);
            }

            Rebuild();
        }

        public bool TryResolve(string mapId, Vector2Int cell, byte elevation, out EncounterCellDefinition encounterCell, out EncounterTableDefinition table)
        {
            encounterCell = null;
            table = null;
            if (string.IsNullOrWhiteSpace(mapId) || !cells.TryGetValue(new EncounterCellKey(mapId, cell, elevation), out encounterCell) ||
                !encounterCell.IsExplorationEnabled)
            {
                return false;
            }

            return tables.TryGetValue(encounterCell.TableId, out table);
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            if (tableEntries == null) tableEntries = new List<EncounterTableEntry>();
            if (cellEntries == null) cellEntries = new List<EncounterCellEntry>();
        }

        private void Rebuild()
        {
            tables.Clear();
            cells.Clear();
            for (int index = 0; index < tableEntries.Count; index++)
            {
                EncounterTableDefinition table = tableEntries[index].ToDefinition();
                if (tables.ContainsKey(table.TableId)) throw new InvalidOperationException("Encounter table IDs must be unique.");
                tables.Add(table.TableId, table);
            }

            for (int index = 0; index < cellEntries.Count; index++)
            {
                EncounterCellDefinition cell = cellEntries[index].ToDefinition();
                var key = new EncounterCellKey(cell.MapId, cell.Cell, cell.Elevation);
                if (cells.ContainsKey(key)) throw new InvalidOperationException("Encounter cells must be unique per map, cell, and elevation.");
                cells.Add(key, cell);
            }
        }

        private readonly struct EncounterCellKey : IEquatable<EncounterCellKey>
        {
            public EncounterCellKey(string mapId, Vector2Int cell, byte elevation) { MapId = mapId; Cell = cell; Elevation = elevation; }
            public string MapId { get; }
            public Vector2Int Cell { get; }
            public byte Elevation { get; }
            public bool Equals(EncounterCellKey other) => string.Equals(MapId, other.MapId, StringComparison.Ordinal) && Cell == other.Cell && Elevation == other.Elevation;
            public override bool Equals(object obj) => obj is EncounterCellKey other && Equals(other);
            public override int GetHashCode() => ((MapId == null ? 0 : StringComparer.Ordinal.GetHashCode(MapId)) * 397) ^ (Cell.GetHashCode() * 17) ^ Elevation;
        }
    }
}
