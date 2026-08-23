using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;

namespace RetroRPG.Tests.EditMode
{
    public sealed class GbaHeaderParserTests
    {
        [Test]
        public void ParsesAndValidatesSyntheticHeader()
        {
            var bytes = BuildHeader("POKEMON FIRE", "BPRE", "01", 1);
            var header = GbaHeaderParser.Parse(new RomReader(bytes));

            Assert.That(header.Title, Is.EqualTo("POKEMON FIRE"));
            Assert.That(header.GameCode, Is.EqualTo("BPRE"));
            Assert.That(header.MakerCode, Is.EqualTo("01"));
            Assert.That(header.SoftwareVersion, Is.EqualTo(1));
            Assert.That(header.HasValidFixedValue, Is.True);
            Assert.That(header.HasValidComplementCheck, Is.True);
        }

        [Test]
        public void RejectsTruncatedHeader()
        {
            Assert.Throws<RomReadException>(() =>
                GbaHeaderParser.Parse(new RomReader(new byte[GbaHeaderParser.HeaderLength - 1])));
        }

        [Test]
        public void ReportsInvalidComplementChecksum()
        {
            var bytes = BuildHeader("POKEMON FIRE", "BPRE", "01", 1);
            bytes[0xBD] ^= 0xFF;

            var header = GbaHeaderParser.Parse(new RomReader(bytes));

            Assert.That(header.HasValidComplementCheck, Is.False);
        }

        internal static byte[] BuildHeader(string title, string gameCode, string makerCode, byte version)
        {
            var bytes = new byte[GbaHeaderParser.HeaderLength];
            WriteAscii(bytes, 0xA0, 12, title);
            WriteAscii(bytes, 0xAC, 4, gameCode);
            WriteAscii(bytes, 0xB0, 2, makerCode);
            bytes[0xB2] = 0x96;
            bytes[0xBC] = version;

            var checksum = 0;
            for (var offset = 0xA0; offset <= 0xBC; offset++)
            {
                checksum = (checksum - bytes[offset]) & 0xFF;
            }

            bytes[0xBD] = (byte)((checksum - 0x19) & 0xFF);
            return bytes;
        }

        private static void WriteAscii(byte[] bytes, int offset, int length, string text)
        {
            var encoded = System.Text.Encoding.ASCII.GetBytes(text);
            System.Buffer.BlockCopy(encoded, 0, bytes, offset, System.Math.Min(length, encoded.Length));
        }
    }
}
