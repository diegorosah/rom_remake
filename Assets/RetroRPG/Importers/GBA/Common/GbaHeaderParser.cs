using System;
using System.Text;

namespace RetroRPG.Importers.GBA.Common
{
    public static class GbaHeaderParser
    {
        public const int HeaderLength = 0xC0;
        public const int TitleOffset = 0xA0;
        public const int TitleLength = 12;
        public const int GameCodeOffset = 0xAC;
        public const int GameCodeLength = 4;
        public const int MakerCodeOffset = 0xB0;
        public const int MakerCodeLength = 2;

        public static GbaHeader Parse(RomReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            reader.EnsureRange(0, HeaderLength, "ROM is smaller than the complete GBA header.");

            var title = ReadAscii(reader, TitleOffset, TitleLength);
            var gameCode = ReadAscii(reader, GameCodeOffset, GameCodeLength);
            var makerCode = ReadAscii(reader, MakerCodeOffset, MakerCodeLength);
            var fixedValue = reader.ReadByte(0xB2);
            var unitCode = reader.ReadByte(0xB3);
            var softwareVersion = reader.ReadByte(0xBC);
            var complement = reader.ReadByte(0xBD);

            var checksum = 0;
            for (var offset = 0xA0; offset <= 0xBC; offset++)
            {
                checksum = (checksum - reader.ReadByte(offset)) & 0xFF;
            }

            checksum = (checksum - 0x19) & 0xFF;

            return new GbaHeader(
                title,
                gameCode,
                makerCode,
                fixedValue,
                unitCode,
                softwareVersion,
                complement,
                (byte)checksum);
        }

        private static string ReadAscii(RomReader reader, int offset, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(offset, length)).TrimEnd('\0', ' ');
        }
    }
}

