using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Named, audited map and tileset facts for the bounded MVP 3 bundle.</summary>
    public sealed class FireRedMapSpec
    {
        public FireRedMapSpec(string id, string name, int mapGroup, int mapNumber, int mapGroupPointerOffset, int headerOffset, int layoutOffset, int eventsOffset, int layoutId, int width, int height, int borderCellsOffset, int mapCellsOffset, FireRedTilesetSpec primaryTileset, FireRedTilesetSpec secondaryTileset, int objectEventCount, int warpCount, int coordEventCount, int backgroundEventCount, int warpArrayOffset)
            : this(id, name, mapGroup, mapNumber, mapGroupPointerOffset, headerOffset, layoutOffset, eventsOffset, layoutId, width, height, borderCellsOffset, mapCellsOffset, primaryTileset, secondaryTileset, objectEventCount, warpCount, coordEventCount, backgroundEventCount, warpArrayOffset, true)
        {
        }

        public FireRedMapSpec(string id, string name, int mapGroup, int mapNumber, int mapGroupPointerOffset, int headerOffset, int layoutOffset, int eventsOffset, int layoutId, int width, int height, int borderCellsOffset, int mapCellsOffset, FireRedTilesetSpec primaryTileset, FireRedTilesetSpec secondaryTileset, int objectEventCount, int warpCount, int coordEventCount, int backgroundEventCount, int warpArrayOffset, bool importObjectEvents)
        {
            Id = id;
            Name = name;
            MapGroup = mapGroup;
            MapNumber = mapNumber;
            MapGroupPointerOffset = mapGroupPointerOffset;
            HeaderOffset = headerOffset;
            LayoutOffset = layoutOffset;
            EventsOffset = eventsOffset;
            LayoutId = layoutId;
            Width = width;
            Height = height;
            BorderCellsOffset = borderCellsOffset;
            MapCellsOffset = mapCellsOffset;
            PrimaryTileset = primaryTileset;
            SecondaryTileset = secondaryTileset;
            ObjectEventCount = objectEventCount;
            WarpCount = warpCount;
            CoordEventCount = coordEventCount;
            BackgroundEventCount = backgroundEventCount;
            WarpArrayOffset = warpArrayOffset;
            ImportObjectEvents = importObjectEvents;
        }

        public string Id { get; }
        public string Name { get; }
        public int MapGroup { get; }
        public int MapNumber { get; }
        public int MapGroupPointerOffset { get; }
        public int HeaderOffset { get; }
        public int LayoutOffset { get; }
        public int EventsOffset { get; }
        public int LayoutId { get; }
        public int Width { get; }
        public int Height { get; }
        public int BorderCellsOffset { get; }
        public int MapCellsOffset { get; }
        public FireRedTilesetSpec PrimaryTileset { get; }
        public FireRedTilesetSpec SecondaryTileset { get; }
        public int ObjectEventCount { get; }
        public int WarpCount { get; }
        public int CoordEventCount { get; }
        public int BackgroundEventCount { get; }
        public int WarpArrayOffset { get; }
        public bool ImportObjectEvents { get; }
    }

    public sealed class FireRedTilesetSpec
    {
        public FireRedTilesetSpec(string id, bool isSecondary, int headerOffset, int tilesOffset, int palettesOffset, int metatilesOffset, int attributesOffset, uint animationCallback, int tileCount, int paletteCount, int metatileCount, int tileStart, int metatileStart)
        {
            Id = id;
            IsSecondary = isSecondary;
            HeaderOffset = headerOffset;
            TilesOffset = tilesOffset;
            PalettesOffset = palettesOffset;
            MetatilesOffset = metatilesOffset;
            AttributesOffset = attributesOffset;
            AnimationCallback = animationCallback;
            TileCount = tileCount;
            PaletteCount = paletteCount;
            MetatileCount = metatileCount;
            TileStart = tileStart;
            MetatileStart = metatileStart;
        }

        public string Id { get; }
        public bool IsSecondary { get; }
        public int HeaderOffset { get; }
        public int TilesOffset { get; }
        public int PalettesOffset { get; }
        public int MetatilesOffset { get; }
        public int AttributesOffset { get; }
        public uint AnimationCallback { get; }
        public int TileCount { get; }
        public int PaletteCount { get; }
        public int MetatileCount { get; }
        public int TileStart { get; }
        public int MetatileStart { get; }
    }

    public static partial class FireRedRomLayoutRev1
    {
        public static readonly FireRedTilesetSpec GeneralTilesetSpec = new FireRedTilesetSpec("General", false, PalletTownPrimaryTileset, GeneralTiles, GeneralPalettes, GeneralMetatiles, GeneralMetatileAttributes, GeneralAnimationCallback, PrimaryTileCount, PrimaryPaletteCount, PrimaryMetatileCount, 0, 0);
        public static readonly FireRedTilesetSpec PalletTownTilesetSpec = new FireRedTilesetSpec("PalletTown", true, PalletTownSecondaryTileset, PalletTownTiles, PalletTownPalettes, PalletTownMetatiles, PalletTownMetatileAttributes, 0, SecondaryTileCount, SecondaryPaletteCount, SecondaryMetatileCount, SecondaryTileStart, SecondaryMetatileStart);
        public static readonly FireRedTilesetSpec BuildingTilesetSpec = new FireRedTilesetSpec("Building", false, BuildingTileset, BuildingTiles, BuildingPalettes, BuildingMetatiles, BuildingMetatileAttributes, 0, BuildingTileCount, BuildingPaletteCount, BuildingMetatileCount, 0, 0);
        public static readonly FireRedTilesetSpec GenericBuilding1TilesetSpec = new FireRedTilesetSpec("GenericBuilding1", true, GenericBuilding1Tileset, GenericBuilding1Tiles, GenericBuilding1Palettes, GenericBuilding1Metatiles, GenericBuilding1MetatileAttributes, 0, GenericBuilding1TileCount, GenericBuilding1PaletteCount, GenericBuilding1MetatileCount, SecondaryTileStart, SecondaryMetatileStart);
        public static readonly FireRedTilesetSpec GenericBuilding2TilesetSpec = new FireRedTilesetSpec("GenericBuilding2", true, GenericBuilding2Tileset, GenericBuilding2Tiles, GenericBuilding2Palettes, GenericBuilding2Metatiles, GenericBuilding2MetatileAttributes, 0, GenericBuilding2TileCount, GenericBuilding2PaletteCount, GenericBuilding2MetatileCount, SecondaryTileStart, SecondaryMetatileStart);

        public static readonly IReadOnlyList<FireRedMapSpec> SelectedMapSpecs = new ReadOnlyCollection<FireRedMapSpec>(new[]
        {
            new FireRedMapSpec(PalletTownMapId, "Pallet Town", PalletTownMapGroup, PalletTownMapNumber, TownsAndRoutesMapGroup, PalletTownMapHeader, PalletTownMapLayout, PalletTownEvents, PalletTownLayoutId, PalletTownWidth, PalletTownHeight, PalletTownBorderCells, PalletTownMapCells, GeneralTilesetSpec, PalletTownTilesetSpec, 3, 3, 3, 5, 0x3B4E3C),
            new FireRedMapSpec(PlayersHouse1FMapId, "Player's House 1F", PlayersHouse1FMapGroup, PlayersHouse1FMapNumber, IndoorPalletMapGroup, PlayersHouse1FMapHeader, PlayersHouse1FMapLayout, PlayersHouse1FEvents, 1, PlayersHouse1FWidth, PlayersHouse1FHeight, PlayersHouse1FBorderCells, PlayersHouse1FMapCells, BuildingTilesetSpec, GenericBuilding1TilesetSpec, 1, 4, 0, 1, 0x3B9790),
            new FireRedMapSpec(PlayersHouse2FMapId, "Player's House 2F", PlayersHouse1FMapGroup, PlayersHouse2FMapNumber, IndoorPalletMapGroup, PlayersHouse2FMapHeader, PlayersHouse2FMapLayout, PlayersHouse2FEvents, 2, PlayersHouse2FWidth, PlayersHouse2FHeight, PlayersHouse2FBorderCells, PlayersHouse2FMapCells, BuildingTilesetSpec, GenericBuilding1TilesetSpec, 0, 1, 0, 3, 0x3B97D0),
            new FireRedMapSpec(RivalsHouseMapId, "Rival's House", PlayersHouse1FMapGroup, RivalsHouseMapNumber, IndoorPalletMapGroup, RivalsHouseMapHeader, RivalsHouseMapLayout, RivalsHouseEvents, 3, RivalsHouseWidth, RivalsHouseHeight, RivalsHouseBorderCells, RivalsHouseMapCells, BuildingTilesetSpec, GenericBuilding2TilesetSpec, 2, 3, 0, 3, 0x3B9840)
        });

        public static readonly FireRedMapSpec Route1MapSpec = new FireRedMapSpec(
            Route1MapId,
            "Route 1",
            Route1MapGroup,
            Route1MapNumber,
            TownsAndRoutesMapGroup,
            Route1MapHeader,
            Route1MapLayout,
            Route1Events,
            Route1LayoutId,
            Route1Width,
            Route1Height,
            Route1BorderCells,
            Route1MapCells,
            GeneralTilesetSpec,
            PalletTownTilesetSpec,
            Route1ObjectEventCount,
            Route1WarpCount,
            Route1CoordEventCount,
            Route1BackgroundEventCount,
            0,
            false);

        /// <summary>Every map whose exact binary structure is currently audited for this importer.</summary>
        public static readonly IReadOnlyList<FireRedMapSpec> AuditedMapSpecs = new ReadOnlyCollection<FireRedMapSpec>(new[]
        {
            SelectedMapSpecs[0],
            SelectedMapSpecs[1],
            SelectedMapSpecs[2],
            SelectedMapSpecs[3],
            Route1MapSpec
        });
    }
}
