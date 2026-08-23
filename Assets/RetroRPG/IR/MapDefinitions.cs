using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    public enum RenderLayer
    {
        Invalid = -1,
        Bottom,
        Middle,
        Top
    }

    [Serializable]
    public struct Rgba32
    {
        public Rgba32(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }
    }

    [Serializable]
    public sealed class PaletteDefinition
    {
        public PaletteDefinition(int index, IList<Rgba32> colors)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            if (colors.Count != 16) throw new ArgumentException("A GBA palette has exactly 16 colours.", nameof(colors));

            Index = index;
            Colors = new ReadOnlyCollection<Rgba32>(new List<Rgba32>(colors));
        }

        public int Index { get; }
        public IReadOnlyList<Rgba32> Colors { get; }
    }

    [Serializable]
    public sealed class IndexedTileDefinition
    {
        public const int Width = 8;
        public const int Height = 8;
        public const int PixelCount = Width * Height;

        private readonly ReadOnlyCollection<byte> pixels;

        public IndexedTileDefinition(int index, byte[] pixels)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != PixelCount) throw new ArgumentException("An indexed GBA tile must contain 64 pixels.", nameof(pixels));

            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] > 15) throw new ArgumentException("A 4bpp pixel must be in the range 0..15.", nameof(pixels));
            }

            Index = index;
            var copiedPixels = new List<byte>(pixels.Length);
            for (var i = 0; i < pixels.Length; i++) copiedPixels.Add(pixels[i]);
            this.pixels = new ReadOnlyCollection<byte>(copiedPixels);
        }

        public int Index { get; }
        public IReadOnlyList<byte> Pixels => pixels;
    }

    [Serializable]
    public struct SubtileDefinition
    {
        public SubtileDefinition(int tileIndex, int paletteIndex, bool horizontalFlip, bool verticalFlip)
        {
            if (tileIndex < 0 || tileIndex > 1023) throw new ArgumentOutOfRangeException(nameof(tileIndex));
            if (paletteIndex < 0 || paletteIndex > 15) throw new ArgumentOutOfRangeException(nameof(paletteIndex));

            TileIndex = tileIndex;
            PaletteIndex = paletteIndex;
            HorizontalFlip = horizontalFlip;
            VerticalFlip = verticalFlip;
        }

        public int TileIndex { get; }
        public int PaletteIndex { get; }
        public bool HorizontalFlip { get; }
        public bool VerticalFlip { get; }
    }

    [Serializable]
    public struct MetatileLayerRoute
    {
        public MetatileLayerRoute(RenderLayer firstPlane, RenderLayer secondPlane)
        {
            FirstPlane = firstPlane;
            SecondPlane = secondPlane;
        }

        public RenderLayer FirstPlane { get; }
        public RenderLayer SecondPlane { get; }
        public bool IsRenderable => FirstPlane != RenderLayer.Invalid && SecondPlane != RenderLayer.Invalid;
    }

    [Serializable]
    public sealed class MetatileDefinition
    {
        public MetatileDefinition(int index, IList<SubtileDefinition> subtiles, uint attributes, MetatileLayerRoute layerRoute)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (subtiles == null) throw new ArgumentNullException(nameof(subtiles));
            if (subtiles.Count != 8) throw new ArgumentException("A two-plane 16x16 metatile has exactly eight 8x8 subtiles.", nameof(subtiles));

            Index = index;
            Subtiles = new ReadOnlyCollection<SubtileDefinition>(new List<SubtileDefinition>(subtiles));
            Attributes = attributes;
            LayerRoute = layerRoute;
        }

        public int Index { get; }
        public IReadOnlyList<SubtileDefinition> Subtiles { get; }
        public uint Attributes { get; }
        public int Behavior => (int)(Attributes & 0x1FF);
        public int Terrain => (int)((Attributes >> 9) & 0x1F);
        public MetatileLayerRoute LayerRoute { get; }
    }

    [Serializable]
    public sealed class TileAnimationFrameDefinition
    {
        public TileAnimationFrameDefinition(IList<IndexedTileDefinition> tiles)
        {
            if (tiles == null || tiles.Count == 0) throw new ArgumentException("An animation frame needs at least one tile.", nameof(tiles));
            Tiles = new ReadOnlyCollection<IndexedTileDefinition>(new List<IndexedTileDefinition>(tiles));
        }

        public IReadOnlyList<IndexedTileDefinition> Tiles { get; }
    }

    [Serializable]
    public sealed class TileAnimationDefinition
    {
        public TileAnimationDefinition(string id, int destinationTileIndex, int durationTicks, IList<TileAnimationFrameDefinition> frames)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An animation id is required.", nameof(id));
            if (destinationTileIndex < 0) throw new ArgumentOutOfRangeException(nameof(destinationTileIndex));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (frames == null || frames.Count == 0) throw new ArgumentException("An animation needs at least one frame.", nameof(frames));

            Id = id;
            DestinationTileIndex = destinationTileIndex;
            DurationTicks = durationTicks;
            Frames = new ReadOnlyCollection<TileAnimationFrameDefinition>(new List<TileAnimationFrameDefinition>(frames));
        }

        public string Id { get; }
        public int DestinationTileIndex { get; }
        public int DurationTicks { get; }
        public IReadOnlyList<TileAnimationFrameDefinition> Frames { get; }
    }

    [Serializable]
    public sealed class TilesetDefinition
    {
        public TilesetDefinition(
            string id,
            bool isSecondary,
            IList<IndexedTileDefinition> tiles,
            IList<PaletteDefinition> palettes,
            IList<MetatileDefinition> metatiles,
            IList<TileAnimationDefinition> animations)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A tileset id is required.", nameof(id));
            if (tiles == null || palettes == null || metatiles == null || animations == null) throw new ArgumentNullException();

            Id = id;
            IsSecondary = isSecondary;
            Tiles = new ReadOnlyCollection<IndexedTileDefinition>(new List<IndexedTileDefinition>(tiles));
            Palettes = new ReadOnlyCollection<PaletteDefinition>(new List<PaletteDefinition>(palettes));
            Metatiles = new ReadOnlyCollection<MetatileDefinition>(new List<MetatileDefinition>(metatiles));
            Animations = new ReadOnlyCollection<TileAnimationDefinition>(new List<TileAnimationDefinition>(animations));
        }

        public string Id { get; }
        public bool IsSecondary { get; }
        public IReadOnlyList<IndexedTileDefinition> Tiles { get; }
        public IReadOnlyList<PaletteDefinition> Palettes { get; }
        public IReadOnlyList<MetatileDefinition> Metatiles { get; }
        public IReadOnlyList<TileAnimationDefinition> Animations { get; }
    }

    [Serializable]
    public struct MapCellDefinition
    {
        public MapCellDefinition(int metatileId, int collision, int elevation)
        {
            if (metatileId < 0 || metatileId > 1023) throw new ArgumentOutOfRangeException(nameof(metatileId));
            if (collision < 0 || collision > 3) throw new ArgumentOutOfRangeException(nameof(collision));
            if (elevation < 0 || elevation > 15) throw new ArgumentOutOfRangeException(nameof(elevation));

            MetatileId = metatileId;
            Collision = collision;
            Elevation = elevation;
        }

        public int MetatileId { get; }
        public int Collision { get; }
        public int Elevation { get; }
        public bool IsBlocked => Collision != 0;
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public MapDefinition(string id, string name, int width, int height, IList<MapCellDefinition> cells, TilesetDefinition primaryTileset, TilesetDefinition secondaryTileset)
            : this(id, name, width, height, cells, primaryTileset, secondaryTileset, new WarpDefinition[0])
        {
        }

        public MapDefinition(
            string id,
            string name,
            int width,
            int height,
            IList<MapCellDefinition> cells,
            TilesetDefinition primaryTileset,
            TilesetDefinition secondaryTileset,
            IList<WarpDefinition> warps)
            : this(id, name, width, height, cells, primaryTileset, secondaryTileset, warps, new NpcDefinition[0], new StaticMapPropDefinition[0])
        {
        }

        public MapDefinition(
            string id,
            string name,
            int width,
            int height,
            IList<MapCellDefinition> cells,
            TilesetDefinition primaryTileset,
            TilesetDefinition secondaryTileset,
            IList<WarpDefinition> warps,
            IList<NpcDefinition> npcs,
            IList<StaticMapPropDefinition> props)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Map id and name are required.");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            if (cells == null || cells.Count != checked(width * height)) throw new ArgumentException("Cell count must equal width multiplied by height.", nameof(cells));
            if (primaryTileset == null || secondaryTileset == null) throw new ArgumentNullException();
            if (warps == null) throw new ArgumentNullException(nameof(warps));
            if (npcs == null || props == null) throw new ArgumentNullException(npcs == null ? nameof(npcs) : nameof(props));

            var warpIds = new HashSet<string>(StringComparer.Ordinal);
            var warpIndexes = new HashSet<int>();
            var copiedWarps = new List<WarpDefinition>(warps.Count);
            for (var i = 0; i < warps.Count; i++)
            {
                var warp = warps[i] ?? throw new ArgumentException("Map warps cannot contain null.", nameof(warps));
                if (warp.SourceX >= width || warp.SourceY >= height)
                {
                    throw new ArgumentException("Warp source coordinates must be inside the map.", nameof(warps));
                }

                if (!warpIds.Add(warp.Id) || !warpIndexes.Add(warp.Index))
                {
                    throw new ArgumentException("Warp ids and indexes must be unique within a map.", nameof(warps));
                }

                copiedWarps.Add(warp);
            }

            var objectLocalIds = new HashSet<int>();
            var copiedNpcs = new List<NpcDefinition>(npcs.Count);
            for (var i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i] ?? throw new ArgumentException("Map NPCs cannot contain null.", nameof(npcs));
                if (npc.CellX >= width || npc.CellY >= height || !objectLocalIds.Add(npc.LocalId)) throw new ArgumentException("NPC coordinates and local ids must be unique and inside the map.", nameof(npcs));
                copiedNpcs.Add(npc);
            }

            var copiedProps = new List<StaticMapPropDefinition>(props.Count);
            for (var i = 0; i < props.Count; i++)
            {
                var prop = props[i] ?? throw new ArgumentException("Map props cannot contain null.", nameof(props));
                if (prop.CellX >= width || prop.CellY >= height || !objectLocalIds.Add(prop.LocalId)) throw new ArgumentException("Prop coordinates and local ids must be unique and inside the map.", nameof(props));
                copiedProps.Add(prop);
            }

            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Cells = new ReadOnlyCollection<MapCellDefinition>(new List<MapCellDefinition>(cells));
            PrimaryTileset = primaryTileset;
            SecondaryTileset = secondaryTileset;
            Warps = new ReadOnlyCollection<WarpDefinition>(copiedWarps);
            Npcs = new ReadOnlyCollection<NpcDefinition>(copiedNpcs);
            Props = new ReadOnlyCollection<StaticMapPropDefinition>(copiedProps);
        }

        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MapCellDefinition> Cells { get; }
        public TilesetDefinition PrimaryTileset { get; }
        public TilesetDefinition SecondaryTileset { get; }
        public IReadOnlyList<WarpDefinition> Warps { get; }
        public IReadOnlyList<NpcDefinition> Npcs { get; }
        public IReadOnlyList<StaticMapPropDefinition> Props { get; }
    }

    public enum NpcMovementPattern
    {
        FixedFacing,
        WanderCardinal
    }

    [Serializable]
    public sealed class NpcDefinition
    {
        public NpcDefinition(string eventId, int localId, string spriteId, int cellX, int cellY, int elevation, SpriteDirection initialDirection, NpcMovementPattern movementPattern, int minX, int maxX, int minY, int maxY, string interactionKey, string visibilityKey, bool visibleByDefault)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(spriteId) || string.IsNullOrWhiteSpace(interactionKey) || string.IsNullOrWhiteSpace(visibilityKey)) throw new ArgumentException("NPC identities, sprite and interaction keys are required.");
            if (localId <= 0 || cellX < 0 || cellY < 0 || elevation < 0 || elevation > 15 || minX < 0 || minY < 0 || maxX < minX || maxY < minY) throw new ArgumentOutOfRangeException();
            if (!DirectionalSpriteAnimationDefinition.IsCardinalDirection(initialDirection)) throw new ArgumentOutOfRangeException(nameof(initialDirection));
            if (movementPattern != NpcMovementPattern.FixedFacing && movementPattern != NpcMovementPattern.WanderCardinal) throw new ArgumentOutOfRangeException(nameof(movementPattern));
            if (movementPattern == NpcMovementPattern.FixedFacing && (minX != cellX || maxX != cellX || minY != cellY || maxY != cellY)) throw new ArgumentException("A fixed NPC must have a single-cell range.", nameof(movementPattern));
            if (movementPattern == NpcMovementPattern.WanderCardinal && (cellX < minX || cellX > maxX || cellY < minY || cellY > maxY)) throw new ArgumentException("A wandering NPC must start inside its range.", nameof(movementPattern));

            EventId = eventId; LocalId = localId; SpriteId = spriteId; CellX = cellX; CellY = cellY; Elevation = elevation; InitialDirection = initialDirection; MovementPattern = movementPattern; MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; InteractionKey = interactionKey; VisibilityKey = visibilityKey; VisibleByDefault = visibleByDefault;
        }

        public string EventId { get; } public int LocalId { get; } public string SpriteId { get; } public int CellX { get; } public int CellY { get; } public int Elevation { get; } public SpriteDirection InitialDirection { get; } public NpcMovementPattern MovementPattern { get; } public int MinX { get; } public int MaxX { get; } public int MinY { get; } public int MaxY { get; } public string InteractionKey { get; } public string VisibilityKey { get; } public bool VisibleByDefault { get; }
    }

    [Serializable]
    public sealed class StaticMapPropDefinition
    {
        public StaticMapPropDefinition(string eventId, int localId, string spriteId, int cellX, int cellY, int elevation, SpriteDirection initialDirection, string interactionKey, string visibilityKey, bool visibleByDefault)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(spriteId) || string.IsNullOrWhiteSpace(interactionKey) || string.IsNullOrWhiteSpace(visibilityKey)) throw new ArgumentException("Prop identities, sprite and interaction keys are required.");
            if (localId <= 0 || cellX < 0 || cellY < 0 || elevation < 0 || elevation > 15) throw new ArgumentOutOfRangeException();
            if (!DirectionalSpriteAnimationDefinition.IsCardinalDirection(initialDirection)) throw new ArgumentOutOfRangeException(nameof(initialDirection));
            EventId = eventId; LocalId = localId; SpriteId = spriteId; CellX = cellX; CellY = cellY; Elevation = elevation; InitialDirection = initialDirection; InteractionKey = interactionKey; VisibilityKey = visibilityKey; VisibleByDefault = visibleByDefault;
        }

        public string EventId { get; } public int LocalId { get; } public string SpriteId { get; } public int CellX { get; } public int CellY { get; } public int Elevation { get; } public SpriteDirection InitialDirection { get; } public string InteractionKey { get; } public string VisibilityKey { get; } public bool VisibleByDefault { get; }
    }

    [Serializable]
    public sealed class StaticSpriteDefinition
    {
        public StaticSpriteDefinition(string id, int width, int height, IList<Rgba32> palette, IList<IndexedSpriteFrameDefinition> frames)
        {
            if (string.IsNullOrWhiteSpace(id) || width <= 0 || height <= 0 || palette == null || palette.Count != OverworldSpriteDefinition.PaletteColorCount || frames == null || frames.Count == 0) throw new ArgumentException("A static sprite needs an id, dimensions, sixteen colours and frames.");
            for (var i = 0; i < frames.Count; i++) if (frames[i] == null || frames[i].Width != width || frames[i].Height != height) throw new ArgumentException("Static frames must match sprite dimensions.", nameof(frames));
            Id = id; Width = width; Height = height; Palette = new ReadOnlyCollection<Rgba32>(new List<Rgba32>(palette)); Frames = new ReadOnlyCollection<IndexedSpriteFrameDefinition>(new List<IndexedSpriteFrameDefinition>(frames));
        }

        public string Id { get; } public int Width { get; } public int Height { get; } public IReadOnlyList<Rgba32> Palette { get; } public IReadOnlyList<IndexedSpriteFrameDefinition> Frames { get; }
    }

    [Serializable]
    public sealed class ObjectSpriteCatalogDefinition
    {
        private readonly Dictionary<string, OverworldSpriteDefinition> mobileById;
        private readonly Dictionary<string, StaticSpriteDefinition> staticById;

        public ObjectSpriteCatalogDefinition(IList<OverworldSpriteDefinition> mobileSprites, IList<StaticSpriteDefinition> staticSprites)
        {
            if (mobileSprites == null || staticSprites == null) throw new ArgumentNullException();
            mobileById = new Dictionary<string, OverworldSpriteDefinition>(StringComparer.Ordinal);
            staticById = new Dictionary<string, StaticSpriteDefinition>(StringComparer.Ordinal);
            var mobile = new List<OverworldSpriteDefinition>(mobileSprites); var statics = new List<StaticSpriteDefinition>(staticSprites);
            for (var i = 0; i < mobile.Count; i++)
            {
                if (mobile[i] == null || mobileById.ContainsKey(mobile[i].Id)) throw new ArgumentException("Mobile object sprite ids must be unique.", nameof(mobileSprites));
                mobileById.Add(mobile[i].Id, mobile[i]);
            }
            for (var i = 0; i < statics.Count; i++)
            {
                if (statics[i] == null || staticById.ContainsKey(statics[i].Id) || mobileById.ContainsKey(statics[i].Id)) throw new ArgumentException("Object sprite ids must be unique.", nameof(staticSprites));
                staticById.Add(statics[i].Id, statics[i]);
            }
            mobile.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id)); statics.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            MobileSprites = new ReadOnlyCollection<OverworldSpriteDefinition>(mobile); StaticSprites = new ReadOnlyCollection<StaticSpriteDefinition>(statics);
        }

        public IReadOnlyList<OverworldSpriteDefinition> MobileSprites { get; } public IReadOnlyList<StaticSpriteDefinition> StaticSprites { get; }
        public bool TryGetMobile(string id, out OverworldSpriteDefinition sprite) { return mobileById.TryGetValue(id, out sprite); }
        public bool TryGetStatic(string id, out StaticSpriteDefinition sprite) { return staticById.TryGetValue(id, out sprite); }
    }

    public enum WarpActivation
    {
        DoorNorth,
        ArrowSouth,
        StairEast,
        StairWest,
        Inactive
    }

    /// <summary>Immutable map transition data normalized from a game's native event format.</summary>
    [Serializable]
    public sealed class WarpDefinition
    {
        public WarpDefinition(
            string id,
            int index,
            int sourceX,
            int sourceY,
            int sourceElevation,
            string destinationMapId,
            int destinationWarpIndex,
            WarpActivation activation,
            SpriteDirection destinationFacing)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A warp id is required.", nameof(id));
            if (index < 0 || sourceX < 0 || sourceY < 0) throw new ArgumentOutOfRangeException();
            if (sourceElevation < 0 || sourceElevation > 15) throw new ArgumentOutOfRangeException(nameof(sourceElevation));
            if (string.IsNullOrWhiteSpace(destinationMapId)) throw new ArgumentException("A destination map id is required.", nameof(destinationMapId));
            if (destinationWarpIndex < 0) throw new ArgumentOutOfRangeException(nameof(destinationWarpIndex));
            if (activation != WarpActivation.DoorNorth
                && activation != WarpActivation.ArrowSouth
                && activation != WarpActivation.StairEast
                && activation != WarpActivation.StairWest
                && activation != WarpActivation.Inactive) throw new ArgumentOutOfRangeException(nameof(activation));
            if (!DirectionalSpriteAnimationDefinition.IsCardinalDirection(destinationFacing)) throw new ArgumentOutOfRangeException(nameof(destinationFacing));

            Id = id;
            Index = index;
            SourceX = sourceX;
            SourceY = sourceY;
            SourceElevation = sourceElevation;
            DestinationMapId = destinationMapId;
            DestinationWarpIndex = destinationWarpIndex;
            Activation = activation;
            DestinationFacing = destinationFacing;
        }

        public string Id { get; }
        public int Index { get; }
        public int SourceX { get; }
        public int SourceY { get; }
        public int SourceElevation { get; }
        public string DestinationMapId { get; }
        public int DestinationWarpIndex { get; }
        public WarpActivation Activation { get; }
        public SpriteDirection DestinationFacing { get; }
    }

    /// <summary>Deterministically ordered, game-agnostic collection of maps and their transitions.</summary>
    [Serializable]
    public sealed class MapBundleDefinition
    {
        private readonly Dictionary<string, MapDefinition> mapsById;
        private readonly HashSet<string> externalDestinationMapIds;

        public MapBundleDefinition(IList<MapDefinition> maps, IList<string> permittedExternalDestinationMapIds = null)
        {
            if (maps == null || maps.Count == 0) throw new ArgumentException("A map bundle needs at least one map.", nameof(maps));

            var copiedMaps = new List<MapDefinition>(maps.Count);
            mapsById = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < maps.Count; i++)
            {
                var map = maps[i] ?? throw new ArgumentException("Map bundles cannot contain null maps.", nameof(maps));
                if (!mapsById.Add(map.Id, map)) throw new ArgumentException("Map ids must be unique within a bundle.", nameof(maps));
                copiedMaps.Add(map);
            }

            copiedMaps.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            externalDestinationMapIds = new HashSet<string>(StringComparer.Ordinal);
            if (permittedExternalDestinationMapIds != null)
            {
                for (var i = 0; i < permittedExternalDestinationMapIds.Count; i++)
                {
                    var id = permittedExternalDestinationMapIds[i];
                    if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("External map ids cannot be blank.", nameof(permittedExternalDestinationMapIds));
                    if (mapsById.ContainsKey(id)) throw new ArgumentException("An external destination cannot also be included in the bundle.", nameof(permittedExternalDestinationMapIds));
                    externalDestinationMapIds.Add(id);
                }
            }

            ValidateDestinations(copiedMaps);
            Maps = new ReadOnlyCollection<MapDefinition>(copiedMaps);
        }

        public IReadOnlyList<MapDefinition> Maps { get; }

        public bool TryGetMap(string id, out MapDefinition map) => mapsById.TryGetValue(id, out map);

        public MapDefinition GetMap(string id)
        {
            if (!mapsById.TryGetValue(id, out var map)) throw new KeyNotFoundException("Map is not in this bundle: " + id);
            return map;
        }

        public bool TryResolveDestination(WarpDefinition warp, out MapDefinition destinationMap, out WarpDefinition destinationWarp)
        {
            if (warp == null) throw new ArgumentNullException(nameof(warp));
            destinationMap = null;
            destinationWarp = null;
            if (!mapsById.TryGetValue(warp.DestinationMapId, out destinationMap)) return false;
            for (var i = 0; i < destinationMap.Warps.Count; i++)
            {
                if (destinationMap.Warps[i].Index == warp.DestinationWarpIndex)
                {
                    destinationWarp = destinationMap.Warps[i];
                    return true;
                }
            }

            return false;
        }

        private void ValidateDestinations(IList<MapDefinition> maps)
        {
            for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var map = maps[mapIndex];
                for (var warpIndex = 0; warpIndex < map.Warps.Count; warpIndex++)
                {
                    var warp = map.Warps[warpIndex];
                    if (!mapsById.TryGetValue(warp.DestinationMapId, out var destination))
                    {
                        if (!externalDestinationMapIds.Contains(warp.DestinationMapId))
                        {
                            throw new ArgumentException("Warp destination is neither in the bundle nor explicitly external: " + warp.DestinationMapId, nameof(maps));
                        }

                        continue;
                    }

                    var found = false;
                    for (var destinationWarpIndex = 0; destinationWarpIndex < destination.Warps.Count; destinationWarpIndex++)
                    {
                        if (destination.Warps[destinationWarpIndex].Index == warp.DestinationWarpIndex)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found) throw new ArgumentException("Warp destination index is not present in its destination map.", nameof(maps));
                }
            }
        }
    }
}
