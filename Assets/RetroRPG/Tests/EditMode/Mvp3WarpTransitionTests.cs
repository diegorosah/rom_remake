using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.IR;
using RetroRPG.Runtime;
using UnityEngine;

namespace RetroRPG.Tests.EditMode
{
    public sealed class Mvp3WarpTransitionTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[index]);
                }
            }
            objects.Clear();
        }

        [Test]
        public void MapDefinition_RejectsDuplicateWarpIdsAndIndexes()
        {
            TilesetDefinition tileset = CreateTileset();
            WarpDefinition first = CreateIrWarp("door-a", 0, "other", 0);
            WarpDefinition duplicateId = CreateIrWarp("door-a", 1, "other", 1);
            WarpDefinition duplicateIndex = CreateIrWarp("door-b", 0, "other", 1);

            Assert.Throws<ArgumentException>(() => CreateMap("map", tileset, first, duplicateId));
            Assert.Throws<ArgumentException>(() => CreateMap("map", tileset, first, duplicateIndex));
        }

        [Test]
        public void MapBundle_OrdersMapsAndResolvesInternalOrExplicitExternalDestinations()
        {
            TilesetDefinition tileset = CreateTileset();
            MapDefinition alpha = CreateMap("alpha", tileset, CreateIrWarp("a", 0, "beta", 0));
            MapDefinition beta = CreateMap("beta", tileset, CreateIrWarp("b", 0, "alpha", 0));
            MapBundleDefinition bundle = new MapBundleDefinition(new[] { beta, alpha });

            Assert.That(bundle.Maps[0].Id, Is.EqualTo("alpha"));
            Assert.That(bundle.TryResolveDestination(alpha.Warps[0], out MapDefinition destination, out WarpDefinition destinationWarp), Is.True);
            Assert.That(destination.Id, Is.EqualTo("beta"));
            Assert.That(destinationWarp.Id, Is.EqualTo("b"));

            MapDefinition external = CreateMap("entry", tileset, CreateIrWarp("entry-warp", 0, "outside", 0));
            Assert.DoesNotThrow(() => new MapBundleDefinition(new[] { external }, new[] { "outside" }));
            Assert.Throws<ArgumentException>(() => new MapBundleDefinition(new[] { external }));
        }

        [Test]
        public void RuntimeMapRoot_RejectsDuplicateWarpIdsAndCatalogRejectsDuplicateMapIds()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 4);
            MapRuntimeWarp first = CreateRuntimeWarp("same", MapRuntimeWarpActivation.Inactive, new Vector2Int(1, 1), GridDirection.Up, "b", "target", new Vector2Int(1, 1), 0, GridDirection.Down);
            MapRuntimeWarp second = CreateRuntimeWarp("same", MapRuntimeWarpActivation.Inactive, new Vector2Int(2, 1), GridDirection.Up, "b", "target", new Vector2Int(2, 1), 0, GridDirection.Down);
            MapRuntimeRoot root = CreateRoot("a", collision);
            Assert.Throws<ArgumentException>(() => root.Configure("a", collision, new[] { first, second }));

            MapRuntimeRoot firstRoot = CreateRoot("same-map", collision);
            MapRuntimeRoot secondRoot = CreateRoot("same-map", collision);
            RuntimeMapCatalog catalog = Track(new GameObject("catalog")).AddComponent<RuntimeMapCatalog>();
            Assert.Throws<ArgumentException>(() => catalog.Configure(new[] { firstRoot, secondRoot }));
        }

        [Test]
        public void RuntimeWarp_InactiveNeverActivatesAndActiveFormsMatchOnlyTheirConfiguredMode()
        {
            MapRuntimeWarp inactive = CreateRuntimeWarp("inactive", MapRuntimeWarpActivation.Inactive, new Vector2Int(1, 1), GridDirection.Up, "b", "target", Vector2Int.zero, 0, GridDirection.Down);
            Assert.That(inactive.MatchesMovement(new Vector2Int(1, 0), GridDirection.Up), Is.False);

            MapRuntimeWarp adjacent = CreateRuntimeWarp("door", MapRuntimeWarpActivation.AdjacentAttempt, new Vector2Int(1, 1), GridDirection.Up, "b", "target", Vector2Int.zero, 0, GridDirection.Down);
            Assert.That(adjacent.MatchesMovement(new Vector2Int(1, 0), GridDirection.Up), Is.True);
            Assert.That(adjacent.MatchesMovement(new Vector2Int(1, 0), GridDirection.Down), Is.False);
            Assert.That(adjacent.MatchesMovement(new Vector2Int(0, 0), GridDirection.Up), Is.False);

            MapRuntimeWarp current = CreateRuntimeWarp("arrow", MapRuntimeWarpActivation.CurrentCellDirection, new Vector2Int(1, 1), GridDirection.Right, "b", "target", Vector2Int.zero, 0, GridDirection.Down);
            Assert.That(current.MatchesMovement(new Vector2Int(1, 1), GridDirection.Right), Is.True);
            Assert.That(current.MatchesMovement(new Vector2Int(1, 0), GridDirection.Right), Is.False);
        }

        [Test]
        public void MapTransition_InterceptsDoorBeforeBlockedCollisionAndRebindsMapCameraAndPlayer()
        {
            GridCollisionMap sourceCollision = CreateCollisionMap(4, 4);
            byte[] blocked = new byte[16];
            blocked[2 + 1 * 4] = 1;
            sourceCollision.Configure(4, 4, blocked, new byte[16], new GridDirectionMask[16]);
            GridCollisionMap destinationCollision = CreateCollisionMap(6, 6);

            MapRuntimeWarp sourceWarp = CreateRuntimeWarp("source-door", MapRuntimeWarpActivation.AdjacentAttempt, new Vector2Int(2, 1), GridDirection.Up, "town-b", "arrival", new Vector2Int(3, 3), 0, GridDirection.Down);
            MapRuntimeWarp arrivalWarp = CreateRuntimeWarp("arrival", MapRuntimeWarpActivation.CurrentCellDirection, new Vector2Int(3, 3), GridDirection.Down, "town-a", "source-door", new Vector2Int(2, 1), 0, GridDirection.Up);
            MapRuntimeRoot source = CreateRoot("town-a", sourceCollision, sourceWarp);
            MapRuntimeRoot destination = CreateRoot("town-b", destinationCollision, arrivalWarp);
            RuntimeMapCatalog catalog = Track(new GameObject("catalog")).AddComponent<RuntimeMapCatalog>();
            catalog.Configure(new[] { source, destination });

            GameObject playerObject = Track(new GameObject("player"));
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.Configure(sourceCollision, new Vector2Int(2, 0), 4, 4f);
            GameObject cameraObject = Track(new GameObject("camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.aspect = 1f;
            PixelPerfectCameraFollow follow = cameraObject.AddComponent<PixelPerfectCameraFollow>();
            follow.Configure(camera, player.transform, sourceCollision.WorldBounds);
            MapTransitionSystem transitions = Track(new GameObject("transitions")).AddComponent<MapTransitionSystem>();
            transitions.Configure(catalog, player, follow, source);

            Assert.That(player.TryMove(GridDirection.Up), Is.False, "the transition consumes the request instead of starting an ordinary grid step");
            Assert.That(transitions.ActiveMap, Is.SameAs(destination));
            Assert.That(source.IsRuntimeActive, Is.False);
            Assert.That(destination.IsRuntimeActive, Is.True);
            Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(player.Elevation, Is.EqualTo(4), "arrival elevation 0 preserves the player's current elevation");
            Assert.That(follow.MapBounds, Is.EqualTo(destinationCollision.WorldBounds));
            Assert.That(camera.transform.position.x * 16f, Is.EqualTo(Mathf.Round(camera.transform.position.x * 16f)).Within(0.001f));
        }

        [Test]
        public void MapTransition_UnresolvedDestinationIsConsumedAndReportsFailure()
        {
            GridCollisionMap collision = CreateCollisionMap(3, 3);
            MapRuntimeWarp sourceWarp = CreateRuntimeWarp("broken", MapRuntimeWarpActivation.CurrentCellDirection, Vector2Int.one, GridDirection.Right, "missing", "target", Vector2Int.zero, 0, GridDirection.Down);
            MapRuntimeRoot source = CreateRoot("source", collision, sourceWarp);
            RuntimeMapCatalog catalog = Track(new GameObject("catalog")).AddComponent<RuntimeMapCatalog>();
            catalog.Configure(new[] { source });
            GameObject playerObject = Track(new GameObject("player"));
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.Configure(collision, Vector2Int.one, 0);
            MapTransitionSystem transitions = Track(new GameObject("transitions")).AddComponent<MapTransitionSystem>();
            transitions.Configure(catalog, player, null, source);

            Assert.That(player.TryMove(GridDirection.Right), Is.False);
            Assert.That(player.CurrentCell, Is.EqualTo(Vector2Int.one));
            Assert.That(transitions.LastFailure, Does.Contain("not registered"));
        }

        [Test]
        public void MapTransition_ArrivalSuppressionAllowsOneOrdinaryDepartureThenReenablesWarp()
        {
            GridCollisionMap mapACollision = CreateCollisionMap(4, 4);
            GridCollisionMap mapBCollision = CreateCollisionMap(5, 5);
            MapRuntimeWarp sourceWarp = CreateRuntimeWarp("source", MapRuntimeWarpActivation.CurrentCellDirection, new Vector2Int(1, 1), GridDirection.Right, "b", "arrival", new Vector2Int(2, 2), 3, GridDirection.Down);
            MapRuntimeWarp arrivalWarp = CreateRuntimeWarp("arrival", MapRuntimeWarpActivation.CurrentCellDirection, new Vector2Int(2, 2), GridDirection.Down, "a", "source", new Vector2Int(1, 1), 2, GridDirection.Up);
            MapRuntimeRoot mapA = CreateRoot("a", mapACollision, sourceWarp);
            MapRuntimeRoot mapB = CreateRoot("b", mapBCollision, arrivalWarp);
            RuntimeMapCatalog catalog = Track(new GameObject("catalog")).AddComponent<RuntimeMapCatalog>();
            catalog.Configure(new[] { mapA, mapB });
            GameObject playerObject = Track(new GameObject("player"));
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.Configure(mapACollision, new Vector2Int(1, 1), 2);
            MapTransitionSystem transitions = Track(new GameObject("transitions")).AddComponent<MapTransitionSystem>();
            transitions.Configure(catalog, player, null, mapA);

            Assert.That(player.TryMove(GridDirection.Right), Is.False, "the transition is immediate and does not start an ordinary grid step");
            Assert.That(transitions.ActiveMap, Is.SameAs(mapB));
            Assert.That(player.Elevation, Is.EqualTo(3));
            Assert.That(player.TryMove(GridDirection.Down), Is.True, "arrival retry is suppressed, so the ordinary departure can start");
            player.Advance(1f);
            Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(player.TryMove(GridDirection.Up), Is.True);
            player.Advance(1f);
            Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(player.TryMove(GridDirection.Down), Is.False, "returning to the arrival cell re-enables and consumes the warp transition");
            Assert.That(transitions.ActiveMap, Is.SameAs(mapA));
        }

        private MapDefinition CreateMap(string id, TilesetDefinition tileset, params WarpDefinition[] warps)
        {
            return new MapDefinition(id, id, 2, 2, new[]
            {
                new MapCellDefinition(0, 0, 0), new MapCellDefinition(0, 0, 0),
                new MapCellDefinition(0, 0, 0), new MapCellDefinition(0, 0, 0),
            }, tileset, tileset, warps);
        }

        private static WarpDefinition CreateIrWarp(string id, int index, string destination, int destinationIndex)
        {
            return new WarpDefinition(id, index, 0, 0, 0, destination, destinationIndex, WarpActivation.DoorNorth, SpriteDirection.Down);
        }

        private static TilesetDefinition CreateTileset()
        {
            return new TilesetDefinition("tiles", false, new List<IndexedTileDefinition>(), new List<PaletteDefinition>(), new List<MetatileDefinition>(), new List<TileAnimationDefinition>());
        }

        private MapRuntimeRoot CreateRoot(string id, GridCollisionMap collision, params MapRuntimeWarp[] warps)
        {
            MapRuntimeRoot root = Track(new GameObject(id)).AddComponent<MapRuntimeRoot>();
            root.Configure(id, collision, warps);
            return root;
        }

        private MapRuntimeWarp CreateRuntimeWarp(string id, MapRuntimeWarpActivation activation, Vector2Int activationCell, GridDirection activationDirection, string destinationMap, string destinationWarp, Vector2Int arrivalCell, byte arrivalElevation, GridDirection arrivalFacing)
        {
            MapRuntimeWarp warp = new MapRuntimeWarp();
            warp.Configure(id, activation, activationCell, activationDirection, destinationMap, destinationWarp, arrivalCell, arrivalElevation, arrivalFacing);
            return warp;
        }

        private GridCollisionMap CreateCollisionMap(int width, int height)
        {
            GameObject mapObject = Track(new GameObject("collision"));
            GridCollisionMap map = mapObject.AddComponent<GridCollisionMap>();
            map.Configure(width, height, new byte[width * height], new byte[width * height], new GridDirectionMask[width * height]);
            return map;
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            objects.Add(unityObject);
            return unityObject;
        }
    }
}
