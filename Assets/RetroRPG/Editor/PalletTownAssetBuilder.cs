using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RetroRPG.Core;
using RetroRPG.IR;
using RetroRPG.Runtime;
using RetroRPG.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RetroRPG.Editor
{
    /// <summary>Converts validated, game-agnostic map IR into deterministic Unity assets.</summary>
    public static class PalletTownAssetBuilder
    {
        public const string OutputRoot = "Assets/Imported/FireRed/rev1/PalletTown";
        private const string ManifestPath = OutputRoot + "/ImportManifest.json";
        private const string IrPath = OutputRoot + "/PalletTown.ir.json";
        private const string ReportPath = OutputRoot + "/ImportReport.json";
        private const string ScenePath = OutputRoot + "/PalletTown.unity";
        private const float CellsPerWorldUnit = 2f;
        private const float PixelTicksPerSecond = 60f;
        private static readonly Vector2Int PlayerSpawnCell = new Vector2Int(6, 6);

        [Serializable]
        private sealed class ImportManifest
        {
            public int schemaVersion = 3;
            public string[] ownedAssets = Array.Empty<string>();
        }

        private readonly struct TileKey : IEquatable<TileKey>
        {
            public TileKey(int tileIndex, int paletteIndex, bool horizontalFlip, bool verticalFlip)
            {
                TileIndex = tileIndex;
                PaletteIndex = paletteIndex;
                HorizontalFlip = horizontalFlip;
                VerticalFlip = verticalFlip;
            }

            public int TileIndex { get; }
            public int PaletteIndex { get; }
            public bool HorizontalFlip { get; }
            public bool VerticalFlip { get; }

            public string StableName => string.Format(
                CultureInfo.InvariantCulture,
                "tile_{0:D4}_pal_{1:D2}_h{2}_v{3}",
                TileIndex,
                PaletteIndex,
                HorizontalFlip ? 1 : 0,
                VerticalFlip ? 1 : 0);

            public bool Equals(TileKey other)
            {
                return TileIndex == other.TileIndex
                    && PaletteIndex == other.PaletteIndex
                    && HorizontalFlip == other.HorizontalFlip
                    && VerticalFlip == other.VerticalFlip;
            }

            public override bool Equals(object obj) => obj is TileKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TileIndex;
                    hash = (hash * 397) ^ PaletteIndex;
                    hash = (hash * 397) ^ (HorizontalFlip ? 1 : 0);
                    return (hash * 397) ^ (VerticalFlip ? 1 : 0);
                }
            }
        }

        private readonly struct PlayerFrameKey : IEquatable<PlayerFrameKey>
        {
            public PlayerFrameKey(int frameIndex, bool horizontalFlip, bool verticalFlip)
            {
                FrameIndex = frameIndex;
                HorizontalFlip = horizontalFlip;
                VerticalFlip = verticalFlip;
            }

            public int FrameIndex { get; }
            public bool HorizontalFlip { get; }
            public bool VerticalFlip { get; }

            public string StableName => string.Format(
                CultureInfo.InvariantCulture,
                "player_frame_{0:D2}_h{1}_v{2}",
                FrameIndex,
                HorizontalFlip ? 1 : 0,
                VerticalFlip ? 1 : 0);

            public bool Equals(PlayerFrameKey other)
            {
                return FrameIndex == other.FrameIndex
                    && HorizontalFlip == other.HorizontalFlip
                    && VerticalFlip == other.VerticalFlip;
            }

            public override bool Equals(object obj) => obj is PlayerFrameKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = FrameIndex;
                    hash = (hash * 397) ^ (HorizontalFlip ? 1 : 0);
                    return (hash * 397) ^ (VerticalFlip ? 1 : 0);
                }
            }
        }

        public static void Validate(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.Width != 24 || map.Height != 20 || map.Cells.Count != 480)
            {
                throw new InvalidOperationException("Pallet Town IR must contain a 24x20 grid with 480 cells.");
            }

            ValidateMapContent(map);
        }

        private static void ValidateMapContent(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var metatiles = BuildMetatileLookup(map);
            var tiles = BuildTileLookup(map);
            var palettes = BuildPaletteLookup(map);
            ValidateAnimations(map, tiles);
            for (var cellIndex = 0; cellIndex < map.Cells.Count; cellIndex++)
            {
                var cell = map.Cells[cellIndex];
                if (!metatiles.TryGetValue(cell.MetatileId, out var metatile) || !metatile.LayerRoute.IsRenderable)
                {
                    throw new InvalidOperationException("Map cell " + cellIndex + " has no renderable metatile.");
                }

                for (var subtileIndex = 0; subtileIndex < metatile.Subtiles.Count; subtileIndex++)
                {
                    var subtile = metatile.Subtiles[subtileIndex];
                    if (!tiles.ContainsKey(subtile.TileIndex))
                    {
                        throw new InvalidOperationException("Metatile " + metatile.Index + " references an unavailable tile.");
                    }

                    if (!palettes.ContainsKey(subtile.PaletteIndex))
                    {
                        throw new InvalidOperationException("Metatile " + metatile.Index + " references an unavailable palette.");
                    }
                }
            }
        }

        /// <summary>
        /// Validates the complete input snapshot used by the MVP2 importer. This
        /// happens before the output folder or any asset is touched.
        /// </summary>
        public static void Validate(MapDefinition map, OverworldSpriteDefinition playerSprite)
        {
            Validate(map);
            ValidatePlayerSprite(playerSprite);
            ValidatePlayerSpawn(map);
        }

        /// <summary>
        /// Checks every map and link in an MVP3 snapshot before any generated asset is
        /// written.  The editor is deliberately the only layer that understands how
        /// IR becomes a Unity scene; the runtime only receives serialized components.
        /// </summary>
        public static void Validate(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (bundle.Maps == null || bundle.Maps.Count == 0)
            {
                throw new InvalidOperationException("Map bundle contains no maps.");
            }

            var foundPalletTown = false;
            for (var mapIndex = 0; mapIndex < bundle.Maps.Count; mapIndex++)
            {
                var map = bundle.Maps[mapIndex];
                ValidateMapContent(map);
                if (string.Equals(map.Id, "MAP_PALLET_TOWN", StringComparison.Ordinal))
                {
                    Validate(map);
                    ValidatePlayerSpawn(map);
                    foundPalletTown = true;
                }

                for (var warpIndex = 0; warpIndex < map.Warps.Count; warpIndex++)
                {
                    var warp = map.Warps[warpIndex];
                    if (warp == null || string.IsNullOrWhiteSpace(warp.Id) ||
                        warp.SourceX < 0 || warp.SourceX >= map.Width ||
                        warp.SourceY < 0 || warp.SourceY >= map.Height)
                    {
                        throw new InvalidOperationException("Map bundle contains an invalid warp.");
                    }
                }
            }

            if (!foundPalletTown)
            {
                throw new InvalidOperationException("MVP3 map bundle must include MAP_PALLET_TOWN.");
            }

            ValidatePlayerSprite(playerSprite);
        }

        private static void ValidatePlayerSprite(OverworldSpriteDefinition playerSprite)
        {
            if (playerSprite == null)
            {
                throw new ArgumentNullException(nameof(playerSprite));
            }

            if (string.IsNullOrWhiteSpace(playerSprite.Id) || playerSprite.Palette == null ||
                playerSprite.Palette.Count != OverworldSpriteDefinition.PaletteColorCount ||
                playerSprite.Frames == null || playerSprite.Frames.Count == 0 ||
                playerSprite.Animations == null || playerSprite.Animations.Count != OverworldSpriteDefinition.RequiredAnimationCount)
            {
                throw new InvalidOperationException("Player sprite IR is incomplete.");
            }

            var frameIds = new HashSet<int>();
            for (var frameIndex = 0; frameIndex < playerSprite.Frames.Count; frameIndex++)
            {
                var frame = playerSprite.Frames[frameIndex];
                if (frame == null || frame.Width != playerSprite.Width || frame.Height != playerSprite.Height ||
                    frame.Pixels == null || frame.Pixels.Count != checked(frame.Width * frame.Height) || !frameIds.Add(frame.Index))
                {
                    throw new InvalidOperationException("Player sprite frames are incomplete or inconsistent.");
                }
            }

            var animationKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var animationIndex = 0; animationIndex < playerSprite.Animations.Count; animationIndex++)
            {
                var animation = playerSprite.Animations[animationIndex];
                if (animation == null || animation.Steps == null || animation.Steps.Count == 0)
                {
                    throw new InvalidOperationException("Player sprite animation is incomplete.");
                }

                var key = ((int)animation.Direction).ToString(CultureInfo.InvariantCulture)
                    + ":" + ((int)animation.State).ToString(CultureInfo.InvariantCulture);
                if (!animationKeys.Add(key))
                {
                    throw new InvalidOperationException("Player sprite has duplicate directional animations.");
                }

                var duration = animation.Steps[0].DurationTicks;
                for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                {
                    var step = animation.Steps[stepIndex];
                    if (!frameIds.Contains(step.FrameIndex) || step.DurationTicks <= 0 || step.DurationTicks != duration)
                    {
                        throw new InvalidOperationException("Player sprite animation has an invalid frame or nonuniform duration.");
                    }
                }
            }

            for (var directionValue = (int)SpriteDirection.South; directionValue <= (int)SpriteDirection.East; directionValue++)
            {
                var direction = (SpriteDirection)directionValue;
                for (var stateValue = (int)SpriteAnimationState.Idle; stateValue <= (int)SpriteAnimationState.Walking; stateValue++)
                {
                    var state = (SpriteAnimationState)stateValue;
                    var key = ((int)direction).ToString(CultureInfo.InvariantCulture)
                        + ":" + ((int)state).ToString(CultureInfo.InvariantCulture);
                    if (!animationKeys.Contains(key))
                    {
                        throw new InvalidOperationException("Player sprite is missing a required directional animation.");
                    }
                }
            }
        }

        private static void ValidatePlayerSpawn(MapDefinition map)
        {
            // The source map is top-down, while the runtime collision map is bottom-up.
            const int sourceSpawnX = 6;
            const int sourceSpawnY = 13;
            var sourceIndex = (sourceSpawnY * map.Width) + sourceSpawnX;
            var sourceCell = map.Cells[sourceIndex];
            if (sourceCell.Collision != 0 || sourceCell.Elevation != 3 || PlayerSpawnCell.y != map.Height - 1 - sourceSpawnY)
            {
                throw new InvalidOperationException("The verified Pallet Town player spawn must be walkable at elevation 3.");
            }
        }

        private static void ValidateAnimations(MapDefinition map, IDictionary<int, IndexedTileDefinition> tiles)
        {
            var claimedTileIndices = new HashSet<int>();
            ValidateAnimations(map.PrimaryTileset, tiles, claimedTileIndices);
            ValidateAnimations(map.SecondaryTileset, tiles, claimedTileIndices);
        }

        private static void ValidateAnimations(
            TilesetDefinition tileset,
            IDictionary<int, IndexedTileDefinition> tiles,
            ISet<int> claimedTileIndices)
        {
            for (var animationIndex = 0; animationIndex < tileset.Animations.Count; animationIndex++)
            {
                var animation = tileset.Animations[animationIndex];
                if (animation.Frames.Count == 0 || animation.Frames[0].Tiles.Count == 0)
                {
                    throw new InvalidOperationException("Animation " + animation.Id + " has no frame tiles.");
                }

                var tileCount = animation.Frames[0].Tiles.Count;
                for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
                {
                    var frameTiles = animation.Frames[frameIndex].Tiles;
                    if (frameTiles.Count != tileCount)
                    {
                        throw new InvalidOperationException("Animation " + animation.Id + " has inconsistent frame widths.");
                    }

                    for (var tileOffset = 0; tileOffset < tileCount; tileOffset++)
                    {
                        var expectedTileIndex = animation.DestinationTileIndex + tileOffset;
                        if (frameTiles[tileOffset].Index != expectedTileIndex)
                        {
                            throw new InvalidOperationException(
                                "Animation " + animation.Id + " frame " + frameIndex
                                + " does not target the declared contiguous tile range.");
                        }

                        if (!tiles.ContainsKey(expectedTileIndex))
                        {
                            throw new InvalidOperationException(
                                "Animation " + animation.Id + " targets unavailable tile " + expectedTileIndex + ".");
                        }
                    }
                }

                for (var tileOffset = 0; tileOffset < tileCount; tileOffset++)
                {
                    var tileIndex = animation.DestinationTileIndex + tileOffset;
                    if (!claimedTileIndices.Add(tileIndex))
                    {
                        throw new InvalidOperationException("Multiple animations target tile " + tileIndex + ".");
                    }
                }
            }
        }

        /// <summary>Imports only after the parser has emitted and this builder has validated a complete IR snapshot.</summary>
        public static void Import(
            MapDefinition map,
            OverworldSpriteDefinition playerSprite,
            ImportReport report,
            Func<string, float, bool> shouldCancel)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var externalDestinations = new List<string>();
            for (var i = 0; i < map.Warps.Count; i++)
            {
                var destination = map.Warps[i].DestinationMapId;
                if (!string.Equals(destination, map.Id, StringComparison.Ordinal) && !externalDestinations.Contains(destination))
                {
                    externalDestinations.Add(destination);
                }
            }

            Import(new MapBundleDefinition(new[] { map }, externalDestinations), playerSprite, report, shouldCancel);
        }

        /// <summary>Imports the bounded Pallet Town transition bundle into one persistent scene.</summary>
        public static void Import(
            MapBundleDefinition bundle,
            OverworldSpriteDefinition playerSprite,
            ImportReport report,
            Func<string, float, bool> shouldCancel)
        {
            Import(bundle, playerSprite, null, report, shouldCancel);
        }

        /// <summary>Imports the bounded map, player and object-event snapshot after complete validation.</summary>
        public static void Import(
            MapBundleDefinition bundle,
            OverworldSpriteDefinition playerSprite,
            ObjectSpriteCatalogDefinition objectSprites,
            ImportReport report,
            Func<string, float, bool> shouldCancel)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.HasErrors) throw new InvalidOperationException("An import report with errors cannot generate assets.");
            ThrowIfCancelled(shouldCancel, "Validating IR", 0.05f);
            Validate(bundle, playerSprite);
            if (objectSprites != null) ObjectEventAssetBuilder.Validate(bundle, objectSprites);

            // All object discovery and JSON construction completes before generated assets are touched.
            var contexts = new List<BuildContext>(bundle.Maps.Count);
            for (var mapIndex = 0; mapIndex < bundle.Maps.Count; mapIndex++)
            {
                var map = bundle.Maps[mapIndex];
                contexts.Add(new BuildContext(map, playerSprite, GetMapOutputRoot(map)));
            }
            for (var contextIndex = 0; contextIndex < contexts.Count; contextIndex++)
            {
                contexts[contextIndex].Prepare(shouldCancel);
            }
            var irJson = DeterministicJson.SerializeBundle(bundle, playerSprite);
            var reportJson = DeterministicJson.SerializeReport(report);
            ThrowIfCancelled(shouldCancel, "Preparing deterministic output", 0.32f);

            Directory.CreateDirectory(ToAbsolutePath(OutputRoot));
            Directory.CreateDirectory(ToAbsolutePath(OutputRoot + "/Textures"));
            Directory.CreateDirectory(ToAbsolutePath(OutputRoot + "/Tiles"));
            Directory.CreateDirectory(ToAbsolutePath(OutputRoot + "/Player"));
            for (var contextIndex = 0; contextIndex < contexts.Count; contextIndex++)
            {
                contexts[contextIndex].EnsureAssetDirectories();
            }
            // Unity must learn about any newly-created map-specific folders before
            // CreateAsset writes stable Tile assets into them.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var priorManifest = LoadManifest();
            var owned = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                WriteText(IrPath, irJson);
                WriteText(ReportPath, reportJson);
                owned.Add(IrPath);
                owned.Add(ReportPath);

                for (var contextIndex = 0; contextIndex < contexts.Count; contextIndex++)
                {
                    contexts[contextIndex].WriteTexturesAndTiles(owned, shouldCancel);
                }
                FindPalletTownContext(contexts).WritePlayerSprites(owned);
                var objectAssets = objectSprites == null ? null : ObjectEventAssetBuilder.WriteAssets(objectSprites, owned);
                CreateScene(contexts, objectAssets, owned);
                owned.Add(ManifestPath);
                WriteText(ManifestPath, JsonUtility.ToJson(new ImportManifest { schemaVersion = 3, ownedAssets = ToArray(owned) }, true) + "\n");
                RemoveStaleOwnedAssets(priorManifest, owned);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void Import(MapDefinition map, ImportReport report, Func<string, float, bool> shouldCancel)
        {
            throw new InvalidOperationException("MVP2 import requires a complete player sprite IR snapshot.");
        }

        public static string GetOutputFolderAbsolutePath() => ToAbsolutePath(OutputRoot);

        public static int GetGeneratedTileAssetCount()
        {
            var tilesFolder = ToAbsolutePath(OutputRoot + "/Tiles");
            return Directory.Exists(tilesFolder)
                ? Directory.GetFiles(tilesFolder, "*.asset", SearchOption.TopDirectoryOnly).Length
                : 0;
        }

        /// <summary>Produces the exact stable JSON representation used by the generated IR file.</summary>
        public static string SerializeMapJson(MapDefinition map)
        {
            Validate(map);
            return DeterministicJson.SerializeMap(map);
        }

        /// <summary>Produces the complete stable map and player IR diagnostics used by MVP2 output.</summary>
        public static string SerializeMapJson(MapDefinition map, OverworldSpriteDefinition playerSprite)
        {
            Validate(map, playerSprite);
            return DeterministicJson.SerializeMap(map, playerSprite);
        }

        /// <summary>Produces the schema 3 deterministic bundle diagnostics used by MVP3 output.</summary>
        public static string SerializeMapBundleJson(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite)
        {
            Validate(bundle, playerSprite);
            return DeterministicJson.SerializeBundle(bundle, playerSprite);
        }

        /// <summary>Produces the exact stable JSON representation used by the generated report file.</summary>
        public static string SerializeReportJson(ImportReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return DeterministicJson.SerializeReport(report);
        }

        private static void CreateScene(IList<BuildContext> contexts, ObjectEventAssetBuilder.Assets objectAssets, ISet<string> owned)
        {
            Scene scene = File.Exists(ToAbsolutePath(ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mapsObject = FindRootObjectOrNull(scene, "Maps") ?? new GameObject("Maps");
            MigrateLegacyPalletTownRoot(scene, mapsObject.transform);
            var mapRoots = new List<MapRuntimeRoot>(contexts.Count);
            MapRuntimeRoot palletTownRoot = null;
            for (var contextIndex = 0; contextIndex < contexts.Count; contextIndex++)
            {
                var context = contexts[contextIndex];
                var root = GetOrCreateChild(mapsObject.transform, context.Map.Id);
                var grid = GetOrAddComponent<Grid>(root);
                grid.cellSize = new Vector3(1f / CellsPerWorldUnit, 1f / CellsPerWorldUnit, 1f);
                var bottom = GetExistingOrCreateTilemap(root.transform, "Bottom", 0);
                var middle = GetExistingOrCreateTilemap(root.transform, "Middle", 1);
                var top = GetExistingOrCreateTilemap(root.transform, "Top", 3);
                context.FillTilemaps(bottom, middle, top);

                var collisionObject = GetOrCreateChild(root.transform, "Collision");
                var collisionMap = GetOrAddComponent<GridCollisionMap>(collisionObject);
                collisionMap.Configure(
                    context.Map.Width,
                    context.Map.Height,
                    context.CreateBottomUpCollision(),
                    context.CreateBottomUpElevation(),
                    context.CreateEmptyBottomUpEdges());

                var occupancy = GetOrAddComponent<MapCellOccupancy>(collisionObject);
                occupancy.Configure(collisionMap);

                var runtimeWarps = CreateRuntimeWarps(context.Map);
                var runtimeNpcs = objectAssets == null
                    ? new List<NpcController>()
                    : ObjectEventAssetBuilder.CreateMapObjects(context.Map, root.transform, collisionMap, occupancy, objectAssets);
                var mapRoot = GetOrAddComponent<MapRuntimeRoot>(root);
                mapRoot.Configure(context.Map.Id, collisionMap, runtimeWarps, occupancy, runtimeNpcs);
                var npcSimulation = GetOrAddComponent<NpcSimulationDriver>(root);
                npcSimulation.Configure(mapRoot);
                mapRoots.Add(mapRoot);
                if (string.Equals(context.Map.Id, "MAP_PALLET_TOWN", StringComparison.Ordinal)) palletTownRoot = mapRoot;
            }

            if (palletTownRoot == null) throw new InvalidOperationException("The generated map catalog is missing Pallet Town.");

            // Player and camera deliberately remain sibling roots: map roots can be
            // deactivated during a transition without disabling either one.
            var playerObject = FindRootObjectOrNull(scene, "Player") ?? new GameObject("Player");
            var playerRenderer = GetOrAddComponent<SpriteRenderer>(playerObject);
            playerRenderer.sortingLayerName = "Default";
            playerRenderer.sortingOrder = 2;
            var playerAnimator = GetOrAddComponent<DirectionalSpriteAnimator>(playerObject);
            var playerContext = FindPalletTownContext(contexts);
            playerAnimator.Configure(
                playerRenderer,
                playerContext.CreatePlayerSequences(SpriteAnimationState.Idle),
                playerContext.CreatePlayerSequences(SpriteAnimationState.Walking));
            var playerController = GetOrAddComponent<PlayerController>(playerObject);
            playerController.Configure(palletTownRoot.CollisionMap, PlayerSpawnCell, 3, 4f, palletTownRoot.Occupancy);
            playerController.InputEnabled = true;

            var cameraObject = FindRootObjectOrNull(scene, "Main Camera") ?? new GameObject("Main Camera", typeof(Camera), typeof(PixelPerfectCamera));
            var camera = GetOrAddComponent<Camera>(cameraObject);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(palletTownRoot.CollisionMap.Width * 0.5f, palletTownRoot.CollisionMap.Height * 0.5f, -10f);
            var pixelPerfect = GetOrAddComponent<PixelPerfectCamera>(cameraObject);
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = 240;
            pixelPerfect.refResolutionY = 160;
            var follow = GetOrAddComponent<PixelPerfectCameraFollow>(cameraObject);
            follow.ConfigureForMap(camera, playerObject.transform, palletTownRoot.CollisionMap);

            var runtimeObject = FindRootObjectOrNull(scene, "Runtime Map Catalog") ?? new GameObject("Runtime Map Catalog");
            var catalog = GetOrAddComponent<RuntimeMapCatalog>(runtimeObject);
            catalog.Configure(mapRoots);
            var transitions = GetOrAddComponent<MapTransitionSystem>(runtimeObject);
            transitions.Configure(catalog, playerController, follow, palletTownRoot);
            follow.ApplyFollow();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            owned.Add(ScenePath);
        }

        private static List<MapRuntimeWarp> CreateRuntimeWarps(MapDefinition map)
        {
            var result = new List<MapRuntimeWarp>(map.Warps.Count);
            for (var index = 0; index < map.Warps.Count; index++)
            {
                var source = map.Warps[index];
                var runtimeWarp = new MapRuntimeWarp();
                GetRuntimeWarpActivation(source.Activation, out var activation, out var direction);
                var activationCell = ToRuntimeCell(map, source.SourceX, source.SourceY);
                runtimeWarp.Configure(
                    source.Id,
                    activation,
                    activationCell,
                    direction,
                    source.DestinationMapId,
                    source.DestinationMapId + ":warp:" + source.DestinationWarpIndex.ToString(CultureInfo.InvariantCulture),
                    activationCell,
                    checked((byte)source.SourceElevation),
                    ToGridDirection(source.DestinationFacing));
                result.Add(runtimeWarp);
            }

            return result;
        }

        private static void GetRuntimeWarpActivation(WarpActivation source, out MapRuntimeWarpActivation activation, out GridDirection direction)
        {
            switch (source)
            {
                case WarpActivation.DoorNorth: activation = MapRuntimeWarpActivation.AdjacentAttempt; direction = GridDirection.Up; return;
                case WarpActivation.ArrowSouth: activation = MapRuntimeWarpActivation.CurrentCellDirection; direction = GridDirection.Down; return;
                case WarpActivation.StairEast: activation = MapRuntimeWarpActivation.CurrentCellDirection; direction = GridDirection.Right; return;
                case WarpActivation.StairWest: activation = MapRuntimeWarpActivation.CurrentCellDirection; direction = GridDirection.Left; return;
                default: activation = MapRuntimeWarpActivation.Inactive; direction = GridDirection.Up; return;
            }
        }

        private static GridDirection ToGridDirection(SpriteDirection source)
        {
            switch (source)
            {
                case SpriteDirection.South: return GridDirection.Down;
                case SpriteDirection.North: return GridDirection.Up;
                case SpriteDirection.West: return GridDirection.Left;
                case SpriteDirection.East: return GridDirection.Right;
                default: throw new InvalidOperationException("Warp facing is not cardinal.");
            }
        }

        private static Vector2Int ToRuntimeCell(MapDefinition map, int sourceX, int sourceY)
        {
            return new Vector2Int(sourceX, map.Height - 1 - sourceY);
        }

        private static void MigrateLegacyPalletTownRoot(Scene scene, Transform mapsParent)
        {
            var legacy = FindRootObjectOrNull(scene, "Pallet Town");
            if (legacy != null)
            {
                // MVP2 placed the player under the one map root. Detach it before
                // changing that root into an activatable map so transitions cannot
                // accidentally disable the player.
                var legacyPlayer = legacy.transform.Find("Player");
                if (legacyPlayer != null && FindRootObjectOrNull(scene, "Player") == null)
                {
                    legacyPlayer.SetParent(null, true);
                    legacyPlayer.name = "Player";
                }
                legacy.transform.SetParent(mapsParent, false);
                legacy.name = "MAP_PALLET_TOWN";
            }
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            var result = FindRootObjectOrNull(scene, name);
            if (result != null) return result;
            throw new InvalidOperationException("Generated scene is missing root object " + name + ".");
        }

        private static GameObject FindRootObjectOrNull(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, name, StringComparison.Ordinal))
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static Tilemap GetExistingTilemap(Transform parent, string name, int sortingOrder)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                throw new InvalidOperationException("Generated scene is missing Tilemap " + name + ".");
            }

            var tilemap = child.GetComponent<Tilemap>();
            var renderer = child.GetComponent<TilemapRenderer>();
            if (tilemap == null || renderer == null)
            {
                throw new InvalidOperationException("Generated scene has an invalid Tilemap " + name + ".");
            }

            tilemap.ClearAllTiles();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(parent, false);
            var renderer = child.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            return child.GetComponent<Tilemap>();
        }

        private static Tilemap GetExistingOrCreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var child = parent.Find(name);
            return child == null ? CreateTilemap(parent, name, sortingOrder) : GetExistingTilemap(parent, name, sortingOrder);
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            // Unity components can be "fake null" after a script/assembly change.
            // Use UnityEngine.Object's null operator instead of CLR null coalescing.
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static ImportManifest LoadManifest()
        {
            var absolute = ToAbsolutePath(ManifestPath);
            if (!File.Exists(absolute)) return new ImportManifest();
            try
            {
                return JsonUtility.FromJson<ImportManifest>(File.ReadAllText(absolute)) ?? new ImportManifest();
            }
            catch (ArgumentException)
            {
                return new ImportManifest();
            }
        }

        private static void RemoveStaleOwnedAssets(ImportManifest previous, ISet<string> current)
        {
            if (previous?.ownedAssets == null) return;
            for (var i = 0; i < previous.ownedAssets.Length; i++)
            {
                var oldPath = previous.ownedAssets[i];
                if (string.IsNullOrEmpty(oldPath) || current.Contains(oldPath) || !oldPath.StartsWith(OutputRoot + "/", StringComparison.Ordinal)) continue;
                AssetDatabase.DeleteAsset(oldPath);
            }
        }

        private static void ThrowIfCancelled(Func<string, float, bool> shouldCancel, string stage, float progress)
        {
            if (shouldCancel != null && shouldCancel(stage, progress)) throw new OperationCanceledException("Pallet Town import cancelled before generated assets were changed.");
        }

        private static void WriteText(string assetPath, string text)
        {
            File.WriteAllText(ToAbsolutePath(assetPath), text, new UTF8Encoding(false));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        }

        private static string[] ToArray(SortedSet<string> items)
        {
            var result = new string[items.Count];
            items.CopyTo(result);
            return result;
        }

        private static BuildContext FindPalletTownContext(IList<BuildContext> contexts)
        {
            for (var i = 0; i < contexts.Count; i++)
            {
                if (string.Equals(contexts[i].Map.Id, "MAP_PALLET_TOWN", StringComparison.Ordinal)) return contexts[i];
            }

            throw new InvalidOperationException("MVP3 context set is missing MAP_PALLET_TOWN.");
        }

        private static string GetMapOutputRoot(MapDefinition map)
        {
            // Keep all original Pallet Town paths stable so their GUIDs survive the
            // migration from the MVP2 one-map scene. Other maps are isolated below
            // Maps using their canonical, file-system-safe stable ID.
            if (string.Equals(map.Id, "MAP_PALLET_TOWN", StringComparison.Ordinal)) return OutputRoot;
            var stableId = map.Id.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
            return OutputRoot + "/Maps/" + stableId;
        }

        private static Dictionary<int, IndexedTileDefinition> BuildTileLookup(MapDefinition map)
        {
            var result = new Dictionary<int, IndexedTileDefinition>();
            AddTiles(result, map.PrimaryTileset);
            AddTiles(result, map.SecondaryTileset);
            return result;
        }

        private static void AddTiles(IDictionary<int, IndexedTileDefinition> result, TilesetDefinition tileset)
        {
            for (var i = 0; i < tileset.Tiles.Count; i++) result[tileset.Tiles[i].Index] = tileset.Tiles[i];
        }

        private static Dictionary<int, PaletteDefinition> BuildPaletteLookup(MapDefinition map)
        {
            var result = new Dictionary<int, PaletteDefinition>();
            AddPalettes(result, map.PrimaryTileset);
            AddPalettes(result, map.SecondaryTileset);
            return result;
        }

        private static void AddPalettes(IDictionary<int, PaletteDefinition> result, TilesetDefinition tileset)
        {
            for (var i = 0; i < tileset.Palettes.Count; i++) result[tileset.Palettes[i].Index] = tileset.Palettes[i];
        }

        private static Dictionary<int, MetatileDefinition> BuildMetatileLookup(MapDefinition map)
        {
            var result = new Dictionary<int, MetatileDefinition>();
            AddMetatiles(result, map.PrimaryTileset);
            AddMetatiles(result, map.SecondaryTileset);
            return result;
        }

        private static void AddMetatiles(IDictionary<int, MetatileDefinition> result, TilesetDefinition tileset)
        {
            for (var i = 0; i < tileset.Metatiles.Count; i++) result[tileset.Metatiles[i].Index] = tileset.Metatiles[i];
        }

        private sealed class BuildContext
        {
            private readonly Dictionary<int, IndexedTileDefinition> tiles;
            private readonly Dictionary<int, PaletteDefinition> palettes;
            private readonly Dictionary<int, MetatileDefinition> metatiles;
            private readonly Dictionary<int, TileAnimationDefinition> animations;
            private readonly Dictionary<int, IndexedSpriteFrameDefinition> playerFrames;
            private readonly Dictionary<TileKey, TileBase> unityTiles = new Dictionary<TileKey, TileBase>();
            private readonly SortedDictionary<TileKey, Sprite[]> sprites = new SortedDictionary<TileKey, Sprite[]>(new TileKeyComparer());
            private readonly SortedDictionary<PlayerFrameKey, Sprite> playerSprites = new SortedDictionary<PlayerFrameKey, Sprite>(new PlayerFrameKeyComparer());

            public BuildContext(MapDefinition map, OverworldSpriteDefinition playerSprite, string assetRoot)
            {
                Map = map;
                PlayerSprite = playerSprite;
                AssetRoot = assetRoot ?? throw new ArgumentNullException(nameof(assetRoot));
                tiles = BuildTileLookup(map);
                palettes = BuildPaletteLookup(map);
                metatiles = BuildMetatileLookup(map);
                animations = BuildAnimationLookup(map.PrimaryTileset, map.SecondaryTileset);
                playerFrames = BuildPlayerFrameLookup(playerSprite);
            }

            public MapDefinition Map { get; }
            public OverworldSpriteDefinition PlayerSprite { get; }
            public string AssetRoot { get; }

            public void Prepare(Func<string, float, bool> shouldCancel)
            {
                for (var cellIndex = 0; cellIndex < Map.Cells.Count; cellIndex++)
                {
                    ThrowIfCancelled(shouldCancel, "Preparing tile keys", 0.05f + (0.25f * cellIndex / Map.Cells.Count));
                    var metatile = metatiles[Map.Cells[cellIndex].MetatileId];
                    for (var i = 0; i < metatile.Subtiles.Count; i++)
                    {
                        var subtile = metatile.Subtiles[i];
                        var key = new TileKey(subtile.TileIndex, subtile.PaletteIndex, subtile.HorizontalFlip, subtile.VerticalFlip);
                        if (!sprites.ContainsKey(key)) sprites.Add(key, null);
                    }
                }

                for (var animationIndex = 0; animationIndex < PlayerSprite.Animations.Count; animationIndex++)
                {
                    var animation = PlayerSprite.Animations[animationIndex];
                    for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                    {
                        ThrowIfCancelled(shouldCancel, "Preparing player sprite keys", 0.30f);
                        var step = animation.Steps[stepIndex];
                        var key = new PlayerFrameKey(step.FrameIndex, step.HorizontalFlip, step.VerticalFlip);
                        if (!playerSprites.ContainsKey(key))
                        {
                            playerSprites.Add(key, null);
                        }
                    }
                }
            }

            public void EnsureAssetDirectories()
            {
                Directory.CreateDirectory(ToAbsolutePath(AssetRoot + "/Textures"));
                Directory.CreateDirectory(ToAbsolutePath(AssetRoot + "/Tiles"));
            }

            public void WriteTexturesAndTiles(ISet<string> owned, Func<string, float, bool> shouldCancel)
            {
                EnsureAssetDirectories();
                var index = 0;
                var orderedKeys = new List<TileKey>(sprites.Keys);
                foreach (var key in orderedKeys)
                {
                    // Cancellation is intentionally only available until this commit phase begins.
                    // This prevents a half-mutated GUID-preserving reimport.
                    if (index == 0) ThrowIfCancelled(shouldCancel, "Starting asset commit", 0.34f);
                    var animation = FindAnimation(key.TileIndex);
                    var frames = CreateSprites(key, animation, owned);
                    sprites[key] = frames;
                    unityTiles[key] = CreateTileAsset(key, frames, animation, owned);
                    index++;
                }
            }

            public void WritePlayerSprites(ISet<string> owned)
            {
                EnsureAssetDirectories();
                Directory.CreateDirectory(ToAbsolutePath(AssetRoot + "/Player"));
                var orderedKeys = new List<PlayerFrameKey>(playerSprites.Keys);
                for (var keyIndex = 0; keyIndex < orderedKeys.Count; keyIndex++)
                {
                    var key = orderedKeys[keyIndex];
                    var path = AssetRoot + "/Player/" + key.StableName + ".png";
                    WritePlayerTexture(path, playerFrames[key.FrameIndex], PlayerSprite.Palette, key);
                    ConfigureTextureImporter(path, true);
                    owned.Add(path);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        throw new InvalidOperationException("Unity did not import generated player sprite " + path + ".");
                    }

                    playerSprites[key] = sprite;
                }
            }

            public byte[] CreateBottomUpCollision()
            {
                var result = new byte[Map.Cells.Count];
                CopyBottomUpCells(result, cell => checked((byte)cell.Collision));
                return result;
            }

            public byte[] CreateBottomUpElevation()
            {
                var result = new byte[Map.Cells.Count];
                CopyBottomUpCells(result, cell => checked((byte)cell.Elevation));
                return result;
            }

            public GridDirectionMask[] CreateEmptyBottomUpEdges()
            {
                return new GridDirectionMask[Map.Cells.Count];
            }

            public DirectionalSpriteSequence[] CreatePlayerSequences(SpriteAnimationState state)
            {
                var sequences = new DirectionalSpriteSequence[4];
                for (var animationIndex = 0; animationIndex < PlayerSprite.Animations.Count; animationIndex++)
                {
                    var animation = PlayerSprite.Animations[animationIndex];
                    if (animation.State != state)
                    {
                        continue;
                    }

                    var frames = new Sprite[animation.Steps.Count];
                    for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                    {
                        var step = animation.Steps[stepIndex];
                        var key = new PlayerFrameKey(step.FrameIndex, step.HorizontalFlip, step.VerticalFlip);
                        if (!playerSprites.TryGetValue(key, out var sprite) || sprite == null)
                        {
                            throw new InvalidOperationException("Player sprite assets were not prepared before scene creation.");
                        }

                        frames[stepIndex] = sprite;
                    }

                    sequences[SpriteDirectionToSequenceIndex(animation.Direction)] =
                        new DirectionalSpriteSequence(frames, animation.Steps[0].DurationTicks);
                }

                for (var i = 0; i < sequences.Length; i++)
                {
                    if (sequences[i] == null)
                    {
                        throw new InvalidOperationException("Player sprite sequences are incomplete.");
                    }
                }

                return sequences;
            }

            public void FillTilemaps(Tilemap bottom, Tilemap middle, Tilemap top)
            {
                for (var cellIndex = 0; cellIndex < Map.Cells.Count; cellIndex++)
                {
                    var cell = Map.Cells[cellIndex];
                    var metatile = metatiles[cell.MetatileId];
                    var mapX = cellIndex % Map.Width;
                    var mapY = cellIndex / Map.Width;
                    for (var plane = 0; plane < 2; plane++)
                    {
                        var layer = plane == 0 ? metatile.LayerRoute.FirstPlane : metatile.LayerRoute.SecondPlane;
                        var target = layer == RenderLayer.Bottom ? bottom : layer == RenderLayer.Middle ? middle : top;
                        for (var local = 0; local < 4; local++)
                        {
                            var subtile = metatile.Subtiles[(plane * 4) + local];
                            var key = new TileKey(subtile.TileIndex, subtile.PaletteIndex, subtile.HorizontalFlip, subtile.VerticalFlip);
                            var x = (mapX * 2) + (local % 2);
                            var y = ((Map.Height - 1 - mapY) * 2) + (1 - (local / 2));
                            target.SetTile(new Vector3Int(x, y, 0), unityTiles[key]);
                        }
                    }
                }
            }

            private Sprite[] CreateSprites(TileKey key, TileAnimationDefinition animation, ISet<string> owned)
            {
                var sources = GetFrameTiles(key.TileIndex, animation);
                var result = new Sprite[sources.Count];
                for (var frame = 0; frame < sources.Count; frame++)
                {
                    var suffix = sources.Count == 1 ? string.Empty : "_frame_" + frame.ToString("D2", CultureInfo.InvariantCulture);
                    var texturePath = AssetRoot + "/Textures/" + key.StableName + suffix + ".png";
                    WriteTexture(texturePath, sources[frame], palettes[key.PaletteIndex], key);
                    ConfigureTextureImporter(texturePath);
                    owned.Add(texturePath);
                    result[frame] = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                    if (result[frame] == null) throw new InvalidOperationException("Unity did not import generated sprite " + texturePath + ".");
                }
                return result;
            }

            private TileBase CreateTileAsset(TileKey key, Sprite[] frames, TileAnimationDefinition animation, ISet<string> owned)
            {
                var path = AssetRoot + "/Tiles/" + key.StableName + ".asset";
                var shouldAnimate = animation != null && frames.Length > 1;
                var existing = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (shouldAnimate)
                {
                    var animated = existing as DeterministicAnimatedTile;
                    if (animated == null)
                    {
                        if (existing != null) AssetDatabase.DeleteAsset(path);
                        animated = ScriptableObject.CreateInstance<DeterministicAnimatedTile>();
                        AssetDatabase.CreateAsset(animated, path);
                    }
                    animated.Configure(frames, PixelTicksPerSecond / animation.DurationTicks);
                    EditorUtility.SetDirty(animated);
                    owned.Add(path);
                    return animated;
                }

                var tile = existing as Tile;
                if (tile == null)
                {
                    if (existing != null) AssetDatabase.DeleteAsset(path);
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                tile.sprite = frames[0];
                tile.colliderType = Tile.ColliderType.None;
                tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                EditorUtility.SetDirty(tile);
                owned.Add(path);
                return tile;
            }

            private TileAnimationDefinition FindAnimation(int tileIndex)
            {
                return animations.TryGetValue(tileIndex, out var animation) ? animation : null;
            }

            private List<IndexedTileDefinition> GetFrameTiles(int tileIndex, TileAnimationDefinition animation)
            {
                if (animation == null) return new List<IndexedTileDefinition> { tiles[tileIndex] };
                var result = new List<IndexedTileDefinition>(animation.Frames.Count);
                for (var frame = 0; frame < animation.Frames.Count; frame++)
                {
                    IndexedTileDefinition match = null;
                    var frameTiles = animation.Frames[frame].Tiles;
                    for (var i = 0; i < frameTiles.Count; i++)
                    {
                        if (frameTiles[i].Index == tileIndex)
                        {
                            match = frameTiles[i];
                            break;
                        }
                    }
                    if (match == null) throw new InvalidOperationException("Animation " + animation.Id + " does not contain tile " + tileIndex + ".");
                    result.Add(match);
                }
                return result;
            }

            private static Dictionary<int, TileAnimationDefinition> BuildAnimationLookup(params TilesetDefinition[] tilesets)
            {
                var result = new Dictionary<int, TileAnimationDefinition>();
                for (var tilesetIndex = 0; tilesetIndex < tilesets.Length; tilesetIndex++)
                {
                    var tileset = tilesets[tilesetIndex];
                    for (var animationIndex = 0; animationIndex < tileset.Animations.Count; animationIndex++)
                    {
                        var animation = tileset.Animations[animationIndex];
                        for (var frameTile = 0; frameTile < animation.Frames[0].Tiles.Count; frameTile++)
                        {
                            result.Add(animation.Frames[0].Tiles[frameTile].Index, animation);
                        }
                    }
                }
                return result;
            }

            private static Dictionary<int, IndexedSpriteFrameDefinition> BuildPlayerFrameLookup(OverworldSpriteDefinition playerSprite)
            {
                var result = new Dictionary<int, IndexedSpriteFrameDefinition>();
                for (var i = 0; i < playerSprite.Frames.Count; i++)
                {
                    result.Add(playerSprite.Frames[i].Index, playerSprite.Frames[i]);
                }

                return result;
            }

            private void CopyBottomUpCells(byte[] target, Func<MapCellDefinition, byte> selector)
            {
                for (var topDownY = 0; topDownY < Map.Height; topDownY++)
                {
                    var runtimeY = Map.Height - 1 - topDownY;
                    for (var x = 0; x < Map.Width; x++)
                    {
                        target[(runtimeY * Map.Width) + x] = selector(Map.Cells[(topDownY * Map.Width) + x]);
                    }
                }
            }

            private static int SpriteDirectionToSequenceIndex(SpriteDirection direction)
            {
                switch (direction)
                {
                    case SpriteDirection.South:
                        return 0;
                    case SpriteDirection.North:
                        return 1;
                    case SpriteDirection.West:
                        return 2;
                    case SpriteDirection.East:
                        return 3;
                    default:
                        throw new InvalidOperationException("Player animation has an invalid direction.");
                }
            }

            private static void WriteTexture(string assetPath, IndexedTileDefinition tile, PaletteDefinition palette, TileKey key)
            {
                var colors = new Color32[IndexedTileDefinition.PixelCount];
                for (var y = 0; y < IndexedTileDefinition.Height; y++)
                {
                    for (var x = 0; x < IndexedTileDefinition.Width; x++)
                    {
                        var sourceX = key.HorizontalFlip ? IndexedTileDefinition.Width - 1 - x : x;
                        var sourceY = key.VerticalFlip ? IndexedTileDefinition.Height - 1 - y : y;
                        var paletteEntry = palette.Colors[(sourceY * IndexedTileDefinition.Width) + sourceX < tile.Pixels.Count
                            ? tile.Pixels[(sourceY * IndexedTileDefinition.Width) + sourceX]
                            : (byte)0];
                        // Texture coordinates begin at the bottom; source GBA pixels begin at the top.
                        colors[((IndexedTileDefinition.Height - 1 - y) * IndexedTileDefinition.Width) + x] = new Color32(
                            paletteEntry.Red,
                            paletteEntry.Green,
                            paletteEntry.Blue,
                            paletteEntry.Alpha);
                    }
                }
                var texture = new Texture2D(IndexedTileDefinition.Width, IndexedTileDefinition.Height, TextureFormat.RGBA32, false, true);
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                var directory = Path.GetDirectoryName(ToAbsolutePath(assetPath));
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(ToAbsolutePath(assetPath), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            private static void WritePlayerTexture(
                string assetPath,
                IndexedSpriteFrameDefinition frame,
                IReadOnlyList<Rgba32> palette,
                PlayerFrameKey key)
            {
                var colors = new Color32[checked(frame.Width * frame.Height)];
                for (var y = 0; y < frame.Height; y++)
                {
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var sourceX = key.HorizontalFlip ? frame.Width - 1 - x : x;
                        var sourceY = key.VerticalFlip ? frame.Height - 1 - y : y;
                        var paletteIndex = frame.Pixels[(sourceY * frame.Width) + sourceX];
                        var paletteEntry = palette[paletteIndex];
                        colors[((frame.Height - 1 - y) * frame.Width) + x] = new Color32(
                            paletteEntry.Red,
                            paletteEntry.Green,
                            paletteEntry.Blue,
                            paletteIndex == 0 ? (byte)0 : paletteEntry.Alpha);
                    }
                }

                var texture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, true);
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                File.WriteAllBytes(ToAbsolutePath(assetPath), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            private static void ConfigureTextureImporter(string assetPath, bool playerSprite = false)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Generated texture has no TextureImporter: " + assetPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = false;
                if (playerSprite)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, 0.25f);
                    importer.SetTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }

            private sealed class TileKeyComparer : IComparer<TileKey>
            {
                public int Compare(TileKey left, TileKey right)
                {
                    return string.CompareOrdinal(left.StableName, right.StableName);
                }
            }

            private sealed class PlayerFrameKeyComparer : IComparer<PlayerFrameKey>
            {
                public int Compare(PlayerFrameKey left, PlayerFrameKey right)
                {
                    return string.CompareOrdinal(left.StableName, right.StableName);
                }
            }
        }

        private static class DeterministicJson
        {
            public static string SerializeBundle(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite)
            {
                var builder = new StringBuilder(8192);
                builder.Append("{\n  \"schemaVersion\": 3,\n  \"maps\": [");
                for (var mapIndex = 0; mapIndex < bundle.Maps.Count; mapIndex++)
                {
                    builder.Append(mapIndex == 0 ? "\n    " : ",\n    ");
                    AppendMapV3(builder, bundle.Maps[mapIndex]);
                }

                builder.Append("\n  ],\n  \"playerSprite\": ");
                AppendPlayerSprite(builder, playerSprite);
                builder.Append("\n}\n");
                return builder.ToString();
            }

            public static string SerializeMap(MapDefinition map)
            {
                return SerializeMap(map, null);
            }

            public static string SerializeMap(MapDefinition map, OverworldSpriteDefinition playerSprite)
            {
                var builder = new StringBuilder(4096);
                builder.Append("{\n  \"schemaVersion\": 2,\n  \"id\": ");
                AppendString(builder, map.Id);
                builder.Append(",\n  \"name\": ");
                AppendString(builder, map.Name);
                builder.Append(",\n  \"width\": ").Append(map.Width).Append(",\n  \"height\": ").Append(map.Height);
                builder.Append(",\n  \"primaryTileset\": "); AppendString(builder, map.PrimaryTileset.Id);
                builder.Append(",\n  \"secondaryTileset\": "); AppendString(builder, map.SecondaryTileset.Id);
                builder.Append(",\n  \"tilesets\": [\n    ");
                AppendTileset(builder, map.PrimaryTileset);
                builder.Append(",\n    ");
                AppendTileset(builder, map.SecondaryTileset);
                builder.Append("\n  ]");
                builder.Append(",\n  \"cells\": [");
                for (var i = 0; i < map.Cells.Count; i++)
                {
                    var cell = map.Cells[i];
                    builder.Append(i == 0 ? "\n    " : ",\n    ");
                    builder.Append("{\"metatile\":").Append(cell.MetatileId)
                        .Append(",\"collision\":").Append(cell.Collision)
                        .Append(",\"elevation\":").Append(cell.Elevation).Append('}');
                }
                builder.Append("\n  ]");
                if (playerSprite != null)
                {
                    builder.Append(",\n  \"playerSprite\": ");
                    AppendPlayerSprite(builder, playerSprite);
                }
                builder.Append("\n}\n");
                return builder.ToString();
            }

            private static void AppendMapV3(StringBuilder builder, MapDefinition map)
            {
                builder.Append("{\"id\":");
                AppendString(builder, map.Id);
                builder.Append(",\"name\":");
                AppendString(builder, map.Name);
                builder.Append(",\"width\":").Append(map.Width).Append(",\"height\":").Append(map.Height);
                builder.Append(",\"primaryTileset\":"); AppendString(builder, map.PrimaryTileset.Id);
                builder.Append(",\"secondaryTileset\":"); AppendString(builder, map.SecondaryTileset.Id);
                builder.Append(",\"tilesets\":[");
                AppendTileset(builder, map.PrimaryTileset);
                builder.Append(',');
                AppendTileset(builder, map.SecondaryTileset);
                builder.Append("],\"cells\":[");
                for (var cellIndex = 0; cellIndex < map.Cells.Count; cellIndex++)
                {
                    var cell = map.Cells[cellIndex];
                    builder.Append(cellIndex == 0 ? string.Empty : ",")
                        .Append("{\"metatile\":").Append(cell.MetatileId)
                        .Append(",\"collision\":").Append(cell.Collision)
                        .Append(",\"elevation\":").Append(cell.Elevation).Append('}');
                }

                builder.Append("],\"warps\":[");
                for (var warpIndex = 0; warpIndex < map.Warps.Count; warpIndex++)
                {
                    var warp = map.Warps[warpIndex];
                    builder.Append(warpIndex == 0 ? string.Empty : ",").Append("{\"id\":");
                    AppendString(builder, warp.Id);
                    builder.Append(",\"index\":").Append(warp.Index)
                        .Append(",\"sourceX\":").Append(warp.SourceX)
                        .Append(",\"sourceY\":").Append(warp.SourceY)
                        .Append(",\"sourceElevation\":").Append(warp.SourceElevation)
                        .Append(",\"destinationMapId\":");
                    AppendString(builder, warp.DestinationMapId);
                    builder.Append(",\"destinationWarpIndex\":").Append(warp.DestinationWarpIndex)
                        .Append(",\"activation\":");
                    AppendString(builder, warp.Activation.ToString());
                    builder.Append(",\"destinationFacing\":");
                    AppendString(builder, warp.DestinationFacing.ToString());
                    builder.Append('}');
                }

                builder.Append("]}");
            }

            private static void AppendPlayerSprite(StringBuilder builder, OverworldSpriteDefinition playerSprite)
            {
                builder.Append("{\"id\":");
                AppendString(builder, playerSprite.Id);
                builder.Append(",\"width\":").Append(playerSprite.Width)
                    .Append(",\"height\":").Append(playerSprite.Height)
                    .Append(",\"palette\":[");
                for (var colorIndex = 0; colorIndex < playerSprite.Palette.Count; colorIndex++)
                {
                    var color = playerSprite.Palette[colorIndex];
                    builder.Append(colorIndex == 0 ? string.Empty : ",")
                        .Append("[").Append(color.Red).Append(',').Append(color.Green).Append(',')
                        .Append(color.Blue).Append(',').Append(color.Alpha).Append(']');
                }

                var orderedFrames = new List<IndexedSpriteFrameDefinition>(playerSprite.Frames);
                orderedFrames.Sort((left, right) => left.Index.CompareTo(right.Index));
                builder.Append("],\"frames\":[");
                for (var frameIndex = 0; frameIndex < orderedFrames.Count; frameIndex++)
                {
                    var frame = orderedFrames[frameIndex];
                    builder.Append(frameIndex == 0 ? string.Empty : ",")
                        .Append("{\"index\":").Append(frame.Index)
                        .Append(",\"width\":").Append(frame.Width)
                        .Append(",\"height\":").Append(frame.Height)
                        .Append(",\"pixels\":[");
                    for (var pixelIndex = 0; pixelIndex < frame.Pixels.Count; pixelIndex++)
                    {
                        builder.Append(pixelIndex == 0 ? string.Empty : ",").Append(frame.Pixels[pixelIndex]);
                    }

                    builder.Append("]}");
                }

                builder.Append("],\"animations\":[");
                var animationIndex = 0;
                for (var directionValue = (int)SpriteDirection.South; directionValue <= (int)SpriteDirection.East; directionValue++)
                {
                    var direction = (SpriteDirection)directionValue;
                    for (var stateValue = (int)SpriteAnimationState.Idle; stateValue <= (int)SpriteAnimationState.Walking; stateValue++)
                    {
                        var state = (SpriteAnimationState)stateValue;
                        var animation = FindPlayerAnimation(playerSprite, direction, state);
                        builder.Append(animationIndex == 0 ? string.Empty : ",").Append("{\"direction\":");
                        AppendString(builder, animation.Direction.ToString());
                        builder.Append(",\"state\":");
                        AppendString(builder, animation.State.ToString());
                        builder.Append(",\"steps\":[");
                        for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                        {
                            var step = animation.Steps[stepIndex];
                            builder.Append(stepIndex == 0 ? string.Empty : ",")
                                .Append("{\"frame\":").Append(step.FrameIndex)
                                .Append(",\"hFlip\":").Append(step.HorizontalFlip ? "true" : "false")
                                .Append(",\"vFlip\":").Append(step.VerticalFlip ? "true" : "false")
                                .Append(",\"durationTicks\":").Append(step.DurationTicks).Append('}');
                        }

                        builder.Append("]}");
                        animationIndex++;
                    }
                }

                builder.Append("]}");
            }

            private static DirectionalSpriteAnimationDefinition FindPlayerAnimation(
                OverworldSpriteDefinition playerSprite,
                SpriteDirection direction,
                SpriteAnimationState state)
            {
                for (var i = 0; i < playerSprite.Animations.Count; i++)
                {
                    var animation = playerSprite.Animations[i];
                    if (animation.Direction == direction && animation.State == state)
                    {
                        return animation;
                    }
                }

                throw new InvalidOperationException("Player sprite is missing a required animation.");
            }

            private static void AppendTileset(StringBuilder builder, TilesetDefinition tileset)
            {
                builder.Append("{\"id\":"); AppendString(builder, tileset.Id);
                builder.Append(",\"isSecondary\":").Append(tileset.IsSecondary ? "true" : "false");
                builder.Append(",\"tiles\":[");
                for (var i = 0; i < tileset.Tiles.Count; i++)
                {
                    var tile = tileset.Tiles[i];
                    builder.Append(i == 0 ? string.Empty : ",");
                    AppendTile(builder, tile);
                }
                builder.Append("],\"palettes\":[");
                for (var i = 0; i < tileset.Palettes.Count; i++)
                {
                    var palette = tileset.Palettes[i];
                    builder.Append(i == 0 ? string.Empty : ",");
                    builder.Append("{\"index\":").Append(palette.Index).Append(",\"colors\":[");
                    for (var color = 0; color < palette.Colors.Count; color++)
                    {
                        var rgba = palette.Colors[color];
                        builder.Append(color == 0 ? string.Empty : ",");
                        builder.Append("[").Append(rgba.Red).Append(',').Append(rgba.Green).Append(',').Append(rgba.Blue).Append(',').Append(rgba.Alpha).Append(']');
                    }
                    builder.Append("]}");
                }
                builder.Append("],\"metatiles\":[");
                for (var i = 0; i < tileset.Metatiles.Count; i++)
                {
                    var metatile = tileset.Metatiles[i];
                    builder.Append(i == 0 ? string.Empty : ",");
                    builder.Append("{\"index\":").Append(metatile.Index)
                        .Append(",\"attributes\":").Append(metatile.Attributes)
                        .Append(",\"route\":{\"first\":");
                    AppendString(builder, metatile.LayerRoute.FirstPlane.ToString());
                    builder.Append(",\"second\":");
                    AppendString(builder, metatile.LayerRoute.SecondPlane.ToString());
                    builder.Append("},\"subtiles\":[");
                    for (var subtileIndex = 0; subtileIndex < metatile.Subtiles.Count; subtileIndex++)
                    {
                        var subtile = metatile.Subtiles[subtileIndex];
                        builder.Append(subtileIndex == 0 ? string.Empty : ",");
                        builder.Append("{\"tile\":").Append(subtile.TileIndex)
                            .Append(",\"palette\":").Append(subtile.PaletteIndex)
                            .Append(",\"hFlip\":").Append(subtile.HorizontalFlip ? "true" : "false")
                            .Append(",\"vFlip\":").Append(subtile.VerticalFlip ? "true" : "false").Append('}');
                    }
                    builder.Append("]}");
                }
                builder.Append("],\"animations\":[");
                for (var i = 0; i < tileset.Animations.Count; i++)
                {
                    var animation = tileset.Animations[i];
                    builder.Append(i == 0 ? string.Empty : ",");
                    builder.Append("{\"id\":"); AppendString(builder, animation.Id);
                    builder.Append(",\"destinationTile\":").Append(animation.DestinationTileIndex)
                        .Append(",\"durationTicks\":").Append(animation.DurationTicks).Append(",\"frames\":[");
                    for (var frame = 0; frame < animation.Frames.Count; frame++)
                    {
                        builder.Append(frame == 0 ? string.Empty : ",");
                        builder.Append('[');
                        var frameTiles = animation.Frames[frame].Tiles;
                        for (var tile = 0; tile < frameTiles.Count; tile++)
                        {
                            builder.Append(tile == 0 ? string.Empty : ",");
                            AppendTile(builder, frameTiles[tile]);
                        }
                        builder.Append(']');
                    }
                    builder.Append("]}");
                }
                builder.Append("]}");
            }

            private static void AppendTile(StringBuilder builder, IndexedTileDefinition tile)
            {
                builder.Append("{\"index\":").Append(tile.Index).Append(",\"pixels\":[");
                for (var pixel = 0; pixel < tile.Pixels.Count; pixel++)
                {
                    builder.Append(pixel == 0 ? string.Empty : ",").Append(tile.Pixels[pixel]);
                }
                builder.Append("]}");
            }

            public static string SerializeReport(ImportReport report)
            {
                var builder = new StringBuilder();
                builder.Append("{\n  \"schemaVersion\": 1,\n  \"stage\": ");
                AppendString(builder, report.Stage);
                builder.Append(",\n  \"diagnostics\": [");
                for (var i = 0; i < report.Diagnostics.Count; i++)
                {
                    var diagnostic = report.Diagnostics[i];
                    builder.Append(i == 0 ? "\n    {\"stage\":" : ",\n    {\"stage\":");
                    AppendString(builder, diagnostic.Stage ?? report.Stage);
                    builder.Append(",\"category\":"); AppendString(builder, diagnostic.Category);
                    builder.Append(",\"severity\":"); AppendString(builder, diagnostic.Severity.ToString());
                    builder.Append(",\"message\":"); AppendString(builder, diagnostic.Message);
                    if (diagnostic.Offset.HasValue) builder.Append(",\"offset\":").Append(diagnostic.Offset.Value);
                    if (diagnostic.Length.HasValue) builder.Append(",\"length\":").Append(diagnostic.Length.Value);
                    builder.Append('}');
                }
                builder.Append("\n  ]\n}\n");
                return builder.ToString();
            }

            private static void AppendString(StringBuilder builder, string value)
            {
                builder.Append('"');
                if (value != null)
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        var character = value[i];
                        switch (character)
                        {
                            case '\\': builder.Append("\\\\"); break;
                            case '"': builder.Append("\\\""); break;
                            case '\n': builder.Append("\\n"); break;
                            case '\r': builder.Append("\\r"); break;
                            case '\t': builder.Append("\\t"); break;
                            default:
                                if (character < 32) builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                                else builder.Append(character);
                                break;
                        }
                    }
                }
                builder.Append('"');
            }
        }
    }
}
