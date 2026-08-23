using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RetroRPG.Core;
using RetroRPG.IR;
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

        [Serializable]
        private sealed class ImportManifest
        {
            public int schemaVersion = 1;
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

        public static void Validate(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.Width != 24 || map.Height != 20 || map.Cells.Count != 480)
            {
                throw new InvalidOperationException("Pallet Town IR must contain a 24x20 grid with 480 cells.");
            }

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
        public static void Import(MapDefinition map, ImportReport report, Func<string, float, bool> shouldCancel)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.HasErrors) throw new InvalidOperationException("An import report with errors cannot generate assets.");
            ThrowIfCancelled(shouldCancel, "Validating IR", 0.05f);
            Validate(map);

            // All object discovery and JSON construction completes before generated assets are touched.
            var context = new BuildContext(map);
            context.Prepare(shouldCancel);
            ThrowIfCancelled(shouldCancel, "Preparing deterministic output", 0.32f);

            Directory.CreateDirectory(ToAbsolutePath(OutputRoot));
            Directory.CreateDirectory(ToAbsolutePath(OutputRoot + "/Textures"));
            Directory.CreateDirectory(ToAbsolutePath(OutputRoot + "/Tiles"));
            var priorManifest = LoadManifest();
            var owned = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                WriteText(IrPath, DeterministicJson.SerializeMap(map));
                WriteText(ReportPath, DeterministicJson.SerializeReport(report));
                owned.Add(IrPath);
                owned.Add(ReportPath);

                context.WriteTexturesAndTiles(owned, shouldCancel);
                CreateScene(context, owned);
                owned.Add(ManifestPath);
                WriteText(ManifestPath, JsonUtility.ToJson(new ImportManifest { ownedAssets = ToArray(owned) }, true) + "\n");
                RemoveStaleOwnedAssets(priorManifest, owned);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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

        /// <summary>Produces the exact stable JSON representation used by the generated report file.</summary>
        public static string SerializeReportJson(ImportReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return DeterministicJson.SerializeReport(report);
        }

        private static void CreateScene(BuildContext context, ISet<string> owned)
        {
            Scene scene;
            GameObject root;
            Tilemap bottom;
            Tilemap middle;
            Tilemap top;
            GameObject cameraObject;

            if (File.Exists(ToAbsolutePath(ScenePath)))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                root = FindRootObject(scene, "Pallet Town");
                bottom = GetExistingTilemap(root.transform, "Bottom", 0);
                middle = GetExistingTilemap(root.transform, "Middle", 1);
                top = GetExistingTilemap(root.transform, "Top", 2);
                cameraObject = FindRootObject(scene, "Main Camera");
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                root = new GameObject("Pallet Town");
                root.AddComponent<Grid>();
                bottom = CreateTilemap(root.transform, "Bottom", 0);
                middle = CreateTilemap(root.transform, "Middle", 1);
                top = CreateTilemap(root.transform, "Top", 2);
                cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(PixelPerfectCamera));
            }

            var grid = root.GetComponent<Grid>();
            grid.cellSize = new Vector3(1f / CellsPerWorldUnit, 1f / CellsPerWorldUnit, 1f);

            context.FillTilemaps(bottom, middle, top);

            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(context.Map.Width * 0.5f, context.Map.Height * 0.5f, -10f);
            var pixelPerfect = cameraObject.GetComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = 240;
            pixelPerfect.refResolutionY = 160;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            owned.Add(ScenePath);
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, name, StringComparison.Ordinal))
                {
                    return roots[i];
                }
            }

            throw new InvalidOperationException("Generated scene is missing root object " + name + ".");
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
            private readonly Dictionary<TileKey, TileBase> unityTiles = new Dictionary<TileKey, TileBase>();
            private readonly SortedDictionary<TileKey, Sprite[]> sprites = new SortedDictionary<TileKey, Sprite[]>(new TileKeyComparer());

            public BuildContext(MapDefinition map)
            {
                Map = map;
                tiles = BuildTileLookup(map);
                palettes = BuildPaletteLookup(map);
                metatiles = BuildMetatileLookup(map);
                animations = BuildAnimationLookup(map.PrimaryTileset, map.SecondaryTileset);
            }

            public MapDefinition Map { get; }

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
            }

            public void WriteTexturesAndTiles(ISet<string> owned, Func<string, float, bool> shouldCancel)
            {
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
                    var texturePath = OutputRoot + "/Textures/" + key.StableName + suffix + ".png";
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
                var path = OutputRoot + "/Tiles/" + key.StableName + ".asset";
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

            private static void ConfigureTextureImporter(string assetPath)
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
                importer.SaveAndReimport();
            }

            private sealed class TileKeyComparer : IComparer<TileKey>
            {
                public int Compare(TileKey left, TileKey right)
                {
                    return string.CompareOrdinal(left.StableName, right.StableName);
                }
            }
        }

        private static class DeterministicJson
        {
            public static string SerializeMap(MapDefinition map)
            {
                var builder = new StringBuilder(4096);
                builder.Append("{\n  \"schemaVersion\": 1,\n  \"id\": ");
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
                builder.Append("\n  ]\n}\n");
                return builder.ToString();
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
