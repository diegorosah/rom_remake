using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
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
