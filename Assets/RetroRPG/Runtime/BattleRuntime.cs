using System;
using System.Collections.Generic;
using RetroRPG.Core;
using UnityEngine;

namespace RetroRPG.Runtime
{
    [Serializable]
    public sealed class SkillSpecEntry
    {
        [SerializeField] private string key;
        [SerializeField, Min(1)] private int power = 1;

        public void Configure(SkillSpec definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            key = definition.Key; power = definition.Power;
        }

        public SkillSpec ToDefinition() => new SkillSpec(key, power);
    }

    [Serializable]
    public sealed class CreatureSpecEntry
    {
        [SerializeField] private string key;
        [SerializeField, Min(1)] private int hitPoints = 1;
        [SerializeField, Min(1)] private int attack = 1;
        [SerializeField, Min(1)] private int defense = 1;
        [SerializeField, Min(1)] private int speed = 1;
        [SerializeField] private List<string> skillKeys = new List<string>();

        public void Configure(CreatureSpec definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            key = definition.Key;
            hitPoints = definition.BaseStats.HitPoints; attack = definition.BaseStats.Attack;
            defense = definition.BaseStats.Defense; speed = definition.BaseStats.Speed;
            skillKeys = new List<string>(definition.SkillKeys);
        }

        public CreatureSpec ToDefinition()
        {
            return new CreatureSpec(key, new BattleStats(hitPoints, attack, defense, speed), skillKeys);
        }
    }

    /// <summary>Serializable MonoBehaviour adapter for the pure Core battle-content port.</summary>
    public sealed class RuntimeBattleContentCatalog : MonoBehaviour, IBattleContentCatalog
    {
        [SerializeField] private List<CreatureSpecEntry> creatureEntries = new List<CreatureSpecEntry>();
        [SerializeField] private List<SkillSpecEntry> skillEntries = new List<SkillSpecEntry>();

        private readonly Dictionary<string, CreatureSpec> creatures = new Dictionary<string, CreatureSpec>(StringComparer.Ordinal);
        private readonly Dictionary<string, SkillSpec> skills = new Dictionary<string, SkillSpec>(StringComparer.Ordinal);

        public void Configure(IList<CreatureSpec> configuredCreatures, IList<SkillSpec> configuredSkills)
        {
            if (configuredCreatures == null || configuredSkills == null)
            {
                throw new ArgumentNullException(configuredCreatures == null ? nameof(configuredCreatures) : nameof(configuredSkills));
            }

            creatureEntries = new List<CreatureSpecEntry>(configuredCreatures.Count);
            for (int index = 0; index < configuredCreatures.Count; index++)
            {
                var entry = new CreatureSpecEntry(); entry.Configure(configuredCreatures[index]); creatureEntries.Add(entry);
            }

            skillEntries = new List<SkillSpecEntry>(configuredSkills.Count);
            for (int index = 0; index < configuredSkills.Count; index++)
            {
                var entry = new SkillSpecEntry(); entry.Configure(configuredSkills[index]); skillEntries.Add(entry);
            }

            Rebuild();
        }

        public bool TryResolveCreature(string creatureKey, out CreatureSpec creature)
        {
            return !string.IsNullOrWhiteSpace(creatureKey) && creatures.TryGetValue(creatureKey, out creature);
        }

        public bool TryResolveSkill(string skillKey, out SkillSpec skill)
        {
            return !string.IsNullOrWhiteSpace(skillKey) && skills.TryGetValue(skillKey, out skill);
        }

        private void Awake() { Rebuild(); }

        private void OnValidate()
        {
            if (creatureEntries == null) creatureEntries = new List<CreatureSpecEntry>();
            if (skillEntries == null) skillEntries = new List<SkillSpecEntry>();
        }

        private void Rebuild()
        {
            creatures.Clear(); skills.Clear();
            for (int index = 0; index < creatureEntries.Count; index++)
            {
                CreatureSpec creature = creatureEntries[index].ToDefinition();
                if (creatures.ContainsKey(creature.Key)) throw new InvalidOperationException("Creature keys must be unique.");
                creatures.Add(creature.Key, creature);
            }

            for (int index = 0; index < skillEntries.Count; index++)
            {
                SkillSpec skill = skillEntries[index].ToDefinition();
                if (skills.ContainsKey(skill.Key)) throw new InvalidOperationException("Skill keys must be unique.");
                skills.Add(skill.Key, skill);
            }

            foreach (CreatureSpec creature in creatures.Values)
            {
                for (int index = 0; index < creature.SkillKeys.Count; index++)
                {
                    if (!skills.ContainsKey(creature.SkillKeys[index]))
                    {
                        throw new InvalidOperationException("Every creature skill key must exist in the battle catalog.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Connects encounter events to a pure one-versus-one battle state. A concrete
    /// battle view can submit the player's sole attack and render state through IBattleView.
    /// </summary>
    public sealed class BattleCoordinator : MonoBehaviour
    {
        [SerializeField] private EncounterSystem encounterSystem;
        [SerializeField] private PlayerController player;
        [SerializeField] private MapTransitionSystem mapTransitions;
        [SerializeField] private RuntimeBattleContentCatalog contentCatalog;
        [SerializeField] private MonoBehaviour battleViewComponent;
        [SerializeField] private string partyCreatureKey;
        [SerializeField, Range(1, 100)] private int partyLevel = 5;
        [SerializeField] private int partyCurrentHitPoints;
        [SerializeField] private bool hasPartyState;

        private IBattleView view;
        private BattleState state;
        private NpcSimulationDriver suspendedDriver;
        private bool priorPlayerInputEnabled;
        private bool priorExplorationBlocked;
        private bool priorDriverSuspended;
        private bool overworldLocked;
        private bool isSubscribed;

        public BattleState State => state;
        public bool IsBattleActive => state != null && !state.IsComplete;
        public bool IsAwaitingReturn => state != null && state.IsComplete;
        public int PartyCurrentHitPoints => partyCurrentHitPoints;

        public void Configure(
            EncounterSystem configuredEncounterSystem,
            PlayerController configuredPlayer,
            MapTransitionSystem configuredMapTransitions,
            RuntimeBattleContentCatalog configuredContentCatalog,
            IBattleView configuredView = null)
        {
            Unsubscribe();
            encounterSystem = configuredEncounterSystem ?? throw new ArgumentNullException(nameof(configuredEncounterSystem));
            player = configuredPlayer ?? throw new ArgumentNullException(nameof(configuredPlayer));
            mapTransitions = configuredMapTransitions;
            contentCatalog = configuredContentCatalog ?? throw new ArgumentNullException(nameof(configuredContentCatalog));
            view = configuredView;
            battleViewComponent = configuredView as MonoBehaviour;
            if (isActiveAndEnabled) Subscribe();
        }

        public void ConfigureParty(string creatureKey, int level, int currentHitPoints)
        {
            if (string.IsNullOrWhiteSpace(creatureKey)) throw new ArgumentException("Party creature key is required.", nameof(creatureKey));
            if (level <= 0 || level > 100) throw new ArgumentOutOfRangeException(nameof(level));
            if (currentHitPoints < 0) throw new ArgumentOutOfRangeException(nameof(currentHitPoints));

            partyCreatureKey = creatureKey; partyLevel = level; partyCurrentHitPoints = currentHitPoints; hasPartyState = true;
        }

        public void SetViewComponent(MonoBehaviour configuredViewComponent)
        {
            if (configuredViewComponent != null && !(configuredViewComponent is IBattleView)) throw new ArgumentException("Battle view must implement IBattleView.", nameof(configuredViewComponent));
            battleViewComponent = configuredViewComponent;
            view = configuredViewComponent as IBattleView;
            if (state != null) view?.PresentBattle(state);
        }

        public bool TrySubmitPlayerAttack(string skillKey)
        {
            if (!IsBattleActive || string.IsNullOrWhiteSpace(skillKey)) return false;
            string enemySkill = state.Opponent.Spec.SkillKeys[0];
            BattleTurnResult result;
            try
            {
                result = state.ResolveTurn(BattleAction.Attack(skillKey), BattleAction.Attack(enemySkill), contentCatalog);
            }
            catch (ArgumentException)
            {
                return false;
            }

            partyCurrentHitPoints = state.Player.CurrentHitPoints;
            view?.PresentTurn(result, state);
            if (state.IsComplete) FinishBattle();
            return true;
        }

        public bool TrySubmitPrimaryAttack()
        {
            return IsBattleActive && TrySubmitPlayerAttack(state.Player.Spec.SkillKeys[0]);
        }

        public void ReturnToMap()
        {
            if (state != null && !state.IsComplete) return;
            RestoreOverworld();
            state = null;
            view?.HideBattle();
        }

        private void Awake()
        {
            view = battleViewComponent as IBattleView;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RestoreOverworld();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (state != null)
            {
                RestoreOverworld();
                state = null;
                view?.HideBattle();
            }
        }

        private void OnEncounterTriggered(EncounterTrigger trigger)
        {
            TryStartBattle(trigger);
        }

        private bool TryStartBattle(EncounterTrigger trigger)
        {
            if (IsBattleActive || contentCatalog == null || player == null ||
                !contentCatalog.TryResolveCreature(partyCreatureKey, out CreatureSpec partySpec) ||
                !contentCatalog.TryResolveCreature(trigger.Selection.CreatureKey, out CreatureSpec enemySpec))
            {
                return false;
            }

            int maximumPartyHitPoints = partySpec.CreateStatsForLevel(partyLevel).HitPoints;
            if (!hasPartyState)
            {
                partyCurrentHitPoints = maximumPartyHitPoints;
                hasPartyState = true;
            }

            partyCurrentHitPoints = Mathf.Clamp(partyCurrentHitPoints, 0, maximumPartyHitPoints);
            state = new BattleState(partySpec, partyLevel, partyCurrentHitPoints, enemySpec, trigger.Selection.Level);
            priorPlayerInputEnabled = player.InputEnabled;
            player.CancelPendingMove();
            player.InputEnabled = false;
            if (encounterSystem != null)
            {
                priorExplorationBlocked = encounterSystem.IsExplorationBlocked;
                encounterSystem.SetExplorationBlocked(true);
            }

            MapRuntimeRoot activeMap = mapTransitions == null ? null : mapTransitions.ActiveMap;
            suspendedDriver = activeMap == null ? null : activeMap.NpcSimulationDriver;
            if (suspendedDriver != null)
            {
                priorDriverSuspended = suspendedDriver.IsSuspended;
                suspendedDriver.SetSuspended(true);
            }

            overworldLocked = true;

            view?.PresentBattle(state);
            if (state.IsComplete) FinishBattle();
            return true;
        }

        private void FinishBattle()
        {
            partyCurrentHitPoints = state.Player.CurrentHitPoints;
            view?.PresentOutcome(state.Outcome, state);
        }

        private void RestoreOverworld()
        {
            if (!overworldLocked)
            {
                return;
            }

            if (player != null) player.InputEnabled = priorPlayerInputEnabled;
            if (encounterSystem != null) encounterSystem.SetExplorationBlocked(priorExplorationBlocked);
            if (suspendedDriver != null) suspendedDriver.SetSuspended(priorDriverSuspended);
            suspendedDriver = null;
            overworldLocked = false;
        }

        private void Subscribe()
        {
            if (!isSubscribed && encounterSystem != null)
            {
                encounterSystem.EncounterTriggered += OnEncounterTriggered;
                isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (isSubscribed && encounterSystem != null) encounterSystem.EncounterTriggered -= OnEncounterTriggered;
            isSubscribed = false;
        }
    }
}
