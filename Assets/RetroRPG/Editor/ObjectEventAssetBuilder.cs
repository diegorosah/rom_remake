using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RetroRPG.IR;
using RetroRPG.Runtime;
using UnityEditor;
using UnityEngine;

namespace RetroRPG.Editor
{
    /// <summary>Converts generic object-event IR into deterministic sprite assets and scene actors.</summary>
    internal static class ObjectEventAssetBuilder
    {
        private const string ObjectRoot = PalletTownAssetBuilder.OutputRoot + "/Objects";

        internal sealed class Assets
        {
            private readonly Dictionary<string, MobileAsset> mobile;
            private readonly Dictionary<string, Sprite> statics;

            public Assets(Dictionary<string, MobileAsset> configuredMobile, Dictionary<string, Sprite> configuredStatics)
            {
                mobile = configuredMobile ?? throw new ArgumentNullException(nameof(configuredMobile));
                statics = configuredStatics ?? throw new ArgumentNullException(nameof(configuredStatics));
            }

            public MobileAsset GetMobile(string id)
            {
                if (!mobile.TryGetValue(id, out var result)) throw new InvalidOperationException("Missing generated mobile sprite " + id + ".");
                return result;
            }

            public Sprite GetStatic(string id)
            {
                if (!statics.TryGetValue(id, out var result)) throw new InvalidOperationException("Missing generated static sprite " + id + ".");
                return result;
            }
        }

        internal sealed class MobileAsset
        {
            public MobileAsset(DirectionalSpriteSequence[] idle, DirectionalSpriteSequence[] walking)
            {
                Idle = idle;
                Walking = walking;
            }

            public DirectionalSpriteSequence[] Idle { get; }
            public DirectionalSpriteSequence[] Walking { get; }
        }

        internal sealed class MapObjects
        {
            public MapObjects(List<NpcController> npcs, List<MonoBehaviour> interactionTargets)
            {
                Npcs = npcs;
                InteractionTargets = interactionTargets;
            }

            public List<NpcController> Npcs { get; }
            public List<MonoBehaviour> InteractionTargets { get; }
        }

        private readonly struct FrameKey : IEquatable<FrameKey>
        {
            public FrameKey(int frameIndex, bool horizontalFlip, bool verticalFlip)
            {
                FrameIndex = frameIndex;
                HorizontalFlip = horizontalFlip;
                VerticalFlip = verticalFlip;
            }

            public int FrameIndex { get; }
            public bool HorizontalFlip { get; }
            public bool VerticalFlip { get; }
            public string StableName => string.Format(CultureInfo.InvariantCulture, "frame_{0:D2}_h{1}_v{2}", FrameIndex, HorizontalFlip ? 1 : 0, VerticalFlip ? 1 : 0);

            public bool Equals(FrameKey other) => FrameIndex == other.FrameIndex && HorizontalFlip == other.HorizontalFlip && VerticalFlip == other.VerticalFlip;
            public override bool Equals(object obj) => obj is FrameKey other && Equals(other);
            public override int GetHashCode() => ((FrameIndex * 397) ^ (HorizontalFlip ? 1 : 0)) * 397 ^ (VerticalFlip ? 1 : 0);
        }

        public static void Validate(MapBundleDefinition bundle, ObjectSpriteCatalogDefinition catalog)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (var mapIndex = 0; mapIndex < bundle.Maps.Count; mapIndex++)
            {
                var map = bundle.Maps[mapIndex];
                for (var npcIndex = 0; npcIndex < map.Npcs.Count; npcIndex++)
                {
                    var npc = map.Npcs[npcIndex];
                    if (!eventIds.Add(npc.EventId) || npc.CellX >= map.Width || npc.CellY >= map.Height ||
                        npc.MaxX >= map.Width || npc.MaxY >= map.Height || !catalog.TryGetMobile(npc.SpriteId, out _))
                    {
                        throw new InvalidOperationException("Map bundle contains an invalid or unresolved NPC " + npc.EventId + ".");
                    }
                }

                for (var propIndex = 0; propIndex < map.Props.Count; propIndex++)
                {
                    var prop = map.Props[propIndex];
                    if (!eventIds.Add(prop.EventId) || prop.CellX >= map.Width || prop.CellY >= map.Height ||
                        !catalog.TryGetStatic(prop.SpriteId, out _))
                    {
                        throw new InvalidOperationException("Map bundle contains an invalid or unresolved prop " + prop.EventId + ".");
                    }
                }
            }
        }

        public static Assets WriteAssets(ObjectSpriteCatalogDefinition catalog, ISet<string> owned)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            Directory.CreateDirectory(ToAbsolutePath(ObjectRoot));

            var mobile = new Dictionary<string, MobileAsset>(StringComparer.Ordinal);
            for (var spriteIndex = 0; spriteIndex < catalog.MobileSprites.Count; spriteIndex++)
            {
                var definition = catalog.MobileSprites[spriteIndex];
                mobile.Add(definition.Id, WriteMobile(definition, owned));
            }

            var statics = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (var spriteIndex = 0; spriteIndex < catalog.StaticSprites.Count; spriteIndex++)
            {
                var definition = catalog.StaticSprites[spriteIndex];
                var directory = ObjectRoot + "/" + SafeId(definition.Id);
                Directory.CreateDirectory(ToAbsolutePath(directory));
                var path = directory + "/frame_00.png";
                var changed = WriteTexture(path, definition.Frames[0], definition.Palette, false, false);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (changed || sprite == null)
                {
                    ConfigureTexture(path, definition.Height);
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                owned.Add(path);
                if (sprite == null) throw new InvalidOperationException("Unity did not import generated static sprite " + path + ".");
                statics.Add(definition.Id, sprite);
            }

            return new Assets(mobile, statics);
        }

        public static MapObjects CreateMapObjects(
            MapDefinition map,
            Transform mapRoot,
            GridCollisionMap collisionMap,
            MapCellOccupancy occupancy,
            Assets assets)
        {
            if (map == null || mapRoot == null || collisionMap == null || occupancy == null || assets == null) throw new ArgumentNullException();
            var prior = mapRoot.Find("Objects");
            if (prior != null) UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var container = new GameObject("Objects");
            container.transform.SetParent(mapRoot, false);

            var result = new List<NpcController>(map.Npcs.Count);
            var interactionTargets = new List<MonoBehaviour>(map.Npcs.Count + map.Props.Count);
            for (var npcIndex = 0; npcIndex < map.Npcs.Count; npcIndex++)
            {
                var definition = map.Npcs[npcIndex];
                var spriteAsset = assets.GetMobile(definition.SpriteId);
                var instance = new GameObject(definition.EventId);
                instance.transform.SetParent(container.transform, false);
                var renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 2;
                var animator = instance.AddComponent<DirectionalSpriteAnimator>();
                animator.Configure(renderer, spriteAsset.Idle, spriteAsset.Walking);
                var controller = instance.AddComponent<NpcController>();
                controller.Configure(
                    definition.EventId,
                    collisionMap,
                    ToRuntimeCell(map, definition.CellX, definition.CellY),
                    checked((byte)definition.Elevation),
                    animator,
                    occupancy,
                    2f);
                controller.ConfigureMovementBounds(
                    new Vector2Int(definition.MinX, map.Height - 1 - definition.MaxY),
                    new Vector2Int(definition.MaxX, map.Height - 1 - definition.MinY));
                controller.ConfigureInteraction(definition.InteractionKey);
                controller.Face(ToGridDirection(definition.InitialDirection));
                controller.SetVisible(definition.VisibleByDefault);
                if (definition.MovementPattern == NpcMovementPattern.WanderCardinal)
                {
                    controller.SetMovementPattern(new DeterministicWanderNpcMovementPattern(
                        120,
                        new DeterministicNpcRandomSource(StableSeed(definition.EventId))));
                }
                else
                {
                    controller.SetMovementPattern(new FixedFacingNpcMovementPattern());
                }
                result.Add(controller);
                interactionTargets.Add(controller);
            }

            for (var propIndex = 0; propIndex < map.Props.Count; propIndex++)
            {
                var definition = map.Props[propIndex];
                var instance = new GameObject(definition.EventId);
                instance.transform.SetParent(container.transform, false);
                instance.transform.position = collisionMap.CellCenter(ToRuntimeCell(map, definition.CellX, definition.CellY));
                var renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sprite = assets.GetStatic(definition.SpriteId);
                renderer.enabled = definition.VisibleByDefault;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 2;
                var target = instance.AddComponent<InteractionTarget>();
                target.Configure(
                    definition.InteractionKey,
                    ToRuntimeCell(map, definition.CellX, definition.CellY),
                    checked((byte)definition.Elevation),
                    definition.VisibleByDefault);
                interactionTargets.Add(target);
            }

            return new MapObjects(result, interactionTargets);
        }

        private static MobileAsset WriteMobile(OverworldSpriteDefinition definition, ISet<string> owned)
        {
            var frameDefinitions = new Dictionary<int, IndexedSpriteFrameDefinition>();
            for (var i = 0; i < definition.Frames.Count; i++) frameDefinitions.Add(definition.Frames[i].Index, definition.Frames[i]);
            var sprites = new Dictionary<FrameKey, Sprite>();
            for (var animationIndex = 0; animationIndex < definition.Animations.Count; animationIndex++)
            {
                var animation = definition.Animations[animationIndex];
                for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                {
                    var step = animation.Steps[stepIndex];
                    var key = new FrameKey(step.FrameIndex, step.HorizontalFlip, step.VerticalFlip);
                    if (sprites.ContainsKey(key)) continue;
                    var directory = ObjectRoot + "/" + SafeId(definition.Id);
                    Directory.CreateDirectory(ToAbsolutePath(directory));
                    var path = directory + "/" + key.StableName + ".png";
                    var changed = WriteTexture(path, frameDefinitions[key.FrameIndex], definition.Palette, key.HorizontalFlip, key.VerticalFlip);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (changed || sprite == null)
                    {
                        ConfigureTexture(path, definition.Height);
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                    owned.Add(path);
                    if (sprite == null) throw new InvalidOperationException("Unity did not import generated NPC sprite " + path + ".");
                    sprites.Add(key, sprite);
                }
            }

            return new MobileAsset(
                CreateSequences(definition, SpriteAnimationState.Idle, sprites),
                CreateSequences(definition, SpriteAnimationState.Walking, sprites));
        }

        private static DirectionalSpriteSequence[] CreateSequences(OverworldSpriteDefinition definition, SpriteAnimationState state, IDictionary<FrameKey, Sprite> sprites)
        {
            var result = new DirectionalSpriteSequence[4];
            for (var animationIndex = 0; animationIndex < definition.Animations.Count; animationIndex++)
            {
                var animation = definition.Animations[animationIndex];
                if (animation.State != state) continue;
                var frames = new Sprite[animation.Steps.Count];
                for (var stepIndex = 0; stepIndex < animation.Steps.Count; stepIndex++)
                {
                    var step = animation.Steps[stepIndex];
                    frames[stepIndex] = sprites[new FrameKey(step.FrameIndex, step.HorizontalFlip, step.VerticalFlip)];
                }
                result[DirectionIndex(animation.Direction)] = new DirectionalSpriteSequence(frames, animation.Steps[0].DurationTicks);
            }
            for (var i = 0; i < result.Length; i++) if (result[i] == null) throw new InvalidOperationException("Object sprite directional sequences are incomplete.");
            return result;
        }

        private static bool WriteTexture(string assetPath, IndexedSpriteFrameDefinition frame, IReadOnlyList<Rgba32> palette, bool horizontalFlip, bool verticalFlip)
        {
            var colors = new Color32[checked(frame.Width * frame.Height)];
            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var sourceX = horizontalFlip ? frame.Width - 1 - x : x;
                    var sourceY = verticalFlip ? frame.Height - 1 - y : y;
                    var paletteIndex = frame.Pixels[(sourceY * frame.Width) + sourceX];
                    var color = palette[paletteIndex];
                    colors[((frame.Height - 1 - y) * frame.Width) + x] = new Color32(color.Red, color.Green, color.Blue, paletteIndex == 0 ? (byte)0 : color.Alpha);
                }
            }
            var texture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            var encoded = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return WriteBytesIfChanged(assetPath, encoded);
        }

        private static bool WriteBytesIfChanged(string assetPath, byte[] bytes)
        {
            var absolutePath = ToAbsolutePath(assetPath);
            if (File.Exists(absolutePath))
            {
                var info = new FileInfo(absolutePath);
                if (info.Length == bytes.Length)
                {
                    var existing = File.ReadAllBytes(absolutePath);
                    if (existing.Length == bytes.Length)
                    {
                        var equal = true;
                        for (var index = 0; index < bytes.Length; index++)
                        {
                            if (existing[index] == bytes[index]) continue;
                            equal = false;
                            break;
                        }
                        if (equal) return false;
                    }
                }
            }

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(absolutePath, bytes);
            return true;
        }

        private static void ConfigureTexture(string assetPath, int height)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException("Generated object texture has no TextureImporter: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, Mathf.Clamp01(8f / height));
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Vector2Int ToRuntimeCell(MapDefinition map, int x, int y) => new Vector2Int(x, map.Height - 1 - y);
        private static string SafeId(string value) => value.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        private static string ToAbsolutePath(string assetPath) => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        private static uint StableSeed(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619u;
                return hash;
            }
        }
        private static int DirectionIndex(SpriteDirection direction) => (int)direction;
        private static GridDirection ToGridDirection(SpriteDirection direction)
        {
            switch (direction)
            {
                case SpriteDirection.South: return GridDirection.Down;
                case SpriteDirection.North: return GridDirection.Up;
                case SpriteDirection.West: return GridDirection.Left;
                case SpriteDirection.East: return GridDirection.Right;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
