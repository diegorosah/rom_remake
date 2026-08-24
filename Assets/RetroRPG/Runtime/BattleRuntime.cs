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

    /// <summary>
    /// Connects encounter events to a pure one-versus-one battle state. A concrete
    /// battle view can submit the player's sole attack and render state through IBattleView.
    /// </summary>
}
