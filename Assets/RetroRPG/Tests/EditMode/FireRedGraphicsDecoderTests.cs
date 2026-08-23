using NUnit.Framework;
using RetroRPG.Importers.GBA.PokemonFireRed;

namespace RetroRPG.Tests.EditMode
{
    public sealed class FireRedGraphicsDecoderTests
    {
        [Test]
        public void Decodes4BppWithLowNibbleAsTheLeftPixel()
        {
            var source = new byte[FireRedGraphicsDecoder.BytesPer4BppTile];
            source[0] = 0xE3;

            var tile = FireRedGraphicsDecoder.Decode4BppTiles(source, 640)[0];

            Assert.That(tile.Index, Is.EqualTo(640));
            Assert.That(tile.Pixels[0], Is.EqualTo(3));
            Assert.That(tile.Pixels[1], Is.EqualTo(14));
        }

        [Test]
        public void DecodesBgr555AndRetainsExplicitAlpha()
        {
            var color = FireRedGraphicsDecoder.DecodeBgr555(0x7FFF, 0);

            Assert.That(color.Red, Is.EqualTo(255));
            Assert.That(color.Green, Is.EqualTo(255));
            Assert.That(color.Blue, Is.EqualTo(255));
            Assert.That(color.Alpha, Is.EqualTo(0));
        }

        [Test]
        public void RejectsMisaligned4BppData()
        {
            Assert.Throws<System.ArgumentException>(() => FireRedGraphicsDecoder.Decode4BppTiles(new byte[31], 0));
        }
    }
}
