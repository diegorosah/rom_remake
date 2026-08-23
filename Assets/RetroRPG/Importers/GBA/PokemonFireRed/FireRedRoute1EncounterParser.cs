using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Strict, data-only Route 1 encounter decoder for the supported rev1 ROM.</summary>
    internal static class FireRedRoute1EncounterParser
    {
        private static readonly int[] LandWeights = { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 };
        private static readonly int[] LandLevels = { 3, 3, 3, 3, 2, 2, 3, 3, 4, 4, 5, 4 };
        private static readonly int[] LandSpecies = { 16, 19, 16, 19, 16, 19, 16, 19, 16, 19, 16, 19 };

        public static EncounterCatalogDefinition Parse(RomReader reader, MapDefinition route1)
        {
            if (reader == null || route1 == null) throw new ArgumentNullException(reader == null ? nameof(reader) : nameof(route1));
            if (route1.Id != FireRedRomLayoutRev1.Route1MapId || route1.Width != FireRedRomLayoutRev1.Route1Width || route1.Height != FireRedRomLayoutRev1.Route1Height)
            {
                throw new InvalidOperationException("Route 1 encounter parsing requires the audited Route 1 map definition.");
            }

            ValidateRouteHeaderPointers(reader);
            var cells = DecodeAndValidateLandCells(reader, route1);
            var table = DecodeAndValidateLandTable(reader);
            return new EncounterCatalogDefinition(
                new[] { new EncounterZoneDefinition("MAP_ROUTE1:land", FireRedRomLayoutRev1.Route1MapId, EncounterMethod.Land, cells) },
                new[] { table });
        }

        private static List<MapCellCoordinate> DecodeAndValidateLandCells(RomReader reader, MapDefinition route1)
        {
            var cells = new List<MapCellCoordinate>(FireRedRomLayoutRev1.Route1LandCellCount);
            for (var y = 0; y < route1.Height; y++)
            {
                for (var x = 0; x < route1.Width; x++)
                {
                    var index = checked((y * route1.Width) + x);
                    var mapCell = route1.Cells[index];
                    var encounterType = ReadEncounterType(reader, mapCell);
                    var expectedLand = IsExpectedLandCell(x, y);
                    if (encounterType != FireRedRomLayoutRev1.EncounterTypeNone && encounterType != FireRedRomLayoutRev1.EncounterTypeLand)
                    {
                        throw new RomReadException("Route 1 contains a non-audited encounter terrain type.", FireRedRomLayoutRev1.Route1MapCells + (index * 2), 2, reader.Length);
                    }

                    if ((encounterType == FireRedRomLayoutRev1.EncounterTypeLand) != expectedLand)
                    {
                        throw new RomReadException("Route 1 encounter terrain does not match the audited land-cell set.", FireRedRomLayoutRev1.Route1MapCells + (index * 2), 2, reader.Length);
                    }

                    if (expectedLand) cells.Add(new MapCellCoordinate(x, y));
                }
            }

            if (cells.Count != FireRedRomLayoutRev1.Route1LandCellCount) throw new InvalidOperationException("Route 1 land-cell count does not match the audited layout.");
            return cells;
        }

        private static int ReadEncounterType(RomReader reader, MapCellDefinition cell)
        {
            int attributeOffset;
            int metatileIndex;
            if (cell.MetatileId < FireRedRomLayoutRev1.SecondaryMetatileStart)
            {
                metatileIndex = cell.MetatileId;
                if (metatileIndex >= FireRedRomLayoutRev1.PrimaryMetatileCount) throw new InvalidOperationException("Route 1 references an invalid primary metatile.");
                attributeOffset = checked(FireRedRomLayoutRev1.GeneralMetatileAttributes + (metatileIndex * 4));
            }
            else
            {
                metatileIndex = cell.MetatileId - FireRedRomLayoutRev1.SecondaryMetatileStart;
                if (metatileIndex < 0 || metatileIndex >= FireRedRomLayoutRev1.SecondaryMetatileCount) throw new InvalidOperationException("Route 1 references an invalid secondary metatile.");
                attributeOffset = checked(FireRedRomLayoutRev1.PalletTownMetatileAttributes + (metatileIndex * 4));
            }

            reader.EnsureRange(attributeOffset, 4, "Route 1 metatile encounter attributes are outside ROM bounds.");
            return (int)((reader.ReadUInt32(attributeOffset) >> FireRedRomLayoutRev1.EncounterAttributeShift) & FireRedRomLayoutRev1.EncounterAttributeMask);
        }

        private static EncounterTableDefinition DecodeAndValidateLandTable(RomReader reader)
        {
            reader.EnsureRange(FireRedRomLayoutRev1.Route1WildHeader, FireRedRomLayoutRev1.WildPokemonHeaderSize, "Route 1 wild header is outside ROM bounds.");
            Expect(reader, reader.ReadByte(FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderMapGroupOffset), FireRedRomLayoutRev1.Route1MapGroup, "Route 1 wild map group", FireRedRomLayoutRev1.Route1WildHeader);
            Expect(reader, reader.ReadByte(FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderMapNumberOffset), FireRedRomLayoutRev1.Route1MapNumber, "Route 1 wild map number", FireRedRomLayoutRev1.Route1WildHeader + 1);
            ExpectPointer(reader, FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderLandInfoOffset, FireRedRomLayoutRev1.Route1LandInfo, "Route 1 land info");
            Expect(reader, reader.ReadUInt32(FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderWaterInfoOffset), 0u, "Route 1 water method", FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderWaterInfoOffset);
            Expect(reader, reader.ReadUInt32(FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderRockSmashInfoOffset), 0u, "Route 1 rock-smash method", FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderRockSmashInfoOffset);
            Expect(reader, reader.ReadUInt32(FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderFishingInfoOffset), 0u, "Route 1 fishing method", FireRedRomLayoutRev1.Route1WildHeader + FireRedRomLayoutRev1.WildPokemonHeaderFishingInfoOffset);

            reader.EnsureRange(FireRedRomLayoutRev1.Route1LandInfo, FireRedRomLayoutRev1.WildPokemonInfoSize, "Route 1 land info is outside ROM bounds.");
            Expect(reader, reader.ReadByte(FireRedRomLayoutRev1.Route1LandInfo + FireRedRomLayoutRev1.WildPokemonInfoRateOffset), FireRedRomLayoutRev1.Route1LandEncounterRate, "Route 1 encounter rate", FireRedRomLayoutRev1.Route1LandInfo);
            ExpectPointer(reader, FireRedRomLayoutRev1.Route1LandInfo + FireRedRomLayoutRev1.WildPokemonInfoSlotsOffset, FireRedRomLayoutRev1.Route1LandSlots, "Route 1 land slots");
            reader.EnsureRange(FireRedRomLayoutRev1.Route1LandSlots, checked(FireRedRomLayoutRev1.Route1LandSlotCount * FireRedRomLayoutRev1.WildPokemonSlotSize), "Route 1 land slots are outside ROM bounds.");

            var entries = new List<EncounterWeightedEntryDefinition>(FireRedRomLayoutRev1.Route1LandSlotCount);
            var totalWeight = 0;
            for (var slot = 0; slot < FireRedRomLayoutRev1.Route1LandSlotCount; slot++)
            {
                var offset = checked(FireRedRomLayoutRev1.Route1LandSlots + (slot * FireRedRomLayoutRev1.WildPokemonSlotSize));
                var minimumLevel = reader.ReadByte(offset + FireRedRomLayoutRev1.WildPokemonSlotMinimumLevelOffset);
                var maximumLevel = reader.ReadByte(offset + FireRedRomLayoutRev1.WildPokemonSlotMaximumLevelOffset);
                var species = reader.ReadUInt16(offset + FireRedRomLayoutRev1.WildPokemonSlotSpeciesOffset);
                Expect(reader, minimumLevel, LandLevels[slot], "Route 1 slot minimum level", offset);
                Expect(reader, maximumLevel, LandLevels[slot], "Route 1 slot maximum level", offset + 1);
                Expect(reader, species, LandSpecies[slot], "Route 1 slot species", offset + 2);
                totalWeight = checked(totalWeight + LandWeights[slot]);
                entries.Add(new EncounterWeightedEntryDefinition(slot, LandWeights[slot], species, minimumLevel, maximumLevel));
            }

            if (totalWeight != 100) throw new InvalidOperationException("Route 1 encounter weights must total 100.");
            return new EncounterTableDefinition("MAP_ROUTE1:land", FireRedRomLayoutRev1.Route1MapId, EncounterMethod.Land, FireRedRomLayoutRev1.Route1LandEncounterRate, entries);
        }

        private static void ValidateRouteHeaderPointers(RomReader reader)
        {
            ExpectPointer(reader, FireRedRomLayoutRev1.Route1MapHeader + 8, FireRedRomLayoutRev1.Route1Scripts, "Route 1 script table");
            ExpectPointer(reader, FireRedRomLayoutRev1.Route1MapHeader + 12, FireRedRomLayoutRev1.Route1Connections, "Route 1 connections");
        }

        private static bool IsExpectedLandCell(int x, int y)
        {
            if (y >= 6 && y <= 10) return x >= 10 && x <= 21;
            if (y >= 13 && y <= 17) return x >= 16 && x <= 21;
            if (y >= 24 && y <= 28) return x >= 12 && x <= 17;
            if (y >= 32 && y <= 33) return (x >= 4 && x <= 10) || (x >= 17 && x <= 21);
            if (y == 34) return (x >= 2 && x <= 8) || (x >= 15 && x <= 19);
            if (y == 35) return (x >= 2 && x <= 8) || (x >= 12 && x <= 13) || (x >= 15 && x <= 19);
            return y >= 36 && y <= 39 && x >= 12 && x <= 13;
        }

        private static void ExpectPointer(RomReader reader, int fieldOffset, int expectedOffset, string description)
        {
            if (reader.ConvertGbaPointer(reader.ReadUInt32(fieldOffset), 1) != expectedOffset) throw new RomReadException(description + " does not match the audited rev1 location.", fieldOffset, 4, reader.Length);
        }

        private static void Expect(RomReader reader, byte actual, int expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 1, reader.Length);
        }

        private static void Expect(RomReader reader, ushort actual, int expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 2, reader.Length);
        }

        private static void Expect(RomReader reader, uint actual, uint expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the audited rev1 layout.", offset, 4, reader.Length);
        }
    }
}
