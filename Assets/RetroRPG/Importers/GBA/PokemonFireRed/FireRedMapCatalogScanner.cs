using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>
    /// ROM-backed map catalog discovery for the supported FireRed USA rev1 fingerprint.
    /// It enumerates the map-group pointer table and validates each header/layout before
    /// publishing a descriptor. It does not decode proprietary map content while browsing.
    /// </summary>
    public static class FireRedMapCatalogScanner
    {
        private const int ExpectedMapGroupCount = 43;
        private const int MaximumMapsPerGroup = 128;
        private const int MaximumMapDimension = 512;
        private const int MaximumMapCells = 262144;

        private static readonly string[] GroupNames =
        {
            "Link", "Dungeons", "SpecialArea", "TownsAndRoutes", "IndoorPallet",
            "IndoorViridian", "IndoorPewter", "IndoorCerulean", "IndoorLavender",
            "IndoorVermilion", "IndoorCeladon", "IndoorFuchsia", "IndoorCinnabar",
            "IndoorIndigoPlateau", "IndoorSaffron", "IndoorRoute2", "IndoorRoute4",
            "IndoorRoute5", "IndoorRoute6", "IndoorRoute7", "IndoorRoute8",
            "IndoorRoute10", "IndoorRoute11", "IndoorRoute12", "IndoorRoute15",
            "IndoorRoute16", "IndoorRoute18", "IndoorRoute19", "IndoorRoute22",
            "IndoorRoute23", "IndoorRoute25", "IndoorSevenIsland", "IndoorOneIsland",
            "IndoorTwoIsland", "IndoorThreeIsland", "IndoorFourIsland", "IndoorFiveIsland",
            "IndoorSixIsland", "IndoorThreeIslandRoute", "IndoorFiveIslandRoute",
            "IndoorTwoIslandRoute", "IndoorSixIslandRoute", "IndoorSevenIslandRoute"
        };

        // Public decomp labels are used only as human-friendly/stable metadata. The
        // scanner still validates the actual group/header/layout pointers in the ROM.
        private static readonly string[] TownsAndRoutesNames =
        {
            "PalletTown", "ViridianCity", "PewterCity", "CeruleanCity", "LavenderTown",
            "VermilionCity", "CeladonCity", "FuchsiaCity", "CinnabarIsland",
            "IndigoPlateau_Exterior", "SaffronCity", "SaffronCity_Connection",
            "OneIsland", "TwoIsland", "ThreeIsland", "FourIsland", "FiveIsland",
            "SevenIsland", "SixIsland", "Route1", "Route2", "Route3", "Route4",
            "Route5", "Route6", "Route7", "Route8", "Route9", "Route10", "Route11",
            "Route12", "Route13", "Route14", "Route15", "Route16", "Route17",
            "Route18", "Route19", "Route20", "Route21_North", "Route21_South",
            "Route22", "Route23", "Route24", "Route25", "OneIsland_KindleRoad",
            "OneIsland_TreasureBeach", "TwoIsland_CapeBrink", "ThreeIsland_BondBridge",
            "ThreeIsland_Port", "Prototype_SeviiIsle_6", "Prototype_SeviiIsle_7",
            "Prototype_SeviiIsle_8", "Prototype_SeviiIsle_9", "FiveIsland_ResortGorgeous",
            "FiveIsland_WaterLabyrinth", "FiveIsland_Meadow", "FiveIsland_MemorialPillar",
            "SixIsland_OutcastIsland", "SixIsland_GreenPath", "SixIsland_WaterPath",
            "SixIsland_RuinValley", "SevenIsland_TrainerTower",
            "SevenIsland_SevaultCanyon_Entrance", "SevenIsland_SevaultCanyon",
            "SevenIsland_TanobyRuins"
        };

        private static readonly string[] IndoorPalletNames =
        {
            "PalletTown_PlayersHouse_1F", "PalletTown_PlayersHouse_2F",
            "PalletTown_RivalsHouse", "PalletTown_ProfessorOaksLab"
        };

        private static readonly string[] IndoorViridianNames =
        {
            "ViridianCity_House", "ViridianCity_Gym", "ViridianCity_School",
            "ViridianCity_Mart", "ViridianCity_PokemonCenter_1F",
            "ViridianCity_PokemonCenter_2F"
        };

        private static readonly string[] IndoorPewterNames =
        {
            "PewterCity_Museum_1F", "PewterCity_Museum_2F", "PewterCity_Gym",
            "PewterCity_Mart", "PewterCity_House1", "PewterCity_PokemonCenter_1F",
            "PewterCity_PokemonCenter_2F", "PewterCity_House2"
        };

        private static readonly string[] IndoorRoute2Names =
        {
            "Route2_ViridianForest_SouthEntrance", "Route2_House",
            "Route2_EastBuilding", "Route2_ViridianForest_NorthEntrance"
        };

        public static MapCatalogDefinition Scan(RomReader reader)
        {
            return ScanDetailed(reader).Catalog;
        }

        public static bool IsAuditedMapId(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return false;
            for (var index = 0; index < FireRedRomLayoutRev1.AuditedMapSpecs.Count; index++)
            {
                if (string.Equals(FireRedRomLayoutRev1.AuditedMapSpecs[index].Id, mapId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        internal static FireRedMapCatalogScanResult ScanDetailed(RomReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            reader.EnsureRange(FireRedRomLayoutRev1.MapGroupsTable, 4, "Map-group table is outside ROM bounds.");

            var groupOffsets = DiscoverGroupOffsets(reader);
            if (groupOffsets.Count <= FireRedRomLayoutRev1.PlayersHouse1FMapGroup)
            {
                throw new RomReadException("Map-group table does not contain the verified Pallet Town groups.", FireRedRomLayoutRev1.MapGroupsTable, groupOffsets.Count * 4, reader.Length);
            }
            if (groupOffsets[FireRedRomLayoutRev1.PalletTownMapGroup] != FireRedRomLayoutRev1.TownsAndRoutesMapGroup ||
                groupOffsets[FireRedRomLayoutRev1.PlayersHouse1FMapGroup] != FireRedRomLayoutRev1.IndoorPalletMapGroup)
            {
                throw new RomReadException("Discovered map-group pointers do not match the verified rev1 anchors.", FireRedRomLayoutRev1.MapGroupsTable, groupOffsets.Count * 4, reader.Length);
            }

            var maps = new List<FireRedDiscoveredMapSpec>();
            for (var group = 0; group < groupOffsets.Count; group++)
            {
                var start = groupOffsets[group];
                var end = group + 1 < groupOffsets.Count ? groupOffsets[group + 1] : FireRedRomLayoutRev1.MapGroupsTable;
                if (end <= start || ((end - start) & 3) != 0)
                {
                    throw new RomReadException("Map-group pointer arrays are not contiguous 32-bit pointer tables.", start, Math.Max(0, end - start), reader.Length);
                }

                var mapCount = (end - start) / 4;
                if (mapCount <= 0 || mapCount > MaximumMapsPerGroup)
                {
                    throw new RomReadException("Map-group entry count exceeds the configured safety bound.", start, end - start, reader.Length);
                }

                for (var number = 0; number < mapCount; number++)
                {
                    FireRedDiscoveredMapSpec spec;
                    if (TryReadMapSpec(reader, group, number, start, out spec)) maps.Add(spec);
                }
            }

            if (maps.Count == 0) throw new InvalidOperationException("No valid FireRed map headers were discovered.");

            var descriptors = new List<MapImportDescriptorDefinition>(maps.Count);
            for (var index = 0; index < maps.Count; index++)
            {
                var map = maps[index];
                descriptors.Add(new MapImportDescriptorDefinition(
                    map.Id,
                    map.Name,
                    map.Width,
                    map.Height,
                    map.IsInterior,
                    new string[0],
                    new string[0]));
            }

            return new FireRedMapCatalogScanResult(new MapCatalogDefinition(descriptors), maps, groupOffsets.Count);
        }

        internal static string MapId(int group, int number)
        {
            // Preserve the identifiers already published by the earlier MVPs.
            if (group == FireRedRomLayoutRev1.PalletTownMapGroup && number == FireRedRomLayoutRev1.PalletTownMapNumber) return FireRedRomLayoutRev1.PalletTownMapId;
            if (group == FireRedRomLayoutRev1.Route1MapGroup && number == FireRedRomLayoutRev1.Route1MapNumber) return FireRedRomLayoutRev1.Route1MapId;
            if (group == FireRedRomLayoutRev1.PlayersHouse1FMapGroup)
            {
                if (number == FireRedRomLayoutRev1.PlayersHouse1FMapNumber) return FireRedRomLayoutRev1.PlayersHouse1FMapId;
                if (number == FireRedRomLayoutRev1.PlayersHouse2FMapNumber) return FireRedRomLayoutRev1.PlayersHouse2FMapId;
                if (number == FireRedRomLayoutRev1.RivalsHouseMapNumber) return FireRedRomLayoutRev1.RivalsHouseMapId;
                if (number == FireRedRomLayoutRev1.OakLabMapNumber) return FireRedRomLayoutRev1.OakLabMapId;
            }

            var symbol = KnownMapSymbol(group, number);
            if (!string.IsNullOrEmpty(symbol)) return ToStableMapId(symbol);

            return "MAP_G" + group.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                + "_M" + number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static List<int> DiscoverGroupOffsets(RomReader reader)
        {
            var groups = new List<int>(ExpectedMapGroupCount);
            var previous = -1;
            for (var group = 0; group < ExpectedMapGroupCount; group++)
            {
                var field = checked(FireRedRomLayoutRev1.MapGroupsTable + (group * 4));
                reader.EnsureRange(field, 4, "Map-group table entry is outside ROM bounds.");

                int offset;
                if (!TryConvertPointer(reader, reader.ReadUInt32(field), 4, out offset) ||
                    offset >= FireRedRomLayoutRev1.MapGroupsTable ||
                    (offset & 3) != 0 ||
                    (previous >= 0 && offset <= previous))
                {
                    throw new RomReadException("Map-group table does not match the supported FireRed rev1 structure.", field, 4, reader.Length);
                }

                groups.Add(offset);
                previous = offset;
            }
            return groups;
        }

        private static bool TryReadMapSpec(RomReader reader, int group, int number, int groupPointerOffset, out FireRedDiscoveredMapSpec spec)
        {
            spec = null;
            try
            {
                var headerField = checked(groupPointerOffset + (number * 4));
                var headerOffset = reader.ConvertGbaPointer(reader.ReadUInt32(headerField), FireRedRomLayoutRev1.MapHeaderSize);
                var layoutOffset = reader.ConvertGbaPointer(reader.ReadUInt32(headerOffset), FireRedRomLayoutRev1.MapLayoutSize);

                var width = checked((int)reader.ReadUInt32(layoutOffset));
                var height = checked((int)reader.ReadUInt32(checked(layoutOffset + 4)));
                if (width <= 0 || height <= 0 || width > MaximumMapDimension || height > MaximumMapDimension || checked(width * height) > MaximumMapCells) return false;

                var borderOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(layoutOffset + 8)), 2);
                var mapCellsOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(layoutOffset + 0x0C)), checked(width * height * 2));
                var primaryTilesetOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(layoutOffset + 0x10)), FireRedRomLayoutRev1.TilesetSize);
                var secondaryTilesetOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(layoutOffset + 0x14)), FireRedRomLayoutRev1.TilesetSize);

                var layoutId = reader.ReadUInt16(checked(headerOffset + 0x12));
                if (layoutId > 0)
                {
                    var layoutTableField = checked(FireRedRomLayoutRev1.MapLayoutsTable + ((layoutId - 1) * 4));
                    if (layoutTableField > reader.Length - 4) return false;
                    int tableLayout;
                    if (!TryConvertPointer(reader, reader.ReadUInt32(layoutTableField), FireRedRomLayoutRev1.MapLayoutSize, out tableLayout) || tableLayout != layoutOffset) return false;
                }

                var eventsOffset = 0;
                var rawEvents = reader.ReadUInt32(checked(headerOffset + 4));
                if (rawEvents != 0 && !TryConvertPointer(reader, rawEvents, FireRedRomLayoutRev1.MapEventsSize, out eventsOffset)) return false;

                var connectionsOffset = 0;
                var rawConnections = reader.ReadUInt32(checked(headerOffset + 0x0C));
                if (rawConnections != 0 && !TryConvertPointer(reader, rawConnections, 8, out connectionsOffset)) return false;

                var regionMapSectionId = reader.ReadByte(checked(headerOffset + 0x14));
                var mapType = reader.ReadByte(checked(headerOffset + 0x17));
                var id = MapId(group, number);
                var symbol = KnownMapSymbol(group, number);
                var groupName = group >= 0 && group < GroupNames.Length ? GroupNames[group] : "Group";
                var name = !string.IsNullOrEmpty(symbol)
                    ? HumanizeMapSymbol(symbol)
                    : (groupName + " G" + group.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                        + " M" + number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                        + " / section " + regionMapSectionId.ToString(System.Globalization.CultureInfo.InvariantCulture));

                spec = new FireRedDiscoveredMapSpec(
                    id,
                    name,
                    group,
                    number,
                    groupPointerOffset,
                    headerOffset,
                    layoutOffset,
                    eventsOffset,
                    connectionsOffset,
                    layoutId,
                    width,
                    height,
                    borderOffset,
                    mapCellsOffset,
                    primaryTilesetOffset,
                    secondaryTilesetOffset,
                    regionMapSectionId,
                    mapType);
                return true;
            }
            catch (RomReadException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static string KnownMapSymbol(int group, int number)
        {
            if (group == 3 && number >= 0 && number < TownsAndRoutesNames.Length) return TownsAndRoutesNames[number];
            if (group == 4 && number >= 0 && number < IndoorPalletNames.Length) return IndoorPalletNames[number];
            if (group == 5 && number >= 0 && number < IndoorViridianNames.Length) return IndoorViridianNames[number];
            if (group == 6 && number >= 0 && number < IndoorPewterNames.Length) return IndoorPewterNames[number];
            if (group == 15 && number >= 0 && number < IndoorRoute2Names.Length) return IndoorRoute2Names[number];
            if (group == 1)
            {
                switch (number)
                {
                    case 0: return "ViridianForest";
                    case 1: return "MtMoon_1F";
                    case 2: return "MtMoon_B1F";
                    case 3: return "MtMoon_B2F";
                }
            }
            return null;
        }

        private static string ToStableMapId(string symbol)
        {
            var result = new System.Text.StringBuilder("MAP_");
            for (var index = 0; index < symbol.Length; index++)
            {
                var current = symbol[index];
                if (current == '_')
                {
                    result.Append('_');
                    continue;
                }

                if (index > 0 && char.IsUpper(current) && symbol[index - 1] != '_' &&
                    char.IsLower(symbol[index - 1]))
                {
                    result.Append('_');
                }
                result.Append(char.ToUpperInvariant(current));
            }
            return result.ToString();
        }

        private static string HumanizeMapSymbol(string symbol)
        {
            var result = new System.Text.StringBuilder();
            for (var index = 0; index < symbol.Length; index++)
            {
                var current = symbol[index];
                if (current == '_')
                {
                    result.Append(" / ");
                    continue;
                }

                if (index > 0 && char.IsUpper(current) && symbol[index - 1] != '_' &&
                    char.IsLower(symbol[index - 1]))
                {
                    result.Append(' ');
                }
                result.Append(current);
            }
            return result.ToString();
        }

        private static bool TryConvertPointer(RomReader reader, uint pointer, int requiredLength, out int offset)
        {
            offset = 0;
            if (pointer < RomReader.GbaRomAddressBase || pointer >= RomReader.GbaRomAddressEndExclusive) return false;
            var resolved = (long)pointer - RomReader.GbaRomAddressBase;
            if (resolved < 0 || requiredLength < 0 || resolved > reader.Length || requiredLength > reader.Length - resolved) return false;
            offset = (int)resolved;
            return true;
        }
    }

    internal sealed class FireRedMapCatalogScanResult
    {
        private readonly Dictionary<string, FireRedDiscoveredMapSpec> byId;

        public FireRedMapCatalogScanResult(MapCatalogDefinition catalog, IList<FireRedDiscoveredMapSpec> maps, int groupCount)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            GroupCount = groupCount;
            byId = new Dictionary<string, FireRedDiscoveredMapSpec>(StringComparer.Ordinal);
            var copied = new List<FireRedDiscoveredMapSpec>(maps.Count);
            for (var index = 0; index < maps.Count; index++)
            {
                var map = maps[index] ?? throw new ArgumentException("Discovered maps cannot contain null.", nameof(maps));
                byId.Add(map.Id, map);
                copied.Add(map);
            }
            Maps = new ReadOnlyCollection<FireRedDiscoveredMapSpec>(copied);
        }

        public MapCatalogDefinition Catalog { get; }
        public int GroupCount { get; }
        public IReadOnlyList<FireRedDiscoveredMapSpec> Maps { get; }
        public bool TryGetSpec(string id, out FireRedDiscoveredMapSpec spec) => byId.TryGetValue(id, out spec);
    }

    internal sealed class FireRedDiscoveredMapSpec
    {
        public FireRedDiscoveredMapSpec(
            string id,
            string name,
            int mapGroup,
            int mapNumber,
            int mapGroupPointerOffset,
            int headerOffset,
            int layoutOffset,
            int eventsOffset,
            int connectionsOffset,
            int layoutId,
            int width,
            int height,
            int borderCellsOffset,
            int mapCellsOffset,
            int primaryTilesetHeaderOffset,
            int secondaryTilesetHeaderOffset,
            int regionMapSectionId,
            int mapType)
        {
            Id = id;
            Name = name;
            MapGroup = mapGroup;
            MapNumber = mapNumber;
            MapGroupPointerOffset = mapGroupPointerOffset;
            HeaderOffset = headerOffset;
            LayoutOffset = layoutOffset;
            EventsOffset = eventsOffset;
            ConnectionsOffset = connectionsOffset;
            LayoutId = layoutId;
            Width = width;
            Height = height;
            BorderCellsOffset = borderCellsOffset;
            MapCellsOffset = mapCellsOffset;
            PrimaryTilesetHeaderOffset = primaryTilesetHeaderOffset;
            SecondaryTilesetHeaderOffset = secondaryTilesetHeaderOffset;
            RegionMapSectionId = regionMapSectionId;
            MapType = mapType;
        }

        public string Id { get; }
        public string Name { get; }
        public int MapGroup { get; }
        public int MapNumber { get; }
        public int MapGroupPointerOffset { get; }
        public int HeaderOffset { get; }
        public int LayoutOffset { get; }
        public int EventsOffset { get; }
        public int ConnectionsOffset { get; }
        public int LayoutId { get; }
        public int Width { get; }
        public int Height { get; }
        public int BorderCellsOffset { get; }
        public int MapCellsOffset { get; }
        public int PrimaryTilesetHeaderOffset { get; }
        public int SecondaryTilesetHeaderOffset { get; }
        public int RegionMapSectionId { get; }
        public int MapType { get; }
        public bool IsInterior => MapType == 4 || MapType == 8;
    }
}
