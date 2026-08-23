using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.IR;
using RetroRPG.Runtime;
using UnityEngine;

namespace RetroRPG.Tests.EditMode
{
    public sealed class Mvp4NpcAndPropTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null) UnityEngine.Object.DestroyImmediate(objects[index]);
            }
            objects.Clear();
        }

        [Test]
        public void NpcAndPropDefinitions_ValidatePreviewFlagsRangesAndStableIdentity()
        {
            NpcDefinition fixedNpc = CreateNpc("npc-fixed", 1, 2, 2, NpcMovementPattern.FixedFacing, 2, 2, 2, 2);
            Assert.That(fixedNpc.VisibleByDefault, Is.True);
            Assert.That(fixedNpc.InteractionKey, Is.EqualTo("talk:npc-fixed"));
            Assert.That(fixedNpc.VisibilityKey, Is.EqualTo("visible:npc-fixed"));
            Assert.Throws<ArgumentException>(() => CreateNpc("npc-invalid", 2, 2, 2, NpcMovementPattern.FixedFacing, 1, 2, 2, 2));
            Assert.Throws<ArgumentException>(() => CreateNpc("npc-invalid", 2, 2, 2, NpcMovementPattern.WanderCardinal, 0, 1, 0, 1));
            Assert.Throws<ArgumentException>(() => new NpcDefinition("npc", 1, "sprite", 1, 1, 0, SpriteDirection.South, NpcMovementPattern.FixedFacing, 1, 1, 1, 1, "", "visible", true));

            StaticMapPropDefinition prop = new StaticMapPropDefinition("prop-map", 3, "prop_town_map", 1, 1, 0, SpriteDirection.East, "inspect:map", "prop:map", false);
            Assert.That(prop.VisibleByDefault, Is.False);
            Assert.That(prop.InitialDirection, Is.EqualTo(SpriteDirection.East));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaticMapPropDefinition("prop", 0, "sprite", 1, 1, 0, SpriteDirection.South, "inspect", "visible", true));
        }

        [Test]
        public void MapDefinition_RejectsDuplicateOrOutOfBoundsNpcAndPropOccupancy()
        {
            TilesetDefinition tileset = CreateTileset();
            NpcDefinition npc = CreateNpc("npc", 7, 1, 1, NpcMovementPattern.FixedFacing, 1, 1, 1, 1);
            StaticMapPropDefinition duplicate = new StaticMapPropDefinition("prop", 7, "prop", 2, 2, 0, SpriteDirection.South, "inspect", "visible", true);
            Assert.Throws<ArgumentException>(() => CreateMap(4, 4, new[] { npc }, new[] { duplicate }));

            NpcDefinition outside = CreateNpc("outside", 8, 4, 1, NpcMovementPattern.FixedFacing, 4, 4, 1, 1);
            Assert.Throws<ArgumentException>(() => CreateMap(4, 4, new[] { outside }, new StaticMapPropDefinition[0]));
            StaticMapPropDefinition outsideProp = new StaticMapPropDefinition("outside-prop", 9, "prop", 4, 0, 0, SpriteDirection.South, "inspect", "visible", true);
            Assert.Throws<ArgumentException>(() => CreateMap(4, 4, new NpcDefinition[0], new[] { outsideProp }));
        }

        [Test]
        public void SpriteDefinitions_RejectMalformedFramesAndCatalogCollisions()
        {
            Assert.Throws<ArgumentException>(() => new IndexedSpriteFrameDefinition(0, 2, 2, new byte[] { 0, 1, 2 }));
            Assert.Throws<ArgumentException>(() => new IndexedSpriteFrameDefinition(0, 2, 2, new byte[] { 0, 1, 2, 16 }));
            var frame = new IndexedSpriteFrameDefinition(0, 2, 2, new byte[] { 0, 1, 2, 3 });
            var palette = CreatePalette();
            Assert.Throws<ArgumentException>(() => new StaticSpriteDefinition("prop", 4, 4, palette, new[] { frame }));

            OverworldSpriteDefinition mobile = CreateMobileSprite("shared");
            StaticSpriteDefinition prop = new StaticSpriteDefinition("prop", 2, 2, palette, new[] { frame });
            var catalog = new ObjectSpriteCatalogDefinition(new[] { mobile }, new[] { prop });
            Assert.That(catalog.TryGetMobile("shared", out var foundMobile), Is.True);
            Assert.That(foundMobile, Is.SameAs(mobile));
            Assert.That(catalog.TryGetStatic("prop", out var foundProp), Is.True);
            Assert.That(foundProp, Is.SameAs(prop));
            Assert.Throws<ArgumentException>(() => new ObjectSpriteCatalogDefinition(new[] { mobile, CreateMobileSprite("shared") }, new StaticSpriteDefinition[0]));
            Assert.Throws<ArgumentException>(() => new ObjectSpriteCatalogDefinition(new[] { mobile }, new[] { new StaticSpriteDefinition("shared", 2, 2, palette, new[] { frame }) }));
        }

        [Test]
        public void Occupancy_ReservesTargetsBlocksSwapsAndReleasesOnCancelOrInactiveMap()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 3);
            MapCellOccupancy occupancy = Track(new GameObject("occupancy")).AddComponent<MapCellOccupancy>();
            occupancy.Configure(collision);
            object first = new object();
            object second = new object();
            Assert.That(occupancy.TryRegister(first, new Vector2Int(1, 1)), Is.True);
            Assert.That(occupancy.TryRegister(second, new Vector2Int(2, 1)), Is.True);
            Assert.That(occupancy.TryReserveMove(first, new Vector2Int(1, 1), new Vector2Int(2, 1)), Is.False);
            Assert.That(occupancy.TryReserveMove(first, new Vector2Int(1, 1), new Vector2Int(1, 0)), Is.True);
            Assert.That(occupancy.IsOccupied(new Vector2Int(1, 0)), Is.True);
            Assert.That(occupancy.TryReserveMove(second, new Vector2Int(2, 1), new Vector2Int(2, 0)), Is.True);
            occupancy.CancelMove(first);
            Assert.That(occupancy.IsOccupied(new Vector2Int(1, 0)), Is.False);
            occupancy.SetMapActive(false);
            Assert.That(occupancy.ParticipantCount, Is.Zero);
            Assert.That(occupancy.IsOccupied(new Vector2Int(2, 1)), Is.False);
            Assert.That(occupancy.TryRegister(first, Vector2Int.zero), Is.False);
        }

        [Test]
        public void NpcController_FixedFacingAndVisibilityDoNotConsumeMovementOrOccupancy()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 4);
            MapCellOccupancy occupancy = Track(new GameObject("occupancy")).AddComponent<MapCellOccupancy>();
            occupancy.Configure(collision);
            NpcController npc = Track(new GameObject("npc")).AddComponent<NpcController>();
            npc.Configure("npc-fixed", collision, new Vector2Int(1, 1), 0, null, occupancy, 4f);
            npc.SetMovementPattern(new FixedFacingNpcMovementPattern());
            Assert.That(npc.Face(GridDirection.Left), Is.True);
            Assert.That(npc.Facing, Is.EqualTo(GridDirection.Left));
            Assert.That(npc.TryMove(GridDirection.Right), Is.True);
            npc.Advance(1f);
            Assert.That(npc.CurrentCell, Is.EqualTo(new Vector2Int(2, 1)));
            npc.SetVisible(false);
            Assert.That(occupancy.IsOccupied(npc.CurrentCell), Is.False);
            Assert.That(npc.TryMove(GridDirection.Right), Is.False);
            npc.SetVisible(true);
            Assert.That(occupancy.IsOccupied(npc.CurrentCell), Is.True);
            npc.SetRuntimeActive(false);
            Assert.That(npc.TryMove(GridDirection.Left), Is.False);
        }

        [Test]
        public void NpcController_OccupancyPreventsSimultaneousSwapAndCommitsAfterInterpolation()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 3);
            MapCellOccupancy occupancy = Track(new GameObject("occupancy")).AddComponent<MapCellOccupancy>();
            occupancy.Configure(collision);
            NpcController first = Track(new GameObject("first")).AddComponent<NpcController>();
            NpcController second = Track(new GameObject("second")).AddComponent<NpcController>();
            first.Configure("first", collision, new Vector2Int(1, 1), 0, null, occupancy, 2f);
            second.Configure("second", collision, new Vector2Int(2, 1), 0, null, occupancy, 2f);
            Assert.That(first.TryMove(GridDirection.Right), Is.False, "occupied target cannot be reserved");
            Assert.That(second.TryMove(GridDirection.Left), Is.False, "the reverse swap is also blocked by current occupancy");
            occupancy.Unregister(second);
            Assert.That(first.TryMove(GridDirection.Right), Is.True);
            Assert.That(occupancy.IsOccupied(new Vector2Int(2, 1)), Is.True);
            first.Advance(0.5f);
            Assert.That(first.IsMoving, Is.False);
            Assert.That(first.CurrentCell, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(occupancy.IsOccupied(new Vector2Int(1, 1)), Is.False);
        }

        [Test]
        public void NpcPatterns_AreDeterministicAndTickSpaced()
        {
            GridCollisionMap collision = CreateCollisionMap(5, 5);
            NpcController npc = Track(new GameObject("npc")).AddComponent<NpcController>();
            npc.Configure("wander", collision, new Vector2Int(2, 2), 0, null, null, 4f);
            npc.SetMovementPattern(new DeterministicWanderNpcMovementPattern(3, new QueueRandomSource(3, 0)));
            Assert.That(npc.TryMove(GridDirection.Left), Is.True);
            npc.Advance(1f);
            npc.SetMovementPattern(new DeterministicWanderNpcMovementPattern(3, new QueueRandomSource(2, 1)));
            npc.Tick(1);
            Assert.That(npc.IsMoving, Is.False);
            npc.Tick(3);
            Assert.That(npc.IsMoving, Is.True);
            Assert.That(npc.Facing, Is.EqualTo(GridDirection.Left));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicWanderNpcMovementPattern(0, new QueueRandomSource(0)));
        }

        [Test]
        public void NpcController_WanderCannotLeaveConfiguredInclusiveBounds()
        {
            GridCollisionMap collision = CreateCollisionMap(5, 5);
            NpcController npc = Track(new GameObject("bounded-wander")).AddComponent<NpcController>();
            npc.Configure("bounded-wander", collision, new Vector2Int(2, 2), 0, null, null, 4f);
            npc.ConfigureMovementBounds(new Vector2Int(1, 1), new Vector2Int(2, 2));
            Assert.That(npc.TryMove(GridDirection.Right), Is.False);
            Assert.That(npc.TryMove(GridDirection.Up), Is.False);
            Assert.That(npc.TryMove(GridDirection.Left), Is.True);
            npc.Advance(1f);
            Assert.That(npc.CurrentCell, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(npc.TryMove(GridDirection.Left), Is.False);
        }

        private MapDefinition CreateMap(int width, int height, IList<NpcDefinition> npcs, IList<StaticMapPropDefinition> props)
        {
            var cells = new List<MapCellDefinition>(width * height);
            for (var index = 0; index < width * height; index++) cells.Add(new MapCellDefinition(0, 0, 0));
            return new MapDefinition("map", "Map", width, height, cells, CreateTileset(), CreateTileset(), new WarpDefinition[0], npcs, props);
        }

        private static NpcDefinition CreateNpc(string id, int localId, int x, int y, NpcMovementPattern pattern, int minX, int maxX, int minY, int maxY)
        {
            return new NpcDefinition(id, localId, "npc_sprite", x, y, 0, SpriteDirection.South, pattern, minX, maxX, minY, maxY, "talk:" + id, "visible:" + id, true);
        }

        private static TilesetDefinition CreateTileset()
        {
            return new TilesetDefinition("tiles", false, new List<IndexedTileDefinition>(), new List<PaletteDefinition>(), new List<MetatileDefinition>(), new List<TileAnimationDefinition>());
        }

        private static List<Rgba32> CreatePalette()
        {
            var colors = new List<Rgba32>(16);
            for (var index = 0; index < 16; index++) colors.Add(new Rgba32((byte)index, 0, 0, 255));
            return colors;
        }

        private static OverworldSpriteDefinition CreateMobileSprite(string id)
        {
            var frame = new IndexedSpriteFrameDefinition(0, 2, 2, new byte[] { 0, 1, 2, 3 });
            var animations = new List<DirectionalSpriteAnimationDefinition>();
            for (var direction = 0; direction < 4; direction++)
            {
                animations.Add(new DirectionalSpriteAnimationDefinition((SpriteDirection)direction, SpriteAnimationState.Idle, new[] { new SpriteAnimationStepDefinition(0, false, false, 1) }));
                animations.Add(new DirectionalSpriteAnimationDefinition((SpriteDirection)direction, SpriteAnimationState.Walking, new[] { new SpriteAnimationStepDefinition(0, false, false, 1) }));
            }
            return new OverworldSpriteDefinition(id, 2, 2, CreatePalette(), new[] { frame }, animations);
        }

        private GridCollisionMap CreateCollisionMap(int width, int height)
        {
            GridCollisionMap map = Track(new GameObject("collision")).AddComponent<GridCollisionMap>();
            map.Configure(width, height, new byte[width * height], new byte[width * height], new GridDirectionMask[width * height]);
            return map;
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            objects.Add(unityObject);
            return unityObject;
        }

        private sealed class QueueRandomSource : INpcRandomSource
        {
            private readonly Queue<int> values;
            public QueueRandomSource(params int[] configuredValues) { values = new Queue<int>(configuredValues); }
            public int NextInt(int exclusiveUpperBound)
            {
                if (values.Count == 0) return 0;
                return values.Dequeue() % exclusiveUpperBound;
            }
        }
    }
}
