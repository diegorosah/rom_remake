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
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public MapDefinition(string id, string name, int width, int height, IList<MapCellDefinition> cells, TilesetDefinition primaryTileset, TilesetDefinition secondaryTileset)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Map id and name are required.");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            if (cells == null || cells.Count != checked(width * height)) throw new ArgumentException("Cell count must equal width multiplied by height.", nameof(cells));
            if (primaryTileset == null || secondaryTileset == null) throw new ArgumentNullException();

            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Cells = new ReadOnlyCollection<MapCellDefinition>(new List<MapCellDefinition>(cells));
            PrimaryTileset = primaryTileset;
            SecondaryTileset = secondaryTileset;
        }

        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MapCellDefinition> Cells { get; }
        public TilesetDefinition PrimaryTileset { get; }
        public TilesetDefinition SecondaryTileset { get; }
    }
}
