using System;
using System.Collections.Generic;
using RetroRPG.Core;
using UnityEngine;

namespace RetroRPG.Runtime
{
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
            creature = null;

            if (string.IsNullOrWhiteSpace(creatureKey))
            {
                return false;
            }

            return creatures.TryGetValue(creatureKey, out creature);
        }

        public bool TryResolveSkill(string skillKey, out SkillSpec skill)
        {
            skill = null;

            if (string.IsNullOrWhiteSpace(skillKey))
            {
                return false;
            }

            return skills.TryGetValue(skillKey, out skill);
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
}
