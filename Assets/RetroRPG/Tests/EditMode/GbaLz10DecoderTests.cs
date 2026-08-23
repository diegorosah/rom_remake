using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;

namespace RetroRPG.Tests.EditMode
{
    public sealed class GbaLz10DecoderTests
    {
        [Test]
        public void DecodesLiteralStream()
        {
            var stream = new byte[] { 0x10, 3, 0, 0, 0x00, 0x2A, 0x7B, 0xC1 };

            Assert.That(GbaLz10Decoder.Decode(new RomReader(stream), 0, 3), Is.EqualTo(new byte[] { 0x2A, 0x7B, 0xC1 }));
        }

        [Test]
        public void RejectsDeclaredOutputAboveConfiguredLimit()
        {
            var stream = new byte[] { 0x10, 4, 0, 0 };

            Assert.Throws<RomReadException>(() => GbaLz10Decoder.Decode(new RomReader(stream), 0, 3));
        }

        [Test]
        public void RejectsTruncatedLiteral()
        {
            var stream = new byte[] { 0x10, 1, 0, 0, 0x00 };

            Assert.Throws<RomReadException>(() => GbaLz10Decoder.Decode(new RomReader(stream), 0, 1));
        }

        [Test]
        public void RejectsBackReferenceBeforeOutput()
        {
            var stream = new byte[] { 0x10, 3, 0, 0, 0x80, 0x00, 0x00 };

            Assert.Throws<RomReadException>(() => GbaLz10Decoder.Decode(new RomReader(stream), 0, 3));
        }
    }
}
