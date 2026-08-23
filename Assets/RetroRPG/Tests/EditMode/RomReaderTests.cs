using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;

namespace RetroRPG.Tests.EditMode
{
    public sealed class RomReaderTests
    {
        [Test]
        public void ReadsLittleEndianValuesAtExactBoundary()
        {
            var reader = new RomReader(new byte[] { 0x34, 0x12, 0x78, 0x56 });

            Assert.That(reader.ReadUInt16(0), Is.EqualTo(0x1234));
            Assert.That(reader.ReadUInt16(2), Is.EqualTo(0x5678));
            Assert.That(reader.ReadUInt32(0), Is.EqualTo(0x56781234u));
        }

        [TestCase(-1, 1)]
        [TestCase(4, 1)]
        [TestCase(3, 2)]
        [TestCase(0, -1)]
        public void RejectsInvalidRanges(long offset, long length)
        {
            var reader = new RomReader(new byte[4]);
            Assert.Throws<RomReadException>(() => reader.EnsureRange(offset, length, "test"));
        }

        [Test]
        public void ConvertsGbaPointerAndRejectsNonRomAddresses()
        {
            var reader = new RomReader(new byte[16]);

            Assert.That(reader.ConvertGbaPointer(0x08000008, 4), Is.EqualTo(8));
            Assert.Throws<RomReadException>(() => reader.ConvertGbaPointer(0x07000000));
            Assert.Throws<RomReadException>(() => reader.ConvertGbaPointer(0x08000010));
        }
    }
}

