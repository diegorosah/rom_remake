using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.Core;
using RetroRPG.Runtime;
using UnityEngine;

namespace RetroRPG.Tests.EditMode
{
    public sealed class Mvp7BattleTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null) UnityEngine.Object.DestroyImmediate(objects[index]);
            }
            objects.Clear();
        }

        [Test]
        public void BattleStatsAndCreatureLevels_EnforcePositiveBoundsAndScaleDeterministically()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStats(0, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStats(1, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStats(1, 1, 1, 0));
            var creature = new CreatureSpec("synthetic", new BattleStats(10, 5, 4, 3), new[] { "tackle" });
            Assert.That(creature.CreateStatsForLevel(1).HitPoints, Is.EqualTo(10));
            Assert.That(creature.CreateStatsForLevel(10).HitPoints, Is.EqualTo(28));
            Assert.That(creature.CreateStatsForLevel(10).Attack, Is.EqualTo(14));
            Assert.Throws<ArgumentOutOfRangeException>(() => creature.CreateStatsForLevel(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => creature.CreateStatsForLevel(101));
            Assert.Throws<ArgumentException>(() => new CreatureSpec("bad", new BattleStats(1, 1, 1, 1), new string[0]));
        }

        [Test]
        public void BattleState_OrdersActionsBySpeedAndPlayerWinsTies()
        {
            var player = Creature("player", 10, 5, 2, 10, "strike");
            var opponent = Creature("opponent", 10, 5, 2, 5, "scratch");
            var catalog = Content(player, opponent, new SkillSpec("strike", 1), new SkillSpec("scratch", 1));
            var state = new BattleState(player, 1, 10, opponent, 1);
            var turn = state.ResolveTurn(BattleAction.Attack("strike"), BattleAction.Attack("scratch"), catalog);
            Assert.That(turn.FirstAction.HasValue, Is.True);
            Assert.That(turn.FirstAction.Value.PlayerActed, Is.True);
            Assert.That(turn.SecondAction.Value.PlayerActed, Is.False);

            var tiedState = new BattleState(player, 1, 10, Creature("tie", 10, 5, 2, 10, "scratch"), 1);
            var tiedTurn = tiedState.ResolveTurn(BattleAction.Attack("strike"), BattleAction.Attack("scratch"), Content(player, tiedState.Opponent.Spec, new SkillSpec("strike", 1), new SkillSpec("scratch", 1)));
            Assert.That(tiedTurn.FirstAction.Value.PlayerActed, Is.True);
        }

        [Test]
        public void BattleState_DamageHasMinimumOneAndPersistsCurrentHp()
        {
            var player = Creature("player", 20, 1, 100, 5, "weak");
            var opponent = Creature("opponent", 20, 1, 100, 1, "weak");
            var catalog = Content(player, opponent, new SkillSpec("weak", 1));
            var state = new BattleState(player, 1, 20, opponent, 1);
            var result = state.ResolveTurn(BattleAction.Attack("weak"), BattleAction.Attack("weak"), catalog);
            Assert.That(result.FirstAction.Value.Damage, Is.EqualTo(1));
            Assert.That(result.SecondAction.Value.Damage, Is.EqualTo(1));
            Assert.That(state.Player.CurrentHitPoints, Is.EqualTo(19));
            Assert.That(state.Opponent.CurrentHitPoints, Is.EqualTo(19));
        }

        [Test]
        public void BattleState_RejectsUnavailableActionsNullContentAndActionsAfterOutcome()
        {
            var player = Creature("player", 5, 20, 1, 10, "strike");
            var opponent = Creature("opponent", 5, 1, 1, 1, "scratch");
            var catalog = Content(player, opponent, new SkillSpec("strike", 10), new SkillSpec("scratch", 1));
            var state = new BattleState(player, 1, 5, opponent, 1);
            Assert.Throws<ArgumentNullException>(() => state.ResolveTurn(BattleAction.Attack("strike"), BattleAction.Attack("scratch"), null));
            Assert.Throws<ArgumentException>(() => state.ResolveTurn(BattleAction.Attack("missing"), BattleAction.Attack("scratch"), catalog));
            var winningTurn = state.ResolveTurn(BattleAction.Attack("strike"), BattleAction.Attack("scratch"), catalog);
            Assert.That(winningTurn.Outcome, Is.EqualTo(BattleOutcome.PlayerWon));
            Assert.That(state.IsComplete, Is.True);
            Assert.Throws<InvalidOperationException>(() => state.ResolveTurn(BattleAction.Attack("strike"), BattleAction.Attack("scratch"), catalog));
            Assert.Throws<ArgumentException>(() => new BattleAction(BattleActionKind.Attack, string.Empty));
        }

        [Test]
        public void BattleState_ReportsPlayerDefeatWhenOpponentFaintsPlayer()
        {
            var player = Creature("player", 3, 1, 1, 1, "scratch");
            var opponent = Creature("opponent", 3, 20, 1, 10, "blast");
            var catalog = Content(player, opponent, new SkillSpec("scratch", 1), new SkillSpec("blast", 10));
            var state = new BattleState(player, 1, 3, opponent, 1);
            var turn = state.ResolveTurn(BattleAction.Attack("scratch"), BattleAction.Attack("blast"), catalog);
            Assert.That(turn.Outcome, Is.EqualTo(BattleOutcome.PlayerLost));
            Assert.That(state.Player.IsFainted, Is.True);
            Assert.That(turn.SecondAction.HasValue, Is.False);
        }

        [Test]
        public void BattleState_RejectsInvalidCombatantHpAndNullSpecs()
        {
            var player = Creature("player", 5, 2, 2, 2, "strike");
            var opponent = Creature("opponent", 5, 2, 2, 2, "strike");
            Assert.Throws<ArgumentNullException>(() => new BattleState(null, 1, 1, opponent, 1));
            Assert.Throws<ArgumentNullException>(() => new BattleState(player, 1, 1, null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleState(player, 1, 6, opponent, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleState(player, 0, 1, opponent, 1));
        }

        [Test]
        public void RuntimeBattleCatalog_RejectsMissingSkillReferencesBeforeBattleStarts()
        {
            var content = Track(new GameObject("invalid-battle-content")).AddComponent<RuntimeBattleContentCatalog>();
            var creature = Creature("player", 5, 2, 2, 2, "missing");
            Assert.Throws<InvalidOperationException>(() => content.Configure(new[] { creature }, new[] { new SkillSpec("different", 1) }));
        }

        [Test]
        public void BattleCoordinator_OpensFromEncounterLocksOverworldAttacksWinsAndRestores()
        {
            var collision = CreateCollisionMap(4, 3);
            var map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("route", collision, new MapRuntimeWarp[0]);
            var driver = map.gameObject.AddComponent<NpcSimulationDriver>();
            driver.Configure(map);
            var mapCatalog = Track(new GameObject("maps")).AddComponent<RuntimeMapCatalog>();
            mapCatalog.Configure(new[] { map });
            var table = new EncounterTableDefinition("land", 10000, new[] { new EncounterSlotDefinition("enemy", 100, 1, 1) });
            var encounterCatalog = Track(new GameObject("encounters")).AddComponent<RuntimeEncounterCatalog>();
            encounterCatalog.Configure(new[] { table }, new[] { new EncounterCellDefinition("route", new Vector2Int(2, 1), 0, "land", true) });
            var player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, new Vector2Int(1, 1), 0, 2f);
            var transitions = Track(new GameObject("transitions")).AddComponent<MapTransitionSystem>();
            transitions.Configure(mapCatalog, player, null, map);
            var encounterSystem = Track(new GameObject("encounter-system")).AddComponent<EncounterSystem>();
            encounterSystem.Configure(player, null, mapCatalog, encounterCatalog, null, new QueueEncounterRandom(0, 0, 0), null);
            var content = Track(new GameObject("battle-content")).AddComponent<RuntimeBattleContentCatalog>();
            var playerSpec = new CreatureSpec("player", new BattleStats(20, 20, 1, 1), new[] { "strike" });
            var enemySpec = new CreatureSpec("enemy", new BattleStats(5, 1, 1, 10), new[] { "scratch" });
            content.Configure(new[] { playerSpec, enemySpec }, new[] { new SkillSpec("strike", 10), new SkillSpec("scratch", 1) });
            var view = new RecordingBattleView();
            var coordinator = Track(new GameObject("battle-coordinator")).AddComponent<BattleCoordinator>();
            coordinator.Configure(encounterSystem, player, transitions, content, view);
            coordinator.ConfigureParty("player", 1, 20);

            Assert.That(player.TryMove(GridDirection.Right), Is.True);
            Assert.That(coordinator.IsBattleActive, Is.False, "the encounter is emitted only after the step completes");
            player.Advance(0.5f);
            Assert.That(coordinator.IsBattleActive, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(encounterSystem.IsExplorationBlocked, Is.True);
            Assert.That(driver.IsSuspended, Is.True);
            Assert.That(view.PresentBattleCount, Is.EqualTo(1));

            Assert.That(coordinator.TrySubmitPrimaryAttack(), Is.True);
            Assert.That(coordinator.IsBattleActive, Is.False);
            Assert.That(coordinator.IsAwaitingReturn, Is.True);
            Assert.That(coordinator.PartyCurrentHitPoints, Is.EqualTo(19), "enemy acts first and HP persists in coordinator memory");
            Assert.That(view.LastOutcome, Is.EqualTo(BattleOutcome.PlayerWon));
            Assert.That(view.HideCount, Is.Zero);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(encounterSystem.IsExplorationBlocked, Is.True);
            Assert.That(driver.IsSuspended, Is.True);

            coordinator.ReturnToMap();
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(encounterSystem.IsExplorationBlocked, Is.False);
            Assert.That(driver.IsSuspended, Is.False);
            Assert.That(view.HideCount, Is.EqualTo(1));

            Assert.That(player.TryMove(GridDirection.Left), Is.True);
            player.Advance(0.5f);
            Assert.That(player.TryMove(GridDirection.Right), Is.True);
            player.Advance(0.5f);
            Assert.That(coordinator.IsBattleActive, Is.True);
            Assert.That(coordinator.State.Player.CurrentHitPoints, Is.EqualTo(19), "the next battle starts with persisted party HP");
        }

        private static CreatureSpec Creature(string key, int hp, int attack, int defense, int speed, string skill)
        {
            return new CreatureSpec(key, new BattleStats(hp, attack, defense, speed), new[] { skill });
        }

        private static IBattleContentCatalog Content(CreatureSpec player, CreatureSpec opponent, params SkillSpec[] skills)
        {
            return new TestContentCatalog(new[] { player, opponent }, skills);
        }

        private sealed class TestContentCatalog : IBattleContentCatalog
        {
            private readonly Dictionary<string, CreatureSpec> creatures = new Dictionary<string, CreatureSpec>(StringComparer.Ordinal);
            private readonly Dictionary<string, SkillSpec> skills = new Dictionary<string, SkillSpec>(StringComparer.Ordinal);

            public TestContentCatalog(IList<CreatureSpec> configuredCreatures, IList<SkillSpec> configuredSkills)
            {
                for (var index = 0; index < configuredCreatures.Count; index++) creatures[configuredCreatures[index].Key] = configuredCreatures[index];
                for (var index = 0; index < configuredSkills.Count; index++) skills[configuredSkills[index].Key] = configuredSkills[index];
            }

            public bool TryResolveCreature(string creatureKey, out CreatureSpec creature) => creatures.TryGetValue(creatureKey, out creature);
            public bool TryResolveSkill(string skillKey, out SkillSpec skill) => skills.TryGetValue(skillKey, out skill);
        }

        private GridCollisionMap CreateCollisionMap(int width, int height)
        {
            var map = Track(new GameObject("collision")).AddComponent<GridCollisionMap>();
            map.Configure(width, height, new byte[width * height], new byte[width * height], new GridDirectionMask[width * height]);
            return map;
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            objects.Add(unityObject);
            return unityObject;
        }

        private sealed class QueueEncounterRandom : IEncounterRandomSource
        {
            private readonly Queue<int> values;
            public QueueEncounterRandom(params int[] configuredValues) { values = new Queue<int>(configuredValues); }
            public int NextInt(int exclusiveUpperBound) { return values.Count == 0 ? 0 : values.Dequeue() % exclusiveUpperBound; }
        }

        private sealed class RecordingBattleView : IBattleView
        {
            public int PresentBattleCount;
            public int HideCount;
            public BattleOutcome LastOutcome;
            public void PresentBattle(BattleState state) { PresentBattleCount++; }
            public void PresentTurn(BattleTurnResult result, BattleState state) { }
            public void PresentOutcome(BattleOutcome outcome, BattleState state) { LastOutcome = outcome; }
            public void HideBattle() { HideCount++; }
        }
    }
}
