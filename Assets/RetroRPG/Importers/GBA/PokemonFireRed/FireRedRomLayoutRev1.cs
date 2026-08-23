using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    // Verified file offsets for the supported Pokemon FireRed USA revision 1 fingerprint.
    // GBA pointers are file offsets plus 0x08000000.
    public static class FireRedRomLayoutRev1
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

        public const int GbaPointerBase = 0x08000000;
        public const uint ThumbPointerAddressMask = 0xFFFFFFFEu;
        public const int MapHeaderSize = 0x1C;
        public const int MapLayoutSize = 0x1C;
        public const int TilesetSize = 0x18;
        public const int PalletTownWidth = 24;
        public const int PalletTownHeight = 20;
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

        public static readonly int[] GeneralFlowerAnimationFrames = { 0x3A7450, 0x3A74D0, 0x3A7550, 0x3A75D0, 0x3A7650 };
        public static readonly int[] GeneralWaterAnimationFrames = { 0x3A76E4, 0x3A7CE4, 0x3A82E4, 0x3A88E4, 0x3A8EE4, 0x3A94E4, 0x3A9AE4, 0x3AA0E4 };
        public static readonly int[] GeneralSandAnimationFrames = { 0x3AA6E4, 0x3AA924, 0x3AAB64, 0x3AADA4, 0x3AAFE4, 0x3AB224, 0x3AB464, 0x3AB6A4 };

        // Player Red, normal on-foot object-event graphics. All values are verified for
        // the supported FireRed USA revision 1 fingerprint only.
        public const int ObjectEventGraphicsInfoPointerTable = 0x39FE20;
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
