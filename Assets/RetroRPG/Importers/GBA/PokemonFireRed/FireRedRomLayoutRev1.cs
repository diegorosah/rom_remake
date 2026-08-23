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
    }
}
