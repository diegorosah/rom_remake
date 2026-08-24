using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Independent deterministic random stream used exclusively for encounter rolls and selection.</summary>
    public interface IEncounterRandomSource
    {
        int NextInt(int exclusiveUpperBound);
    }

    /// <summary>Small xorshift source that intentionally does not share NPC wander state.</summary>
    public sealed class DeterministicEncounterRandomSource : IEncounterRandomSource
    {
        private uint state;

        public DeterministicEncounterRandomSource(uint seed)
        {
            state = seed == 0 ? 0xA341316Cu : seed;
        }

        public int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return (int)(value % (uint)exclusiveUpperBound);
        }
    }

    /// <summary>One weighted creature candidate with an inclusive level range.</summary>
    public sealed class EncounterSlotDefinition
    {
        public EncounterSlotDefinition(string creatureKey, int weight, int minimumLevel, int maximumLevel)
        {
            if (string.IsNullOrWhiteSpace(creatureKey))
            {
                throw new ArgumentException("Creature key is required.", nameof(creatureKey));
            }

            if (weight <= 0 || minimumLevel <= 0 || maximumLevel < minimumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            CreatureKey = creatureKey;
            Weight = weight;
            MinimumLevel = minimumLevel;
            MaximumLevel = maximumLevel;
        }

        public string CreatureKey { get; }
        public int Weight { get; }
        public int MinimumLevel { get; }
        public int MaximumLevel { get; }
    }

    /// <summary>Weighted, declarative encounter table. Chance uses a deterministic 0..9999 roll.</summary>
    public sealed class EncounterTableDefinition
    {
        private readonly EncounterSlotDefinition[] slots;
        private readonly int totalWeight;

        public EncounterTableDefinition(string tableId, int chancePerTenThousand, IList<EncounterSlotDefinition> configuredSlots)
        {
            if (string.IsNullOrWhiteSpace(tableId))
            {
                throw new ArgumentException("Table ID is required.", nameof(tableId));
            }

            if (chancePerTenThousand < 0 || chancePerTenThousand > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(chancePerTenThousand));
            }

            if (configuredSlots == null || configuredSlots.Count == 0)
            {
                throw new ArgumentException("At least one encounter slot is required.", nameof(configuredSlots));
            }

            long weight = 0;
            slots = new EncounterSlotDefinition[configuredSlots.Count];
            for (int index = 0; index < configuredSlots.Count; index++)
            {
                EncounterSlotDefinition slot = configuredSlots[index] ?? throw new ArgumentException("Encounter slots cannot contain null.", nameof(configuredSlots));
                slots[index] = slot;
                weight += slot.Weight;
            }

            if (weight > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredSlots), "Total encounter weight is too large.");
            }

            TableId = tableId;
            ChancePerTenThousand = chancePerTenThousand;
            totalWeight = (int)weight;
        }

        public string TableId { get; }
        public int ChancePerTenThousand { get; }
        public int TotalWeight => totalWeight;
        public IReadOnlyList<EncounterSlotDefinition> Slots => slots;

        public bool Roll(IEncounterRandomSource random, out EncounterSelection selection)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            selection = default(EncounterSelection);
            if (ChancePerTenThousand == 0 || random.NextInt(10000) >= ChancePerTenThousand)
            {
                return false;
            }

            int weightRoll = random.NextInt(totalWeight);
            int accumulatedWeight = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                EncounterSlotDefinition slot = slots[index];
                accumulatedWeight += slot.Weight;
                if (weightRoll < accumulatedWeight)
                {
                    int level = slot.MinimumLevel + random.NextInt(slot.MaximumLevel - slot.MinimumLevel + 1);
                    selection = new EncounterSelection(TableId, slot.CreatureKey, level);
                    return true;
                }
            }

            throw new InvalidOperationException("Encounter weight selection was outside the configured table.");
        }
    }

    /// <summary>One active exploration cell pointing at a table key.</summary>
    public sealed class EncounterCellDefinition
    {
        public EncounterCellDefinition(string mapId, Vector2Int cell, byte elevation, string tableId, bool isExplorationEnabled)
        {
            if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(tableId))
            {
                throw new ArgumentException("Map and table IDs are required.");
            }

            MapId = mapId;
            Cell = cell;
            Elevation = elevation;
            TableId = tableId;
            IsExplorationEnabled = isExplorationEnabled;
        }

        public string MapId { get; }
        public Vector2Int Cell { get; }
        public byte Elevation { get; }
        public string TableId { get; }
        public bool IsExplorationEnabled { get; }
    }

    public readonly struct EncounterSelection
    {
        public EncounterSelection(string tableId, string creatureKey, int level)
        {
            TableId = tableId;
            CreatureKey = creatureKey;
            Level = level;
        }

        public string TableId { get; }
        public string CreatureKey { get; }
        public int Level { get; }
    }

    public readonly struct EncounterTrigger
    {
        public EncounterTrigger(string mapId, Vector2Int cell, byte elevation, EncounterSelection selection)
        {
            MapId = mapId;
            Cell = cell;
            Elevation = elevation;
            Selection = selection;
        }

        public string MapId { get; }
        public Vector2Int Cell { get; }
        public byte Elevation { get; }
        public EncounterSelection Selection { get; }
    }

    /// <summary>Optional debug presentation port used before a battle system exists.</summary>
    public interface IEncounterDebugView
    {
        void Present(EncounterTrigger trigger);
    }

    [Serializable]
    public sealed class EncounterSlotEntry
    {
        [SerializeField] private string creatureKey;
        [SerializeField, Min(1)] private int weight = 1;
        [SerializeField, Min(1)] private int minimumLevel = 1;
        [SerializeField, Min(1)] private int maximumLevel = 1;

        public void Configure(EncounterSlotDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            creatureKey = definition.CreatureKey;
            weight = definition.Weight;
            minimumLevel = definition.MinimumLevel;
            maximumLevel = definition.MaximumLevel;
        }

        public EncounterSlotDefinition ToDefinition()
        {
            return new EncounterSlotDefinition(creatureKey, weight, minimumLevel, maximumLevel);
        }
    }

    [Serializable]
    public sealed class EncounterTableEntry
    {
        [SerializeField] private string tableId;
        [SerializeField, Range(0, 10000)] private int chancePerTenThousand;
        [SerializeField] private List<EncounterSlotEntry> slots = new List<EncounterSlotEntry>();

        public void Configure(EncounterTableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            tableId = definition.TableId;
            chancePerTenThousand = definition.ChancePerTenThousand;
            slots = new List<EncounterSlotEntry>(definition.Slots.Count);
            for (int index = 0; index < definition.Slots.Count; index++)
            {
                var entry = new EncounterSlotEntry();
                entry.Configure(definition.Slots[index]);
                slots.Add(entry);
            }
        }

        public EncounterTableDefinition ToDefinition()
        {
            var definitions = new List<EncounterSlotDefinition>(slots.Count);
            for (int index = 0; index < slots.Count; index++) definitions.Add(slots[index].ToDefinition());
            return new EncounterTableDefinition(tableId, chancePerTenThousand, definitions);
        }
    }

    [Serializable]
    public sealed class EncounterCellEntry
    {
        [SerializeField] private string mapId;
        [SerializeField] private Vector2Int cell;
        [SerializeField] private byte elevation;
        [SerializeField] private string tableId;
        [SerializeField] private bool isExplorationEnabled = true;

        public void Configure(EncounterCellDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            mapId = definition.MapId;
            cell = definition.Cell;
            elevation = definition.Elevation;
            tableId = definition.TableId;
            isExplorationEnabled = definition.IsExplorationEnabled;
        }

        public EncounterCellDefinition ToDefinition()
        {
            return new EncounterCellDefinition(mapId, cell, elevation, tableId, isExplorationEnabled);
        }
    }

    /// <summary>Serializable stable-ID catalog for encounter tables and eligible map cells.</summary>

    /// <summary>Subscribes to completed player steps and emits valid overworld encounters.</summary>
}
