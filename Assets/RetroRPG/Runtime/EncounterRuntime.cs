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

    /// <summary>Subscribes to completed player steps and emits valid overworld encounters.</summary>
    public sealed class EncounterSystem : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private MapTransitionSystem mapTransitions;
        [SerializeField] private RuntimeMapCatalog mapCatalog;
        [SerializeField] private RuntimeEncounterCatalog encounterCatalog;
        [SerializeField] private DialogueController dialogueController;
        [SerializeField] private MonoBehaviour debugViewComponent;
        [SerializeField] private uint deterministicSeed = 1u;

        private IEncounterDebugView debugView;
        private IEncounterRandomSource random;
        private bool isExplorationBlocked;
        private bool isSubscribed;

        public event Action<EncounterTrigger> EncounterTriggered;
        public IEncounterRandomSource RandomSource => random;
        public bool IsExplorationBlocked => isExplorationBlocked;

        public void Configure(
            PlayerController configuredPlayer,
            MapTransitionSystem configuredMapTransitions,
            RuntimeMapCatalog configuredMapCatalog,
            RuntimeEncounterCatalog configuredEncounterCatalog,
            DialogueController configuredDialogueController = null,
            IEncounterRandomSource configuredRandom = null,
            IEncounterDebugView configuredDebugView = null)
        {
            Unsubscribe();
            player = configuredPlayer ?? throw new ArgumentNullException(nameof(configuredPlayer));
            mapTransitions = configuredMapTransitions;
            mapCatalog = configuredMapCatalog;
            encounterCatalog = configuredEncounterCatalog ?? throw new ArgumentNullException(nameof(configuredEncounterCatalog));
            dialogueController = configuredDialogueController;
            random = configuredRandom ?? new DeterministicEncounterRandomSource(deterministicSeed);
            debugView = configuredDebugView;
            debugViewComponent = configuredDebugView as MonoBehaviour;
            if (isActiveAndEnabled) Subscribe();
        }

        public void SetRandomSource(IEncounterRandomSource configuredRandom)
        {
            random = configuredRandom ?? throw new ArgumentNullException(nameof(configuredRandom));
        }

        public void SetExplorationBlocked(bool blocked)
        {
            isExplorationBlocked = blocked;
        }

        public void SetDebugViewComponent(MonoBehaviour configuredDebugViewComponent)
        {
            if (configuredDebugViewComponent != null && !(configuredDebugViewComponent is IEncounterDebugView)) throw new ArgumentException("Debug view must implement IEncounterDebugView.", nameof(configuredDebugViewComponent));
            debugViewComponent = configuredDebugViewComponent;
            debugView = configuredDebugViewComponent as IEncounterDebugView;
        }

        private void Awake()
        {
            debugView = debugViewComponent as IEncounterDebugView;
            if (random == null) random = new DeterministicEncounterRandomSource(deterministicSeed);
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnPlayerMovementCompleted(PlayerController movingPlayer)
        {
            if (isExplorationBlocked || movingPlayer != player || player == null || encounterCatalog == null || random == null ||
                (mapTransitions != null && mapTransitions.IsTransitioning) ||
                (dialogueController != null && dialogueController.IsOpen))
            {
                return;
            }

            MapRuntimeRoot activeMap = ResolveActiveMap();
            if (activeMap == null || !activeMap.IsRuntimeActive ||
                !encounterCatalog.TryResolve(activeMap.MapId, player.CurrentCell, player.Elevation, out _, out EncounterTableDefinition table) ||
                !table.Roll(random, out EncounterSelection selection))
            {
                return;
            }

            var trigger = new EncounterTrigger(activeMap.MapId, player.CurrentCell, player.Elevation, selection);
            EncounterTriggered?.Invoke(trigger);
            debugView?.Present(trigger);
        }

        private MapRuntimeRoot ResolveActiveMap()
        {
            if (mapTransitions != null && mapTransitions.ActiveMap != null) return mapTransitions.ActiveMap;
            if (mapCatalog != null && player != null)
            {
                foreach (MapRuntimeRoot map in mapCatalog.Maps)
                {
                    if (map != null && map.IsRuntimeActive && map.CollisionMap == player.CollisionMap) return map;
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (!isSubscribed && player != null)
            {
                player.MovementCompleted += OnPlayerMovementCompleted;
                isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (isSubscribed && player != null) player.MovementCompleted -= OnPlayerMovementCompleted;
            isSubscribed = false;
        }
    }
}
