using NUnit.Framework;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.IR;

namespace RetroRPG.Tests.EditMode
{
    public sealed class FireRedMapEncodingTests
    {
        [Test]
        public void DecodesMapCellBitfields()
        {
            var cell = FireRedMapEncoding.DecodeMapCell(0xBA55);

            Assert.That(cell.MetatileId, Is.EqualTo(0x255));
            Assert.That(cell.Collision, Is.EqualTo(2));
            Assert.That(cell.Elevation, Is.EqualTo(0xB));
        }

        [Test]
        public void DecodesSubtileIndexPaletteAndFlips()
        {
            var subtile = FireRedMapEncoding.DecodeSubtile(0xBC55);

            Assert.That(subtile.TileIndex, Is.EqualTo(0x55));
            Assert.That(subtile.PaletteIndex, Is.EqualTo(0xB));
            Assert.That(subtile.HorizontalFlip, Is.True);
            Assert.That(subtile.VerticalFlip, Is.True);
        }

        [TestCase(0u, RenderLayer.Middle, RenderLayer.Top)]
        [TestCase(1u, RenderLayer.Bottom, RenderLayer.Middle)]
        [TestCase(2u, RenderLayer.Bottom, RenderLayer.Top)]
        public void RoutesRenderableMetatileLayers(uint type, RenderLayer first, RenderLayer second)
        {
            var route = FireRedMapEncoding.DecodeLayerRoute(type << 29);

            Assert.That(route.IsRenderable, Is.True);
            Assert.That(route.FirstPlane, Is.EqualTo(first));
            Assert.That(route.SecondPlane, Is.EqualTo(second));
        }

        [Test]
        public void MarksLayerTypeThreeAsInvalid()
        {
            var route = FireRedMapEncoding.DecodeLayerRoute(3u << 29);

            Assert.That(route.IsRenderable, Is.False);
        }
    }
}
