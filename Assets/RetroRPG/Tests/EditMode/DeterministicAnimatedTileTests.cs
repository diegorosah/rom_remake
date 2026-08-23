using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.Core;
using RetroRPG.Editor;
using RetroRPG.IR;
using RetroRPG.Unity;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RetroRPG.Tests.EditMode
{
    public sealed class DeterministicAnimatedTileTests
    {
        [Test]
        public void Configure_UsesFixedStartTimeAndProvidedFrameRate()
        {
            var texture = new Texture2D(1, 1);
            var first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var second = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var tile = ScriptableObject.CreateInstance<DeterministicAnimatedTile>();
            try
            {
                tile.Configure(new[] { first, second }, 3.75f);
                var animation = new TileAnimationData();

                Assert.That(tile.GetTileAnimationData(Vector3Int.zero, null, ref animation), Is.True);
                Assert.That(tile.FrameCount, Is.EqualTo(2));
                Assert.That(animation.animatedSprites, Is.EqualTo(new[] { first, second }));
                Assert.That(animation.animationSpeed, Is.EqualTo(3.75f));
                Assert.That(animation.animationStartTime, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void DeterministicJson_IsStableAndDoesNotIncludeMachinePaths()
        {
            var map = CreateMinimalPalletMap();
            var report = new ImportReport("PalletTown");
            report.Add(new ParseDiagnostic("Map", DiagnosticSeverity.Info, "Ready", 16, 2));

            var first = PalletTownAssetBuilder.SerializeMapJson(map) + PalletTownAssetBuilder.SerializeReportJson(report);
            var second = PalletTownAssetBuilder.SerializeMapJson(map) + PalletTownAssetBuilder.SerializeReportJson(report);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(first, Does.Contain("\"tilesets\""));
            Assert.That(first, Does.Contain("\"pixels\""));
            Assert.That(first, Does.Contain("\"palettes\""));
            Assert.That(first, Does.Not.Contain("C:\\"));
            Assert.That(first, Does.Not.Contain(":\\"));
        }

        [Test]
        public void Validate_RejectsInconsistentAnimationBeforeAssetCommit()
        {
            var pixels = new byte[IndexedTileDefinition.PixelCount];
            var frames = new List<TileAnimationFrameDefinition>
            {
                new TileAnimationFrameDefinition(new List<IndexedTileDefinition>
                {
                    new IndexedTileDefinition(0, pixels),
                }),
                new TileAnimationFrameDefinition(new List<IndexedTileDefinition>
                {
                    new IndexedTileDefinition(1, pixels),
                }),
            };
            var animations = new List<TileAnimationDefinition>
            {
                new TileAnimationDefinition("inconsistent", 0, 16, frames),
            };
            var map = CreateMinimalPalletMap(animations);

            var exception = Assert.Throws<System.InvalidOperationException>(() => PalletTownAssetBuilder.Validate(map));

            Assert.That(exception.Message, Does.Contain("inconsistent"));
        }

        private static MapDefinition CreateMinimalPalletMap(IList<TileAnimationDefinition> animations = null)
        {
            var pixels = new byte[IndexedTileDefinition.PixelCount];
            var tile = new IndexedTileDefinition(0, pixels);
            var colors = new List<Rgba32>();
            for (var i = 0; i < 16; i++) colors.Add(new Rgba32(0, 0, 0, i == 0 ? (byte)0 : (byte)255));
            var palette = new PaletteDefinition(0, colors);
            var subtiles = new List<SubtileDefinition>();
            for (var i = 0; i < 8; i++) subtiles.Add(new SubtileDefinition(0, 0, false, false));
            var metatile = new MetatileDefinition(0, subtiles, 0, new MetatileLayerRoute(RenderLayer.Bottom, RenderLayer.Middle));
            var primary = new TilesetDefinition("General", false, new List<IndexedTileDefinition> { tile }, new List<PaletteDefinition> { palette }, new List<MetatileDefinition> { metatile }, animations ?? new List<TileAnimationDefinition>());
            var secondary = new TilesetDefinition("PalletTown", true, new List<IndexedTileDefinition>(), new List<PaletteDefinition>(), new List<MetatileDefinition>(), new List<TileAnimationDefinition>());
            var cells = new List<MapCellDefinition>();
            for (var i = 0; i < 480; i++) cells.Add(new MapCellDefinition(0, 0, 0));
            return new MapDefinition("MAP_PALLET_TOWN", "Pallet Town", 24, 20, cells, primary, secondary);
        }
    }
}
