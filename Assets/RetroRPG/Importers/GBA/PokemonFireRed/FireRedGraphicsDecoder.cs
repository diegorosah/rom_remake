using System;
using System.Collections.Generic;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    public static class FireRedGraphicsDecoder
    {
        public const int BytesPer4BppTile = 32;

        public static List<IndexedTileDefinition> Decode4BppTiles(byte[] source, int tileStartIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (tileStartIndex < 0) throw new ArgumentOutOfRangeException(nameof(tileStartIndex));
            if (source.Length % BytesPer4BppTile != 0) throw new ArgumentException("4bpp tile data is not aligned to 32-byte tiles.", nameof(source));

            var tiles = new List<IndexedTileDefinition>(source.Length / BytesPer4BppTile);
            for (var tile = 0; tile < source.Length / BytesPer4BppTile; tile++)
            {
                var pixels = new byte[IndexedTileDefinition.PixelCount];
                for (var pixelByte = 0; pixelByte < BytesPer4BppTile; pixelByte++)
                {
                    var packed = source[(tile * BytesPer4BppTile) + pixelByte];
                    pixels[pixelByte * 2] = (byte)(packed & 0x0F);
                    pixels[(pixelByte * 2) + 1] = (byte)(packed >> 4);
                }

                tiles.Add(new IndexedTileDefinition(tileStartIndex + tile, pixels));
            }

            return tiles;
        }

        public static Rgba32 DecodeBgr555(ushort value, byte alpha = 255)
        {
            return new Rgba32(
                Expand5Bit(value & 0x1F),
                Expand5Bit((value >> 5) & 0x1F),
                Expand5Bit((value >> 10) & 0x1F),
                alpha);
        }

        private static byte Expand5Bit(int value)
        {
            return (byte)((value << 3) | (value >> 2));
        }
    }
}
