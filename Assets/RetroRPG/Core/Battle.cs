using System;
using System.Collections.Generic;

namespace RetroRPG.Core
{
    public readonly struct BattleStats
    {
        public BattleStats(int hitPoints, int attack, int defense, int speed)
        {
            if (hitPoints <= 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
            if (attack <= 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (defense <= 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            HitPoints = hitPoints; Attack = attack; Defense = defense; Speed = speed;
        }

        public int HitPoints { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }
    }

    public sealed class SkillSpec
    {
        public SkillSpec(string key, int power)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Skill key is required.", nameof(key));
            if (power <= 0) throw new ArgumentOutOfRangeException(nameof(power));
            Key = key; Power = power;
        }

        public string Key { get; }
        public int Power { get; }
    }

    public sealed class CreatureSpec
    {
        private readonly string[] skillKeys;

        public CreatureSpec(string key, BattleStats baseStats, IList<string> configuredSkillKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Creature key is required.", nameof(key));
            if (configuredSkillKeys == null || configuredSkillKeys.Count == 0) throw new ArgumentException("At least one skill key is required.", nameof(configuredSkillKeys));
            skillKeys = new string[configuredSkillKeys.Count];
            for (int index = 0; index < skillKeys.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(configuredSkillKeys[index])) throw new ArgumentException("Creature skill keys cannot be blank.", nameof(configuredSkillKeys));
                skillKeys[index] = configuredSkillKeys[index];
            }

            Key = key; BaseStats = baseStats;
        }

        public string Key { get; }
        public BattleStats BaseStats { get; }
        public IReadOnlyList<string> SkillKeys => skillKeys;

        public BattleStats CreateStatsForLevel(int level)
        {
            if (level <= 0 || level > 100) throw new ArgumentOutOfRangeException(nameof(level));
            int scale = level - 1;
            return new BattleStats(
                checked(BaseStats.HitPoints + scale * 2),
                checked(BaseStats.Attack + scale),
                checked(BaseStats.Defense + scale),
                checked(BaseStats.Speed + scale));
        }
    }

    public interface IBattleContentCatalog
    {
        bool TryResolveCreature(string creatureKey, out CreatureSpec creature);
        bool TryResolveSkill(string skillKey, out SkillSpec skill);
    }

    public enum BattleActionKind { Attack = 0 }
    public enum BattleOutcome { Ongoing = 0, PlayerWon = 1, PlayerLost = 2 }

    public static class BattleDamage
    {
        /// <summary>Deterministic preview rule; deliberately not native Pokémon damage emulation.</summary>
        public static int CalculateBasicPhysical(BattleCombatant attacker, BattleCombatant defender, SkillSpec skill)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            return Math.Max(1, checked(attacker.Stats.Attack + skill.Power - defender.Stats.Defense));
        }
    }

    public readonly struct BattleAction
    {
        public BattleAction(BattleActionKind kind, string skillKey)
        {
            if (kind != BattleActionKind.Attack) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(skillKey)) throw new ArgumentException("Attack skill key is required.", nameof(skillKey));
            Kind = kind; SkillKey = skillKey;
        }

        public BattleActionKind Kind { get; }
        public string SkillKey { get; }
        public static BattleAction Attack(string skillKey) => new BattleAction(BattleActionKind.Attack, skillKey);
    }

    public sealed class BattleCombatant
    {
        internal BattleCombatant(CreatureSpec spec, int level, int currentHitPoints)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Level = level;
            Stats = spec.CreateStatsForLevel(level);
            if (currentHitPoints < 0 || currentHitPoints > Stats.HitPoints) throw new ArgumentOutOfRangeException(nameof(currentHitPoints));
            CurrentHitPoints = currentHitPoints;
        }

        public CreatureSpec Spec { get; }
        public int Level { get; }
        public BattleStats Stats { get; }
        public int CurrentHitPoints { get; private set; }
        public bool IsFainted => CurrentHitPoints <= 0;

        internal int ApplyDamage(int damage)
        {
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            int applied = Math.Min(CurrentHitPoints, damage);
            CurrentHitPoints -= applied;
            return applied;
        }
    }

    public readonly struct BattleActionResult
    {
        public BattleActionResult(bool playerActed, string skillKey, int damage)
        {
            PlayerActed = playerActed; SkillKey = skillKey; Damage = damage;
        }

        public bool PlayerActed { get; }
        public string SkillKey { get; }
        public int Damage { get; }
    }

    public sealed class BattleTurnResult
    {
        public BattleTurnResult(int turnNumber, BattleActionResult? firstAction, BattleActionResult? secondAction, BattleOutcome outcome)
        {
            TurnNumber = turnNumber; FirstAction = firstAction; SecondAction = secondAction; Outcome = outcome;
        }

        public int TurnNumber { get; }
        public BattleActionResult? FirstAction { get; }
        public BattleActionResult? SecondAction { get; }
        public BattleOutcome Outcome { get; }
    }

    public sealed class BattleState
    {
        public BattleState(CreatureSpec playerSpec, int playerLevel, int playerCurrentHitPoints, CreatureSpec opponentSpec, int opponentLevel)
        {
            if (playerSpec == null) throw new ArgumentNullException(nameof(playerSpec));
            if (opponentSpec == null) throw new ArgumentNullException(nameof(opponentSpec));
            Player = new BattleCombatant(playerSpec, playerLevel, playerCurrentHitPoints);
            Opponent = new BattleCombatant(opponentSpec, opponentLevel, opponentSpec.CreateStatsForLevel(opponentLevel).HitPoints);
            Outcome = Player.IsFainted ? BattleOutcome.PlayerLost : BattleOutcome.Ongoing;
        }

        public BattleCombatant Player { get; }
        public BattleCombatant Opponent { get; }
        public BattleOutcome Outcome { get; private set; }
        public int TurnNumber { get; private set; }
        public bool IsComplete => Outcome != BattleOutcome.Ongoing;

        public BattleTurnResult ResolveTurn(BattleAction playerAction, BattleAction opponentAction, IBattleContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (IsComplete) throw new InvalidOperationException("The battle is already complete.");

            ValidateAction(Player, playerAction, content);
            ValidateAction(Opponent, opponentAction, content);
            TurnNumber = checked(TurnNumber + 1);
            bool playerFirst = Player.Stats.Speed >= Opponent.Stats.Speed;
            BattleActionResult? first = ResolveAction(playerFirst, playerAction, opponentAction, content);
            BattleActionResult? second = null;
            if (!IsComplete)
            {
                second = ResolveAction(!playerFirst, playerAction, opponentAction, content);
            }

            return new BattleTurnResult(TurnNumber, first, second, Outcome);
        }

        private BattleActionResult ResolveAction(bool playerActs, BattleAction playerAction, BattleAction opponentAction, IBattleContentCatalog content)
        {
            BattleCombatant attacker = playerActs ? Player : Opponent;
            BattleCombatant defender = playerActs ? Opponent : Player;
            BattleAction action = playerActs ? playerAction : opponentAction;
            content.TryResolveSkill(action.SkillKey, out SkillSpec skill);
            int appliedDamage = defender.ApplyDamage(BattleDamage.CalculateBasicPhysical(attacker, defender, skill));
            if (defender.IsFainted) Outcome = playerActs ? BattleOutcome.PlayerWon : BattleOutcome.PlayerLost;
            return new BattleActionResult(playerActs, action.SkillKey, appliedDamage);
        }

        private static void ValidateAction(BattleCombatant combatant, BattleAction action, IBattleContentCatalog content)
        {
            if (action.Kind != BattleActionKind.Attack || !ContainsSkill(combatant.Spec, action.SkillKey) || !content.TryResolveSkill(action.SkillKey, out _))
            {
                throw new ArgumentException("Battle action is not available to this combatant.", nameof(action));
            }
        }

        private static bool ContainsSkill(CreatureSpec creature, string key)
        {
            for (int index = 0; index < creature.SkillKeys.Count; index++) if (string.Equals(creature.SkillKeys[index], key, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public interface IBattleView
    {
        void PresentBattle(BattleState state);
        void PresentTurn(BattleTurnResult result, BattleState state);
        void PresentOutcome(BattleOutcome outcome, BattleState state);
        void HideBattle();
    }
}
