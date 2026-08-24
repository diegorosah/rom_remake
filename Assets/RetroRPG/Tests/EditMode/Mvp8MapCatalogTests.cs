using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.Core;
using RetroRPG.Editor;
using RetroRPG.IR;

namespace RetroRPG.Tests.EditMode
{
    /// <summary>Pure contract coverage for the MVP8 map browser/catalog boundary.</summary>
    public sealed class Mvp8MapCatalogTests
    {
        [Test]
        public void Catalog_OrdersMapsAndDependencyIdsOrdinally()
        {
            var catalog = new MapCatalogDefinition(new[]
            {
                Descriptor("zeta", new[] { "alpha", "beta" }, new[] { "outside-z", "outside-a" }),
                Descriptor("alpha", null, null),
                Descriptor("beta", new[] { "gamma" }, null),
                Descriptor("gamma", null, null)
            });

            Assert.That(Ids(catalog.Maps), Is.EqualTo(new[] { "alpha", "beta", "gamma", "zeta" }));
            Assert.That(catalog.GetMap("zeta").RequiredMapIds, Is.EqualTo(new[] { "alpha", "beta" }));
            Assert.That(catalog.GetMap("zeta").ExternalDependencyIds, Is.EqualTo(new[] { "outside-a", "outside-z" }));
        }

        [Test]
        public void Catalog_RejectsEmptyDuplicateUnknownAndInvalidDependencies()
        {
            Assert.Throws<ArgumentException>(() => new MapCatalogDefinition(new MapImportDescriptorDefinition[0]));
            Assert.Throws<ArgumentException>(() => new MapCatalogDefinition(new[] { Descriptor("a", null, null), Descriptor("a", null, null) }));
            Assert.Throws<ArgumentException>(() => Descriptor("a", new[] { "" }, null));
            Assert.Throws<ArgumentException>(() => Descriptor("a", new[] { "b", "b" }, null));
            Assert.Throws<ArgumentException>(() => new MapCatalogDefinition(new[] { Descriptor("a", new[] { "missing" }, null) }));
            Assert.Throws<ArgumentException>(() => new MapCatalogDefinition(new[] { Descriptor("a", null, new[] { "a" }) }));
        }

        [Test]
        public void ResolveClosure_IsTransitiveStableAndCollectsExternalDependencies()
        {
            var catalog = new MapCatalogDefinition(new[]
            {
                Descriptor("root", new[] { "middle" }, new[] { "z-external" }),
                Descriptor("middle", new[] { "leaf" }, new[] { "a-external" }),
                Descriptor("leaf", null, new[] { "z-external" }),
                Descriptor("independent", null, new[] { "unused" })
            });

            var first = catalog.ResolveDependencyClosure(new[] { "root" });
            var second = catalog.ResolveDependencyClosure(new[] { "root" });
            Assert.That(Ids(first), Is.EqualTo(new[] { "leaf", "middle", "root" }));
            Assert.That(Ids(second), Is.EqualTo(Ids(first)));
            Assert.That(catalog.CollectExternalDependencies(new List<MapImportDescriptorDefinition>(first)), Is.EqualTo(new[] { "a-external", "z-external" }));
            Assert.Throws<ArgumentException>(() => catalog.ResolveDependencyClosure(new[] { "root", "root" }));
            Assert.Throws<ArgumentException>(() => catalog.ResolveDependencyClosure(new[] { "unknown" }));
            Assert.Throws<ArgumentException>(() => catalog.ResolveDependencyClosure(new[] { "" }));
        }

        [Test]
        public void SnapshotAndRequest_CopySelectionAndCreateCatalogWithoutAssetWrites()
        {
            var bundle = new MapBundleDefinition(new[] { Map("beta"), Map("alpha") });
            var generatedCatalog = MapAssetImportSnapshot.CreateCatalogFromBundle(bundle);
            Assert.That(Ids(generatedCatalog.Maps), Is.EqualTo(new[] { "alpha", "beta" }));

            var snapshot = new MapAssetImportSnapshot(bundle, Player(), null, null, null, null, new ImportReport("synthetic"), generatedCatalog);
            var request = new MapAssetImportRequest(snapshot, new[] { "beta", "alpha" });
            Assert.That(request.SelectedMapIds, Is.EqualTo(new[] { "alpha", "beta" }));
            Assert.That(request.Snapshot.Catalog, Is.SameAs(generatedCatalog));
            Assert.Throws<ArgumentException>(() => new MapAssetImportRequest(snapshot, new[] { "alpha", "alpha" }));
            Assert.Throws<ArgumentException>(() => new MapAssetImportRequest(snapshot, new[] { "" }));
        }

        private static MapImportDescriptorDefinition Descriptor(string id, IList<string> required, IList<string> external)
        {
            return new MapImportDescriptorDefinition(id, id, 1, 1, false, required ?? new string[0], external ?? new string[0]);
        }

        private static MapDefinition Map(string id)
        {
            var tileset = new TilesetDefinition("tiles", false, new IndexedTileDefinition[0], new PaletteDefinition[0], new MetatileDefinition[0], new TileAnimationDefinition[0]);
            return new MapDefinition(id, id, 1, 1, new[] { new MapCellDefinition(0, 0, 0) }, tileset, tileset);
        }

        private static OverworldSpriteDefinition Player()
        {
            var palette = new List<Rgba32>();
            for (var i = 0; i < 16; i++) palette.Add(new Rgba32(0, 0, 0, 255));
            var frame = new IndexedSpriteFrameDefinition(0, 1, 1, new byte[] { 0 });
            var animations = new List<DirectionalSpriteAnimationDefinition>();
            for (var direction = 0; direction < 4; direction++)
            {
                animations.Add(new DirectionalSpriteAnimationDefinition((SpriteDirection)direction, SpriteAnimationState.Idle, new[] { new SpriteAnimationStepDefinition(0, false, false, 1) }));
                animations.Add(new DirectionalSpriteAnimationDefinition((SpriteDirection)direction, SpriteAnimationState.Walking, new[] { new SpriteAnimationStepDefinition(0, false, false, 1) }));
            }
            return new OverworldSpriteDefinition("player", 1, 1, palette, new[] { frame }, animations);
        }

        private static string[] Ids(IReadOnlyList<MapImportDescriptorDefinition> maps)
        {
            var ids = new string[maps.Count];
            for (var i = 0; i < maps.Count; i++) ids[i] = maps[i].Id;
            return ids;
        }
    }
}
