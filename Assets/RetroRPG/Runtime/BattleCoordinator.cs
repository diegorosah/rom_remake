using System;
using System.Collections.Generic;
using RetroRPG.Core;
using UnityEngine;

namespace RetroRPG.Runtime
{
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
