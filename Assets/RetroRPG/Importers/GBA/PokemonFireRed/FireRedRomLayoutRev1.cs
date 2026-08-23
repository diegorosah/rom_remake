using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    // Verified file offsets for the supported Pokemon FireRed USA revision 1 fingerprint.
    // GBA pointers are file offsets plus 0x08000000.
    public static partial class FireRedRomLayoutRev1
    {
        public const int MapLayoutsTable = 0x34EBFC;
        public const int MapGroupsTable = 0x352718;
        public const int PalletTownLayoutId = 78;
        public const int PalletTownMapGroup = 3;
        public const int PalletTownMapNumber = 0;
        public const int PalletTownMapHeader = 0x350688;
        public const int TownsAndRoutesMapGroup = 0x352364;
        public const int PalletTownMapLayout = 0x2DD530;
        public const int PalletTownEvents = 0x3B4EC0;
        public const int PalletTownScripts = 0x1654D2;
        public const int PalletTownConnections = 0x3527DC;
        public const int PalletTownBorderCells = 0x2DD168;
        public const int PalletTownMapCells = 0x2DD170;
        public const int PalletTownPrimaryTileset = 0x2D4B04;
        public const int PalletTownSecondaryTileset = 0x2D4B1C;

        // MVP 3 selected Pallet Town interior bundle. All values are file offsets
        // verified only for the supported USA revision 1 fingerprint.
        public const int IndoorPalletMapGroup = 0x35246C;
        public const int PlayersHouse1FMapGroup = 4;
        public const int PlayersHouse1FMapNumber = 0;
        public const int PlayersHouse1FMapHeader = 0x350DC0;
        public const int PlayersHouse1FMapLayout = 0x2D5270;
        public const int PlayersHouse1FEvents = 0x3B97BC;
        public const int PlayersHouse1FMapCells = 0x2D516C;
        public const int PlayersHouse1FBorderCells = 0x2D5164;
        public const int PlayersHouse2FMapNumber = 1;
        public const int PlayersHouse2FMapHeader = 0x350DDC;
        public const int PlayersHouse2FMapLayout = 0x2D536C;
        public const int PlayersHouse2FEvents = 0x3B97FC;
        public const int PlayersHouse2FMapCells = 0x2D5294;
        public const int PlayersHouse2FBorderCells = 0x2D528C;
        public const int RivalsHouseMapNumber = 2;
        public const int RivalsHouseMapHeader = 0x350DF8;
        public const int RivalsHouseMapLayout = 0x2D5494;
        public const int RivalsHouseEvents = 0x3B987C;
        public const int RivalsHouseMapCells = 0x2D5390;
        public const int RivalsHouseBorderCells = 0x2D5388;
        public const int OakLabMapNumber = 3;
        public const int OakLabMapHeader = 0x350E14;
        public const int OakLabMapLayout = 0x2D56D8;

        public const string PalletTownMapId = "MAP_PALLET_TOWN";
        public const string PlayersHouse1FMapId = "MAP_PALLET_TOWN_PLAYERS_HOUSE_1F";
        public const string PlayersHouse2FMapId = "MAP_PALLET_TOWN_PLAYERS_HOUSE_2F";
        public const string RivalsHouseMapId = "MAP_PALLET_TOWN_RIVALS_HOUSE";
        public const string OakLabMapId = "MAP_PALLET_TOWN_OAKS_LAB";

        // MVP 6 Route 1. These offsets are valid only after the exact rev1
        // fingerprint gate; no wild-header discovery or arbitrary table scan is used.
        public const string Route1MapId = "MAP_ROUTE1";
        public const int Route1MapGroup = 3;
        public const int Route1MapNumber = 19;
        public const int Route1MapHeader = 0x35089C;
        public const int Route1MapLayout = 0x2E563C;
        public const int Route1Events = 0x3B66B8;
        public const int Route1Scripts = 0x167F75;
        public const int Route1Connections = 0x352A64;
        public const int Route1LayoutId = 89;
        public const int Route1Width = 24;
        public const int Route1Height = 40;
        public const int Route1BorderCells = 0x2E4EB4;
        public const int Route1MapCells = 0x2E4EBC;
        public const int Route1ObjectEventCount = 2;
        public const int Route1WarpCount = 0;
        public const int Route1CoordEventCount = 0;
        public const int Route1BackgroundEventCount = 1;
        public const int Route1WildHeader = 0x3CA3F4;
        public const int Route1LandInfo = 0x3C8F00;
        public const int Route1LandSlots = 0x3C8ED0;
        public const int Route1LandEncounterRate = 21;
        public const int Route1LandCellCount = 178;
        public const int WildPokemonHeaderSize = 20;
        public const int WildPokemonHeaderMapGroupOffset = 0;
        public const int WildPokemonHeaderMapNumberOffset = 1;
        public const int WildPokemonHeaderLandInfoOffset = 4;
        public const int WildPokemonHeaderWaterInfoOffset = 8;
        public const int WildPokemonHeaderRockSmashInfoOffset = 12;
        public const int WildPokemonHeaderFishingInfoOffset = 16;
        public const int WildPokemonInfoSize = 8;
        public const int WildPokemonInfoRateOffset = 0;
        public const int WildPokemonInfoSlotsOffset = 4;
        public const int WildPokemonSlotSize = 4;
        public const int WildPokemonSlotMinimumLevelOffset = 0;
        public const int WildPokemonSlotMaximumLevelOffset = 1;
        public const int WildPokemonSlotSpeciesOffset = 2;
        public const int Route1LandSlotCount = 12;
        public const int EncounterAttributeShift = 24;
        public const uint EncounterAttributeMask = 0x07u;
        public const int EncounterTypeNone = 0;
        public const int EncounterTypeLand = 1;
        public const int EncounterTypeWater = 2;

        public const int GbaPointerBase = 0x08000000;
        public const uint ThumbPointerAddressMask = 0xFFFFFFFEu;
        public const int MapHeaderSize = 0x1C;
        public const int MapLayoutSize = 0x1C;
        public const int TilesetSize = 0x18;
        public const int MapEventsSize = 0x14;
        public const int WarpEventSize = 8;
        public const int ObjectEventSize = 0x18;
        public const int ObjectEventTemplateSize = 0x18;
        public const int ObjectEventGraphicsInfoCount = 152;
        public const int ObjectEventTemplateLocalIdOffset = 0;
        public const int ObjectEventTemplateGraphicsIdOffset = 1;
        public const int ObjectEventTemplateKindOffset = 2;
        public const int ObjectEventTemplateReservedOffset = 3;
        public const int ObjectEventTemplateXOffset = 4;
        public const int ObjectEventTemplateYOffset = 6;
        public const int ObjectEventTemplateElevationOffset = 8;
        public const int ObjectEventTemplateMovementOffset = 9;
        public const int ObjectEventTemplateRangesOffset = 0x0A;
        public const int ObjectEventTemplateTrainerTypeOffset = 0x0B;
        public const int ObjectEventTemplateTrainerRangeOffset = 0x0C;
        public const int ObjectEventTemplateScriptOffset = 0x10;
        public const int ObjectEventTemplateVisibilityFlagOffset = 0x14;
        public const int DialogueMaxTextBytes = 512;
        public const int ScriptOpcodeEnd = 0x02;
        public const int ScriptOpcodeLoadWord = 0x0F;
        public const int ScriptOpcodeCallStd = 0x09;
        public const int ScriptDataSlotZero = 0;
        public const int ScriptStandardMessageBoxNpc = 2;
        public const int FireRedTextEnd = 0xFF;
        public const int FireRedTextNewline = 0xFE;
        public const int FireRedTextPromptScroll = 0xFA;
        public const int FireRedTextPromptClear = 0xFB;
        public const int FireRedTextExtendedControl = 0xFC;
        public const int FireRedTextPlaceholder = 0xFD;
        public const int FatManDialogueScript = 0x1658A7;
        public const int FatManDialogueText = 0x17D885;
        public const int FatManDialogueTextLength = 89;
        public const int TownMapDialogueScript = 0x168FDB;
        public const int TownMapDialogueText = 0x18D7DB;
        public const int TownMapDialogueTextLength = 62;
        public const int CoordEventSize = 0x10;
        public const int BackgroundEventSize = 0x0C;
        public const int MapEventsObjectCountOffset = 0;
        public const int MapEventsWarpCountOffset = 1;
        public const int MapEventsCoordCountOffset = 2;
        public const int MapEventsBackgroundCountOffset = 3;
        public const int MapEventsObjectPointerOffset = 4;
        public const int MapEventsWarpPointerOffset = 8;
        public const int MapEventsCoordPointerOffset = 0x0C;
        public const int MapEventsBackgroundPointerOffset = 0x10;
        public const int WarpEventXOffset = 0;
        public const int WarpEventYOffset = 2;
        public const int WarpEventElevationOffset = 4;
        public const int WarpEventDestinationWarpIndexOffset = 5;
        public const int WarpEventDestinationMapNumberOffset = 6;
        public const int WarpEventDestinationMapGroupOffset = 7;
        public const int PalletTownWidth = 24;
        public const int PalletTownHeight = 20;
        public const int PlayersHouse1FWidth = 13;
        public const int PlayersHouse1FHeight = 10;
        public const int PlayersHouse2FWidth = 12;
        public const int PlayersHouse2FHeight = 9;
        public const int RivalsHouseWidth = 13;
        public const int RivalsHouseHeight = 10;
        public const int PrimaryTileCount = 640;
        public const int SecondaryTileCount = 76;
        public const int PrimaryMetatileCount = 640;
        public const int SecondaryMetatileCount = 89;
        public const int PrimaryPaletteCount = 7;
        public const int SecondaryPaletteCount = 6;
        public const int SecondaryTileStart = 640;
        public const int SecondaryMetatileStart = 640;
        public const int SubtilesPerMetatile = 8;

        public const int GeneralTiles = 0xEA1D68;
        public const int GeneralPalettes = 0xEA1B68;
        public const int GeneralMetatiles = 0x29F738;
        public const int GeneralMetatileAttributes = 0x2A1F38;
        public const uint GeneralAnimationCallback = 0x08070169;

        public const int PalletTownTiles = 0x26D3EC;
        public const int PalletTownPalettes = 0x26D830;
        public const int PalletTownMetatiles = 0x2A2938;
        public const int PalletTownMetatileAttributes = 0x2A2EC8;

        public const int BuildingTileset = 0x2D4C24;
        public const int BuildingTiles = 0x275304;
        public const int BuildingPalettes = 0x277704;
        public const int BuildingMetatiles = 0x2AD824;
        public const int BuildingMetatileAttributes = 0x2B0024;
        public const int GenericBuilding1Tileset = 0x2D4CE4;
        public const int GenericBuilding1Tiles = 0xEA99F4;
        public const int GenericBuilding1Palettes = 0xEA97F4;
        public const int GenericBuilding1Metatiles = 0x2B4EBC;
        public const int GenericBuilding1MetatileAttributes = 0x2B503C;
        public const int GenericBuilding2Tileset = 0x2D4EF4;
        public const int GenericBuilding2Tiles = 0x28E614;
        public const int GenericBuilding2Palettes = 0x28ECE0;
        public const int GenericBuilding2Metatiles = 0x2BEF84;
        public const int GenericBuilding2MetatileAttributes = 0x2BFB04;
        public const int BuildingTileCount = 640;
        public const int BuildingPaletteCount = 7;
        public const int BuildingMetatileCount = 640;
        public const int GenericBuilding1TileCount = 63;
        public const int GenericBuilding1PaletteCount = 6;
        public const int GenericBuilding1MetatileCount = 24;
        public const int GenericBuilding2TileCount = 152;
        public const int GenericBuilding2PaletteCount = 6;
        public const int GenericBuilding2MetatileCount = 184;
        public const int WarpDoorBehavior = 0x69;
        public const int SouthArrowBehavior = 0x65;
        public const int UpRightStairBehavior = 0x6C;
        public const int DownLeftStairBehavior = 0x6F;

        public static readonly int[] GeneralFlowerAnimationFrames = { 0x3A7450, 0x3A74D0, 0x3A7550, 0x3A75D0, 0x3A7650 };
        public static readonly int[] GeneralWaterAnimationFrames = { 0x3A76E4, 0x3A7CE4, 0x3A82E4, 0x3A88E4, 0x3A8EE4, 0x3A94E4, 0x3A9AE4, 0x3AA0E4 };
        public static readonly int[] GeneralSandAnimationFrames = { 0x3AA6E4, 0x3AA924, 0x3AAB64, 0x3AADA4, 0x3AAFE4, 0x3AB224, 0x3AB464, 0x3AB6A4 };

        // Player Red, normal on-foot object-event graphics. All values are verified for
        // the supported FireRed USA revision 1 fingerprint only.
        public const int ObjectEventGraphicsInfoPointerTable = 0x39FE20;
        public const int StandardObjectAnimationTable = 0x3A33D8;
        public const int InanimateObjectAnimationTable = 0x3A3384;
        public const int InanimateObjectAnimationScript = 0x3A29C0;
        public const int ObjectPalette1103Entry = 0x3A51C8;
        public const int ObjectPalette1103Data = 0x36D898;
        public const int ObjectPalette1105Entry = 0x3A51D8;
        public const int ObjectPalette1105Data = 0x36D8D8;
        public const int ObjectPalette1106Entry = 0x3A51E0;
        public const int ObjectPalette1106Data = 0x36D8F8;
        public const int PlayerRedNormalGraphicsInfoPointerIndex = 0;
        public const int PlayerRedNormalGraphicsInfo = 0x3A3C20;
        public const int ObjectEventGraphicsInfoSize = 0x24;
        public const int ObjectEventGraphicsInfoTileTagOffset = 0x00;
        public const int ObjectEventGraphicsInfoPaletteTagOffset = 0x02;
        public const int ObjectEventGraphicsInfoReflectionPaletteTagOffset = 0x04;
        public const int ObjectEventGraphicsInfoAllocationSizeOffset = 0x06;
        public const int ObjectEventGraphicsInfoWidthOffset = 0x08;
        public const int ObjectEventGraphicsInfoHeightOffset = 0x0A;
        public const int ObjectEventGraphicsInfoPaletteAndShadowOffset = 0x0C;
        public const int ObjectEventGraphicsInfoTracksOffset = 0x0D;
        public const int ObjectEventGraphicsInfoOamOffset = 0x10;
        public const int ObjectEventGraphicsInfoSubspriteTablesOffset = 0x14;
        public const int ObjectEventGraphicsInfoAnimationsOffset = 0x18;
        public const int ObjectEventGraphicsInfoImagesOffset = 0x1C;
        public const int ObjectEventGraphicsInfoAffineAnimationsOffset = 0x20;

        public const ushort PlayerRedNormalTileTag = 0xFFFF;
        public const ushort PlayerRedNormalPaletteTag = 0x1100;
        public const ushort PlayerRedNormalReflectionPaletteTag = 0x1102;
        public const ushort PlayerRedNormalAllocationSize = 0x0200;
        public const ushort PlayerRedNormalWidth = 16;
        public const ushort PlayerRedNormalHeight = 32;
        // paletteSlot = PALSLOT_PLAYER (0), shadowSize = SHADOW_SIZE_M (1).
        // The packed bitfield stores shadowSize in bits 4-5.
        public const byte PlayerRedNormalPaletteAndShadow = 0x10;
        public const byte PlayerRedNormalTracks = 0x01;
        public const int PlayerRedNormalOam = 0x3A3780;
        public const int PlayerRedNormalSubspriteTables = 0x3A380C;
        public const int PlayerRedNormalAnimationTable = 0x3A34E0;
        public const int PlayerRedNormalImageTable = 0x3A0110;
        public const int PlayerRedNormalAffineAnimationTable = 0x231D6C;

        public const int SpriteFrameImageSize = 8;
        public const int SpriteFrameImageDataOffset = 0;
        public const int SpriteFrameImageByteSizeOffset = 4;
        public const int PlayerRedNormalFrameCount = 9;
        public const int PlayerRedNormalFrameByteSize = 0x100;
        public const int PlayerRedNormalGraphics = 0x35BBD8;
        public const int PlayerRedNormalGraphicsByteSize = 0x900;
        public const int PlayerRedNormalTilesWide = 2;
        public const int PlayerRedNormalTilesHigh = 4;
        public const int PlayerRedNormalDirectionCount = 4;
        public const int SpriteAnimationPointerCount = 8;
        public const int GbaPointerSize = 4;
        public const int GbaHalfwordSize = 2;
        public const int SpriteAnimationCommandSize = 4;
        public const int SpriteAnimationFrameValueOffset = 0;
        public const int SpriteAnimationFlagsOffset = 2;
        public const ushort SpriteAnimationJumpOpcode = 0xFFFE;
        public const ushort SpriteAnimationJumpTargetZero = 0;
        public const ushort SpriteAnimationDurationMask = 0x003F;
        public const ushort SpriteAnimationHorizontalFlipMask = 0x0040;
        public const ushort SpriteAnimationVerticalFlipMask = 0x0080;
        public const ushort SpriteAnimationAllowedFlagsMask = SpriteAnimationDurationMask | SpriteAnimationHorizontalFlipMask | SpriteAnimationVerticalFlipMask;
        public const int PlayerRedIdleAnimationCommandCount = 2;
        public const int PlayerRedWalkingAnimationCommandCount = 5;
        public const int PlayerRedIdleDurationTicks = 16;
        public const int PlayerRedWalkingDurationTicks = 8;

        public const int ObjectEventPaletteEntry = 0x3A5208;
        public const int SpritePaletteSize = 8;
        public const int SpritePaletteDataOffset = 0;
        public const int SpritePaletteTagOffset = 4;
        public const int PlayerRedNormalPalette = 0x35B9D8;
        public const int PlayerRedNormalPaletteColorCount = 16;
        public const int PlayerRedNormalPaletteByteSize = PlayerRedNormalPaletteColorCount * 2;

        public static readonly IReadOnlyList<PlayerRedAnimationScript> PlayerRedNormalAnimationScripts =
            new ReadOnlyCollection<PlayerRedAnimationScript>(new[]
            {
                new PlayerRedAnimationScript(0, 0x3A2B34, new[] { 0 }, false, false, PlayerRedIdleDurationTicks),
                new PlayerRedAnimationScript(1, 0x3A2B3C, new[] { 1 }, false, false, PlayerRedIdleDurationTicks),
                new PlayerRedAnimationScript(2, 0x3A2B44, new[] { 2 }, false, false, PlayerRedIdleDurationTicks),
                new PlayerRedAnimationScript(3, 0x3A2B4C, new[] { 2 }, true, false, PlayerRedIdleDurationTicks),
                new PlayerRedAnimationScript(4, 0x3A2B54, new[] { 3, 0, 4, 0 }, false, false, PlayerRedWalkingDurationTicks),
                new PlayerRedAnimationScript(5, 0x3A2B68, new[] { 5, 1, 6, 1 }, false, false, PlayerRedWalkingDurationTicks),
                new PlayerRedAnimationScript(6, 0x3A2B7C, new[] { 7, 2, 8, 2 }, false, false, PlayerRedWalkingDurationTicks),
                new PlayerRedAnimationScript(7, 0x3A2B90, new[] { 7, 2, 8, 2 }, true, false, PlayerRedWalkingDurationTicks)
            });

        public sealed class PlayerRedAnimationScript
        {
            public PlayerRedAnimationScript(int tableIndex, int offset, int[] frameIndices, bool horizontalFlip, bool verticalFlip, int durationTicks)
            {
                TableIndex = tableIndex;
                Offset = offset;
                if (frameIndices == null) throw new System.ArgumentNullException(nameof(frameIndices));

                FrameIndices = new ReadOnlyCollection<int>(new List<int>(frameIndices));
                HorizontalFlip = horizontalFlip;
                VerticalFlip = verticalFlip;
                DurationTicks = durationTicks;
            }

            public int TableIndex { get; }
            public int Offset { get; }
            public IReadOnlyList<int> FrameIndices { get; }
            public bool HorizontalFlip { get; }
            public bool VerticalFlip { get; }
            public int DurationTicks { get; }
        }
    }
}
