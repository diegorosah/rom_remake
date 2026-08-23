using NUnit.Framework;
using RetroRPG.IR;

namespace RetroRPG.Tests.EditMode
{
    public sealed class MapDefinitionTests
    {
        [Test]
        public void IndexedTileDefensivelyCopiesItsPixels()
        {
            var source = new byte[IndexedTileDefinition.PixelCount];
            source[0] = 3;
            var tile = new IndexedTileDefinition(0, source);
            source[0] = 12;

            Assert.That(tile.Pixels[0], Is.EqualTo(3));
            Assert.Throws<System.NotSupportedException>(() => ((System.Collections.Generic.IList<byte>)tile.Pixels)[0] = 12);
        }
    }
}
