using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Strict data-only decoder for the audited MVP 7 creature, skill, and sprite whitelist.</summary>
    internal static class FireRedBattleContentParser
    {
        private const string TackleId = "move_tackle";

        private static readonly BattleCreatureSpec[] CreatureSpecs =
        {
            new BattleCreatureSpec(FireRedRomLayoutRev1.PokemonSpeciesBulbasaur, "pokemon_bulbasaur", "Bulbasaur", "grass", "poison", 45, 49, 49, 45, 65, 65, FireRedRomLayoutRev1.BulbasaurFrontSpriteData, FireRedRomLayoutRev1.BulbasaurBackSpriteData, FireRedRomLayoutRev1.BulbasaurSpritePaletteData),
            new BattleCreatureSpec(FireRedRomLayoutRev1.PokemonSpeciesPidgey, "pokemon_pidgey", "Pidgey", "normal", "flying", 40, 45, 40, 56, 35, 35, FireRedRomLayoutRev1.PidgeyFrontSpriteData, FireRedRomLayoutRev1.PidgeyBackSpriteData, FireRedRomLayoutRev1.PidgeySpritePaletteData),
            new BattleCreatureSpec(FireRedRomLayoutRev1.PokemonSpeciesRattata, "pokemon_rattata", "Rattata", "normal", "normal", 30, 56, 35, 72, 25, 35, FireRedRomLayoutRev1.RattataFrontSpriteData, FireRedRomLayoutRev1.RattataBackSpriteData, FireRedRomLayoutRev1.RattataSpritePaletteData)
        };

        public static BattleContentCatalogDefinition Parse(RomReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var tackle = DecodeTackle(reader);
            var creatures = new List<CreatureDefinition>(CreatureSpecs.Length);
            var sprites = new List<CreatureSpriteDefinition>(CreatureSpecs.Length);
            for (var index = 0; index < CreatureSpecs.Length; index++)
            {
                var spec = CreatureSpecs[index];
                creatures.Add(DecodeCreature(reader, spec));
                sprites.Add(DecodeSprite(reader, spec));
            }

            return new BattleContentCatalogDefinition(creatures, new[] { tackle }, sprites, "pokemon_bulbasaur");
        }

        private static CreatureDefinition DecodeCreature(RomReader reader, BattleCreatureSpec spec)
        {
            var record = checked(FireRedRomLayoutRev1.PokemonSpeciesInfoTable + (spec.SpeciesId * FireRedRomLayoutRev1.PokemonSpeciesInfoRecordSize));
            reader.EnsureRange(record, FireRedRomLayoutRev1.PokemonSpeciesInfoRecordSize, spec.DisplayName + " species record is outside ROM bounds.");
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoHitPointsOffset)), spec.HitPoints, spec.DisplayName + " base HP", record);
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoAttackOffset)), spec.Attack, spec.DisplayName + " base attack", checked(record + 1));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoDefenseOffset)), spec.Defense, spec.DisplayName + " base defense", checked(record + 2));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoSpeedOffset)), spec.Speed, spec.DisplayName + " base speed", checked(record + 3));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoSpecialAttackOffset)), spec.SpecialAttack, spec.DisplayName + " base special attack", checked(record + 4));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoSpecialDefenseOffset)), spec.SpecialDefense, spec.DisplayName + " base special defense", checked(record + 5));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoPrimaryTypeOffset)), TypeValue(spec.PrimaryType), spec.DisplayName + " primary type", checked(record + 6));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.PokemonSpeciesInfoSecondaryTypeOffset)), TypeValue(spec.SecondaryType), spec.DisplayName + " secondary type", checked(record + 7));

            return new CreatureDefinition(
                spec.Id,
                spec.SpeciesId,
                spec.DisplayName,
                new CreatureBaseStatsDefinition(spec.HitPoints, spec.Attack, spec.Defense, spec.Speed, spec.SpecialAttack, spec.SpecialDefense),
                new[] { spec.PrimaryType, spec.SecondaryType },
                new[] { TackleId },
                SpriteId(spec));
        }

        private static SkillDefinition DecodeTackle(RomReader reader)
        {
            var record = checked(FireRedRomLayoutRev1.BattleMovesTable + (FireRedRomLayoutRev1.BattleMoveTackle * FireRedRomLayoutRev1.BattleMoveRecordSize));
            reader.EnsureRange(record, FireRedRomLayoutRev1.BattleMoveRecordSize, "Tackle move record is outside ROM bounds.");
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveEffectOffset)), FireRedRomLayoutRev1.BattleMoveEffectHit, "Tackle effect", record);
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMovePowerOffset)), FireRedRomLayoutRev1.BattleMoveTacklePower, "Tackle power", checked(record + 1));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveTypeOffset)), FireRedRomLayoutRev1.PokemonTypeNormal, "Tackle type", checked(record + 2));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveAccuracyOffset)), FireRedRomLayoutRev1.BattleMoveTackleAccuracy, "Tackle accuracy", checked(record + 3));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveMaximumUsesOffset)), FireRedRomLayoutRev1.BattleMoveTackleMaximumUses, "Tackle maximum uses", checked(record + 4));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveSecondaryEffectChanceOffset)), 0, "Tackle secondary-effect chance", checked(record + 5));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMoveTargetOffset)), FireRedRomLayoutRev1.BattleMoveTargetSelectedOpponent, "Tackle target", checked(record + 6));
            ExpectEqual(reader, reader.ReadByte(checked(record + FireRedRomLayoutRev1.BattleMovePriorityOffset)), 0, "Tackle priority", checked(record + 7));
            ExpectEqual(reader, reader.ReadUInt32(checked(record + FireRedRomLayoutRev1.BattleMoveFlagsOffset)), FireRedRomLayoutRev1.BattleMoveTackleFlags, "Tackle flags", checked(record + 8));

            return new SkillDefinition(TackleId, FireRedRomLayoutRev1.BattleMoveTackle, "Tackle", "normal", SkillEffectKind.DirectDamage, FireRedRomLayoutRev1.BattleMoveTacklePower, FireRedRomLayoutRev1.BattleMoveTackleAccuracy, FireRedRomLayoutRev1.BattleMoveTackleMaximumUses, 0, SkillTargetKind.SingleOpponent, 0, FireRedRomLayoutRev1.BattleMoveTackleFlags);
        }

        private static CreatureSpriteDefinition DecodeSprite(RomReader reader, BattleCreatureSpec spec)
        {
            var front = DecodeSheet(reader, FireRedRomLayoutRev1.BattleFrontSpriteSheetTable, spec.SpeciesId, spec.FrontDataOffset, spec.DisplayName + " front sprite", 0);
            var back = DecodeSheet(reader, FireRedRomLayoutRev1.BattleBackSpriteSheetTable, spec.SpeciesId, spec.BackDataOffset, spec.DisplayName + " back sprite", 1);
            var palette = DecodePalette(reader, spec.SpeciesId, spec.PaletteDataOffset, spec.DisplayName + " sprite palette");
            return new CreatureSpriteDefinition(SpriteId(spec), spec.Id, FireRedRomLayoutRev1.BattleSpriteWidth, FireRedRomLayoutRev1.BattleSpriteHeight, palette, front, back);
        }

        private static IndexedSpriteFrameDefinition DecodeSheet(RomReader reader, int table, int speciesId, int expectedData, string description, int frameIndex)
        {
            var entry = checked(table + (speciesId * FireRedRomLayoutRev1.CompressedSpriteSheetRecordSize));
            reader.EnsureRange(entry, FireRedRomLayoutRev1.CompressedSpriteSheetRecordSize, description + " sheet record is outside ROM bounds.");
            ExpectPointer(reader, checked(entry + FireRedRomLayoutRev1.CompressedSpriteSheetDataOffset), expectedData, description + " sheet data");
            ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.CompressedSpriteSheetOutputSizeOffset)), FireRedRomLayoutRev1.BattleSprite4BppByteSize, description + " sheet output size", checked(entry + 4));
            ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.CompressedSpriteSheetTagOffset)), speciesId, description + " sheet tag", checked(entry + 6));
            var packed = GbaLz10Decoder.Decode(reader, expectedData, FireRedRomLayoutRev1.BattleSprite4BppByteSize);
            if (packed.Length != FireRedRomLayoutRev1.BattleSprite4BppByteSize)
            {
                throw new RomReadException(description + " LZ10 output length does not match the audited 64x64 4bpp sheet.", expectedData, packed.Length, reader.Length);
            }

            return new IndexedSpriteFrameDefinition(frameIndex, FireRedRomLayoutRev1.BattleSpriteWidth, FireRedRomLayoutRev1.BattleSpriteHeight, Expand4BppTiles(packed));
        }

        private static List<Rgba32> DecodePalette(RomReader reader, int speciesId, int expectedData, string description)
        {
            var entry = checked(FireRedRomLayoutRev1.BattleSpritePaletteTable + (speciesId * FireRedRomLayoutRev1.CompressedSpritePaletteRecordSize));
            reader.EnsureRange(entry, FireRedRomLayoutRev1.CompressedSpritePaletteRecordSize, description + " record is outside ROM bounds.");
            ExpectPointer(reader, checked(entry + FireRedRomLayoutRev1.CompressedSpritePaletteDataOffset), expectedData, description + " data");
            ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.CompressedSpritePaletteTagOffset)), speciesId, description + " tag", checked(entry + 4));
            var bytes = GbaLz10Decoder.Decode(reader, expectedData, FireRedRomLayoutRev1.BattleSpritePaletteByteSize);
            if (bytes.Length != FireRedRomLayoutRev1.BattleSpritePaletteByteSize)
            {
                throw new RomReadException(description + " LZ10 output length does not match the audited 16-colour palette.", expectedData, bytes.Length, reader.Length);
            }

            var colors = new List<Rgba32>(FireRedRomLayoutRev1.BattleSpritePaletteColorCount);
            for (var index = 0; index < FireRedRomLayoutRev1.BattleSpritePaletteColorCount; index++)
            {
                var offset = checked(index * FireRedRomLayoutRev1.GbaHalfwordSize);
                var bgr555 = (ushort)(bytes[offset] | (bytes[checked(offset + 1)] << 8));
                colors.Add(FireRedGraphicsDecoder.DecodeBgr555(bgr555, index == 0 ? (byte)0 : (byte)255));
            }

            return colors;
        }

        private static byte[] Expand4BppTiles(byte[] packed)
        {
            var tiles = FireRedGraphicsDecoder.Decode4BppTiles(packed, 0);
            var tilesWide = FireRedRomLayoutRev1.BattleSpriteWidth / IndexedTileDefinition.Width;
            var tilesHigh = FireRedRomLayoutRev1.BattleSpriteHeight / IndexedTileDefinition.Height;
            if (tiles.Count != checked(tilesWide * tilesHigh)) throw new InvalidOperationException("Battle sprite tile count does not match the audited dimensions.");
            var pixels = new byte[checked(FireRedRomLayoutRev1.BattleSpriteWidth * FireRedRomLayoutRev1.BattleSpriteHeight)];
            for (var y = 0; y < FireRedRomLayoutRev1.BattleSpriteHeight; y++)
            {
                for (var x = 0; x < FireRedRomLayoutRev1.BattleSpriteWidth; x++)
                {
                    var tile = tiles[checked(((y / IndexedTileDefinition.Height) * tilesWide) + (x / IndexedTileDefinition.Width))];
                    pixels[checked((y * FireRedRomLayoutRev1.BattleSpriteWidth) + x)] = tile.Pixels[checked(((y % IndexedTileDefinition.Height) * IndexedTileDefinition.Width) + (x % IndexedTileDefinition.Width))];
                }
            }

            return pixels;
        }

        private static string SpriteId(BattleCreatureSpec spec) => spec.Id + "_battle_sprite";

        private static int TypeValue(string type)
        {
            switch (type)
            {
                case "normal": return FireRedRomLayoutRev1.PokemonTypeNormal;
                case "flying": return FireRedRomLayoutRev1.PokemonTypeFlying;
                case "poison": return FireRedRomLayoutRev1.PokemonTypePoison;
                case "grass": return FireRedRomLayoutRev1.PokemonTypeGrass;
                default: throw new InvalidOperationException("Battle creature type is outside the audited whitelist.");
            }
        }

        private static void ExpectPointer(RomReader reader, int fieldOffset, int expectedOffset, string description)
        {
            if (reader.ConvertGbaPointer(reader.ReadUInt32(fieldOffset), 1) != expectedOffset)
            {
                throw new RomReadException(description + " does not match the audited rev1 location.", fieldOffset, FireRedRomLayoutRev1.GbaPointerSize, reader.Length);
            }
        }

        private static void ExpectEqual(RomReader reader, byte actual, int expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 1, reader.Length);
        }

        private static void ExpectEqual(RomReader reader, ushort actual, int expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 2, reader.Length);
        }

        private static void ExpectEqual(RomReader reader, uint actual, uint expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 4, reader.Length);
        }

        private sealed class BattleCreatureSpec
        {
            public BattleCreatureSpec(int speciesId, string id, string displayName, string primaryType, string secondaryType, int hitPoints, int attack, int defense, int speed, int specialAttack, int specialDefense, int frontDataOffset, int backDataOffset, int paletteDataOffset)
            {
                SpeciesId = speciesId; Id = id; DisplayName = displayName; PrimaryType = primaryType; SecondaryType = secondaryType;
                HitPoints = hitPoints; Attack = attack; Defense = defense; Speed = speed; SpecialAttack = specialAttack; SpecialDefense = specialDefense;
                FrontDataOffset = frontDataOffset; BackDataOffset = backDataOffset; PaletteDataOffset = paletteDataOffset;
            }

            public int SpeciesId { get; } public string Id { get; } public string DisplayName { get; } public string PrimaryType { get; } public string SecondaryType { get; }
            public int HitPoints { get; } public int Attack { get; } public int Defense { get; } public int Speed { get; } public int SpecialAttack { get; } public int SpecialDefense { get; }
            public int FrontDataOffset { get; } public int BackDataOffset { get; } public int PaletteDataOffset { get; }
        }
    }
}
