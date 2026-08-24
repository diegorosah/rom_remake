using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    /// <summary>Immutable six-stat baseline for a creature. Values are source content, not runtime state.</summary>
    [Serializable]
    public sealed class CreatureBaseStatsDefinition
    {
        public CreatureBaseStatsDefinition(int hitPoints, int attack, int defense, int speed, int specialAttack, int specialDefense)
        {
            if (hitPoints <= 0 || attack <= 0 || defense <= 0 || speed <= 0 || specialAttack <= 0 || specialDefense <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hitPoints));
            }

            HitPoints = hitPoints;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            SpecialAttack = specialAttack;
            SpecialDefense = specialDefense;
        }

        public int HitPoints { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }
        public int SpecialAttack { get; }
        public int SpecialDefense { get; }
    }

    public enum SkillEffectKind
    {
        DirectDamage
    }

    public enum SkillTargetKind
    {
        SingleOpponent
    }

    /// <summary>Game-agnostic declarative skill content. It does not represent executable source-game behavior.</summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public SkillDefinition(
            string id,
            int sourceId,
            string displayName,
            string typeId,
            SkillEffectKind effect,
            int power,
            int accuracy,
            int maximumUses,
            int secondaryEffectChance,
            SkillTargetKind target,
            int priority,
            uint flags)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Skill id, display name, and type id are required.");
            }

            if (sourceId <= 0 || power <= 0 || accuracy < 0 || accuracy > 100 || maximumUses <= 0 || secondaryEffectChance < 0 || secondaryEffectChance > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId));
            }

            if (effect != SkillEffectKind.DirectDamage || target != SkillTargetKind.SingleOpponent)
            {
                throw new ArgumentOutOfRangeException(nameof(effect));
            }

            Id = id;
            SourceId = sourceId;
            DisplayName = displayName;
            TypeId = typeId;
            Effect = effect;
            Power = power;
            Accuracy = accuracy;
            MaximumUses = maximumUses;
            SecondaryEffectChance = secondaryEffectChance;
            Target = target;
            Priority = priority;
            Flags = flags;
        }

        public string Id { get; }
        public int SourceId { get; }
        public string DisplayName { get; }
        public string TypeId { get; }
        public SkillEffectKind Effect { get; }
        public int Power { get; }
        public int Accuracy { get; }
        public int MaximumUses { get; }
        public int SecondaryEffectChance { get; }
        public SkillTargetKind Target { get; }
        public int Priority { get; }
        public uint Flags { get; }
    }

    /// <summary>One indexed 4bpp front/back sprite pair plus its palette for battle presentation.</summary>
    [Serializable]
    public sealed class CreatureSpriteDefinition
    {
        public const int PaletteColorCount = 16;

        public CreatureSpriteDefinition(
            string id,
            string creatureId,
            int width,
            int height,
            IList<Rgba32> palette,
            IndexedSpriteFrameDefinition front,
            IndexedSpriteFrameDefinition back)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(creatureId))
            {
                throw new ArgumentException("Sprite and creature ids are required.");
            }

            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (palette == null || palette.Count != PaletteColorCount) throw new ArgumentException("A creature sprite palette has exactly sixteen colours.", nameof(palette));
            if (front == null || back == null) throw new ArgumentNullException(front == null ? nameof(front) : nameof(back));
            if (front.Width != width || front.Height != height || back.Width != width || back.Height != height)
            {
                throw new ArgumentException("Creature sprite frames must match their declared dimensions.");
            }

            Id = id;
            CreatureId = creatureId;
            Width = width;
            Height = height;
            Palette = new ReadOnlyCollection<Rgba32>(new List<Rgba32>(palette));
            Front = front;
            Back = back;
        }

        public string Id { get; }
        public string CreatureId { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<Rgba32> Palette { get; }
        public IndexedSpriteFrameDefinition Front { get; }
        public IndexedSpriteFrameDefinition Back { get; }
    }

    /// <summary>Immutable source-content creature with stable source identity and its whitelisted skills.</summary>
    [Serializable]
    public sealed class CreatureDefinition
    {
        public CreatureDefinition(
            string id,
            int sourceId,
            string displayName,
            CreatureBaseStatsDefinition baseStats,
            IList<string> typeIds,
            IList<string> skillIds,
            string spriteId)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(spriteId))
            {
                throw new ArgumentException("Creature id, display name, and sprite id are required.");
            }

            if (sourceId <= 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (baseStats == null) throw new ArgumentNullException(nameof(baseStats));
            if (typeIds == null || typeIds.Count == 0 || typeIds.Count > 2) throw new ArgumentException("A creature has one or two type ids.", nameof(typeIds));
            if (skillIds == null || skillIds.Count == 0) throw new ArgumentException("A creature needs at least one skill id.", nameof(skillIds));

            var copiedTypes = CopyKeys(typeIds, nameof(typeIds));
            var copiedSkills = CopyKeys(skillIds, nameof(skillIds));
            Id = id;
            SourceId = sourceId;
            DisplayName = displayName;
            BaseStats = baseStats;
            TypeIds = new ReadOnlyCollection<string>(copiedTypes);
            SkillIds = new ReadOnlyCollection<string>(copiedSkills);
            SpriteId = spriteId;
        }

        public string Id { get; }
        public int SourceId { get; }
        public string DisplayName { get; }
        public CreatureBaseStatsDefinition BaseStats { get; }
        public IReadOnlyList<string> TypeIds { get; }
        public IReadOnlyList<string> SkillIds { get; }
        public string SpriteId { get; }

        private static List<string> CopyKeys(IList<string> values, string parameterName)
        {
            var copied = new List<string>(values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index])) throw new ArgumentException("Content keys cannot be blank.", parameterName);
                copied.Add(values[index]);
            }

            return copied;
        }
    }

    /// <summary>Deterministically ordered, self-contained creature, skill, and indexed-sprite content.</summary>
    [Serializable]
    public sealed class BattleContentCatalogDefinition
    {
        private readonly Dictionary<string, CreatureDefinition> creaturesById;
        private readonly Dictionary<int, CreatureDefinition> creaturesBySourceId;
        private readonly Dictionary<string, SkillDefinition> skillsById;
        private readonly Dictionary<int, SkillDefinition> skillsBySourceId;
        private readonly Dictionary<string, CreatureSpriteDefinition> spritesById;

        public BattleContentCatalogDefinition(
            IList<CreatureDefinition> creatures,
            IList<SkillDefinition> skills,
            IList<CreatureSpriteDefinition> sprites)
            : this(creatures, skills, sprites, null)
        {
        }

        public BattleContentCatalogDefinition(
            IList<CreatureDefinition> creatures,
            IList<SkillDefinition> skills,
            IList<CreatureSpriteDefinition> sprites,
            string defaultPlayerCreatureId)
        {
            if (creatures == null || skills == null || sprites == null)
            {
                throw new ArgumentNullException(creatures == null ? nameof(creatures) : skills == null ? nameof(skills) : nameof(sprites));
            }

            creaturesById = new Dictionary<string, CreatureDefinition>(StringComparer.Ordinal);
            creaturesBySourceId = new Dictionary<int, CreatureDefinition>();
            skillsById = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            skillsBySourceId = new Dictionary<int, SkillDefinition>();
            spritesById = new Dictionary<string, CreatureSpriteDefinition>(StringComparer.Ordinal);

            var copiedSkills = CopySkills(skills);
            var copiedSprites = CopySprites(sprites);
            var copiedCreatures = CopyCreatures(creatures);
            ValidateReferences(copiedCreatures);

            copiedCreatures.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            copiedSkills.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            copiedSprites.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            Creatures = new ReadOnlyCollection<CreatureDefinition>(copiedCreatures);
            Skills = new ReadOnlyCollection<SkillDefinition>(copiedSkills);
            Sprites = new ReadOnlyCollection<CreatureSpriteDefinition>(copiedSprites);
            DefaultPlayerCreatureId = string.IsNullOrWhiteSpace(defaultPlayerCreatureId) ? copiedCreatures[0].Id : defaultPlayerCreatureId;
            if (!creaturesById.ContainsKey(DefaultPlayerCreatureId)) throw new ArgumentException("Default player creature must exist in the catalog.", nameof(defaultPlayerCreatureId));
        }

        public IReadOnlyList<CreatureDefinition> Creatures { get; }
        public IReadOnlyList<SkillDefinition> Skills { get; }
        public IReadOnlyList<CreatureSpriteDefinition> Sprites { get; }
        public string DefaultPlayerCreatureId { get; }

        public bool TryGetCreature(string id, out CreatureDefinition creature) => creaturesById.TryGetValue(id, out creature);
        public bool TryGetCreatureBySourceId(int sourceId, out CreatureDefinition creature) => creaturesBySourceId.TryGetValue(sourceId, out creature);
        public bool TryGetSkill(string id, out SkillDefinition skill) => skillsById.TryGetValue(id, out skill);
        public bool TryGetSkillBySourceId(int sourceId, out SkillDefinition skill) => skillsBySourceId.TryGetValue(sourceId, out skill);
        public bool TryGetSprite(string id, out CreatureSpriteDefinition sprite) => spritesById.TryGetValue(id, out sprite);

        private List<SkillDefinition> CopySkills(IList<SkillDefinition> skills)
        {
            if (skills.Count == 0) throw new ArgumentException("Battle content needs at least one skill.", nameof(skills));
            var copied = new List<SkillDefinition>(skills.Count);
            for (var index = 0; index < skills.Count; index++)
            {
                var skill = skills[index] ?? throw new ArgumentException("Skills cannot contain null.", nameof(skills));
                if (skillsById.ContainsKey(skill.Id) || skillsBySourceId.ContainsKey(skill.SourceId))
                {
                    throw new ArgumentException("Skill ids and source ids must be unique.", nameof(skills));
                }

                skillsById.Add(skill.Id, skill);
                skillsBySourceId.Add(skill.SourceId, skill);
                copied.Add(skill);
            }

            return copied;
        }

        private List<CreatureSpriteDefinition> CopySprites(IList<CreatureSpriteDefinition> sprites)
        {
            if (sprites.Count == 0) throw new ArgumentException("Battle content needs at least one sprite.", nameof(sprites));
            var copied = new List<CreatureSpriteDefinition>(sprites.Count);
            var creatureIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < sprites.Count; index++)
            {
                var sprite = sprites[index] ?? throw new ArgumentException("Sprites cannot contain null.", nameof(sprites));
                if (spritesById.ContainsKey(sprite.Id) || creatureIds.Contains(sprite.CreatureId))
                {
                    throw new ArgumentException("Sprite ids and sprite creature ids must be unique.", nameof(sprites));
                }

                spritesById.Add(sprite.Id, sprite);
                creatureIds.Add(sprite.CreatureId);
                copied.Add(sprite);
            }

            return copied;
        }

        private List<CreatureDefinition> CopyCreatures(IList<CreatureDefinition> creatures)
        {
            if (creatures.Count == 0) throw new ArgumentException("Battle content needs at least one creature.", nameof(creatures));
            var copied = new List<CreatureDefinition>(creatures.Count);
            for (var index = 0; index < creatures.Count; index++)
            {
                var creature = creatures[index] ?? throw new ArgumentException("Creatures cannot contain null.", nameof(creatures));
                if (creaturesById.ContainsKey(creature.Id) || creaturesBySourceId.ContainsKey(creature.SourceId))
                {
                    throw new ArgumentException("Creature ids and source ids must be unique.", nameof(creatures));
                }

                creaturesById.Add(creature.Id, creature);
                creaturesBySourceId.Add(creature.SourceId, creature);
                copied.Add(creature);
            }

            return copied;
        }

        private void ValidateReferences(IList<CreatureDefinition> creatures)
        {
            for (var index = 0; index < creatures.Count; index++)
            {
                var creature = creatures[index];
                if (!spritesById.TryGetValue(creature.SpriteId, out var sprite) || !string.Equals(sprite.CreatureId, creature.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Every creature must reference its own sprite.", nameof(creatures));
                }

                for (var skillIndex = 0; skillIndex < creature.SkillIds.Count; skillIndex++)
                {
                    if (!skillsById.ContainsKey(creature.SkillIds[skillIndex])) throw new ArgumentException("Creature skills must exist in the catalog.", nameof(creatures));
                }
            }
        }
    }
}
