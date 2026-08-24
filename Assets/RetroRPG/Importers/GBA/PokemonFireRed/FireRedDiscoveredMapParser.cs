using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>
    /// Conservative parser for ROM-discovered maps. It imports layout, collision and
    /// warp topology without attempting unknown object-event scripts or dialogue.
    /// </summary>
    internal static class FireRedDiscoveredMapParser
    {
        private const int MaximumTilesetOutputBytes = 128 * 1024;
        private const int MaximumPrimaryMetatiles = FireRedRomLayoutRev1.SecondaryMetatileStart;
        private const int MaximumSecondaryMetatiles = 1024 - FireRedRomLayoutRev1.SecondaryMetatileStart;

        public static MapDefinition Parse(RomReader reader, FireRedDiscoveredMapSpec spec, FireRedMapCatalogScanResult discovery)
        {
            int ignoredPlaceholderCount;
            return Parse(reader, spec, discovery, out ignoredPlaceholderCount);
        }

        public static MapDefinition Parse(
            RomReader reader,
            FireRedDiscoveredMapSpec spec,
            FireRedMapCatalogScanResult discovery,
            out int placeholderTileCount)
        {
            if (reader == null || spec == null || discovery == null) throw new ArgumentNullException();

            var rawCells = ReadRawCells(reader, spec);
            DetermineRequiredMetatiles(rawCells, out var primaryMetatileCount, out var secondaryMetatileCount);
            var primary = ParseTileset(reader, spec.PrimaryTilesetHeaderOffset, false, primaryMetatileCount);
            var secondary = ParseTileset(reader, spec.SecondaryTilesetHeaderOffset, true, secondaryMetatileCount);

            // FireRed copies the base compressed tiles first and initializes tileset
            // animation callbacks separately. Some discovered metatiles therefore refer
            // to legal VRAM tile slots which are not present in the base LZ10 stream.
            // The generic importer does not execute arbitrary ROM callbacks yet, so
            // reserve those referenced slots with transparent placeholder tiles instead
            // of rejecting the entire map. Audited maps keep their exact animation data.
            AddReferencedPlaceholderTiles(rawCells, primary, secondary, out primary, out secondary, out placeholderTileCount);
            ValidateReferencedResources(spec, rawCells, primary, secondary);

            var cells = DecodeCells(reader, spec, rawCells, primary, secondary);
            var warps = ParseWarps(reader, spec, cells, primary, secondary);

            return new MapDefinition(
                spec.Id,
                spec.Name,
                spec.Width,
                spec.Height,
                cells,
                primary,
                secondary,
                warps,
                new NpcDefinition[0],
                new StaticMapPropDefinition[0]);
        }

        private static ushort[] ReadRawCells(RomReader reader, FireRedDiscoveredMapSpec spec)
        {
            var count = checked(spec.Width * spec.Height);
            reader.EnsureRange(spec.MapCellsOffset, checked(count * 2), spec.Name + " map cells are outside ROM bounds.");
            var raw = new ushort[count];
            for (var index = 0; index < count; index++) raw[index] = reader.ReadUInt16(checked(spec.MapCellsOffset + (index * 2)));
            return raw;
        }

        private static void DetermineRequiredMetatiles(IList<ushort> rawCells, out int primaryCount, out int secondaryCount)
        {
            var maxPrimary = -1;
            var maxSecondary = -1;
            for (var index = 0; index < rawCells.Count; index++)
            {
                var metatileId = rawCells[index] & 0x03FF;
                if (metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart)
                {
                    if (metatileId > maxPrimary) maxPrimary = metatileId;
                }
                else
                {
                    var local = metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart;
                    if (local > maxSecondary) maxSecondary = local;
                }
            }

            primaryCount = maxPrimary + 1;
            secondaryCount = maxSecondary + 1;
            if (primaryCount <= 0) primaryCount = 1;
            if (primaryCount > MaximumPrimaryMetatiles || secondaryCount > MaximumSecondaryMetatiles)
            {
                throw new InvalidOperationException("Discovered map references metatiles outside the FireRed 10-bit primary/secondary split.");
            }
        }

        private static TilesetDefinition ParseTileset(RomReader reader, int headerOffset, bool secondary, int requiredMetatileCount)
        {
            reader.EnsureRange(headerOffset, FireRedRomLayoutRev1.TilesetSize, "Tileset header is outside ROM bounds.");
            var compressed = reader.ReadByte(headerOffset);
            var secondaryFlag = reader.ReadByte(checked(headerOffset + 1));
            if (compressed != 1)
            {
                throw new RomReadException("Discovered tileset is not LZ10-compressed and is not yet supported by the generic importer.", headerOffset, FireRedRomLayoutRev1.TilesetSize, reader.Length);
            }
            if (secondaryFlag != (secondary ? 1 : 0))
            {
                throw new RomReadException("Discovered tileset primary/secondary flag is inconsistent with its map layout slot.", checked(headerOffset + 1), 1, reader.Length);
            }

            var tilesOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(headerOffset + 4)), 4);
            var palettesOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(headerOffset + 8)), 32);
            var metatilesOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(headerOffset + 0x0C)), Math.Max(1, requiredMetatileCount * 16));
            var attributesOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(headerOffset + 0x14)), Math.Max(1, requiredMetatileCount * 4));

            var declaredTileBytes = ReadLz10DeclaredLength(reader, tilesOffset);
            if (declaredTileBytes <= 0 || declaredTileBytes > MaximumTilesetOutputBytes || declaredTileBytes % FireRedGraphicsDecoder.BytesPer4BppTile != 0)
            {
                throw new RomReadException("Discovered tileset has an unsupported decoded tile length.", tilesOffset, declaredTileBytes, reader.Length);
            }

            var tileCount = declaredTileBytes / FireRedGraphicsDecoder.BytesPer4BppTile;
            var maximumTiles = secondary ? MaximumSecondaryMetatiles : FireRedRomLayoutRev1.SecondaryTileStart;
            if (tileCount <= 0 || tileCount > maximumTiles)
            {
                throw new RomReadException("Discovered tileset tile count exceeds the configured FireRed bank limit.", tilesOffset, declaredTileBytes, reader.Length);
            }

            var packedTiles = GbaLz10Decoder.Decode(reader, tilesOffset, declaredTileBytes);
            var tileStart = secondary ? FireRedRomLayoutRev1.SecondaryTileStart : 0;
            var tiles = FireRedGraphicsDecoder.Decode4BppTiles(packedTiles, tileStart);
            var palettes = DecodePalettes(
                reader,
                palettesOffset,
                secondary ? FireRedRomLayoutRev1.SecondaryPaletteCount : FireRedRomLayoutRev1.PrimaryPaletteCount,
                secondary ? FireRedRomLayoutRev1.PrimaryPaletteCount : 0);
            var metatiles = DecodeMetatiles(reader, metatilesOffset, attributesOffset, requiredMetatileCount, secondary ? FireRedRomLayoutRev1.SecondaryMetatileStart : 0);
            var id = "tileset_" + headerOffset.ToString("X6", System.Globalization.CultureInfo.InvariantCulture);
            return new TilesetDefinition(id, secondary, tiles, palettes, metatiles, new TileAnimationDefinition[0]);
        }

        private static int ReadLz10DeclaredLength(RomReader reader, int offset)
        {
            reader.EnsureRange(offset, 4, "LZ10 header is outside ROM bounds.");
            if (reader.ReadByte(offset) != 0x10) throw new RomReadException("Expected an LZ10 stream marker (0x10).", offset, 1, reader.Length);
            return reader.ReadByte(offset + 1) | (reader.ReadByte(offset + 2) << 8) | (reader.ReadByte(offset + 3) << 16);
        }

        private static List<PaletteDefinition> DecodePalettes(RomReader reader, int offset, int count, int start)
        {
            reader.EnsureRange(offset, checked(count * 32), "Tileset palette data is outside ROM bounds.");
            var palettes = new List<PaletteDefinition>(count);
            for (var palette = 0; palette < count; palette++)
            {
                var colors = new List<Rgba32>(16);
                for (var color = 0; color < 16; color++)
                {
                    colors.Add(FireRedGraphicsDecoder.DecodeBgr555(
                        reader.ReadUInt16(checked(offset + (palette * 32) + (color * 2))),
                        color == 0 ? (byte)0 : (byte)255));
                }
                palettes.Add(new PaletteDefinition(start + palette, colors));
            }
            return palettes;
        }

        private static List<MetatileDefinition> DecodeMetatiles(RomReader reader, int metatilesOffset, int attributesOffset, int count, int start)
        {
            if (count <= 0) return new List<MetatileDefinition>();
            reader.EnsureRange(metatilesOffset, checked(count * 16), "Tileset metatile data is outside ROM bounds.");
            reader.EnsureRange(attributesOffset, checked(count * 4), "Tileset metatile attributes are outside ROM bounds.");
            var definitions = new List<MetatileDefinition>(count);
            for (var metatile = 0; metatile < count; metatile++)
            {
                var subtiles = new List<SubtileDefinition>(FireRedRomLayoutRev1.SubtilesPerMetatile);
                for (var subtile = 0; subtile < FireRedRomLayoutRev1.SubtilesPerMetatile; subtile++)
                {
                    var offset = checked(metatilesOffset + (metatile * 16) + (subtile * 2));
                    subtiles.Add(FireRedMapEncoding.DecodeSubtile(reader.ReadUInt16(offset)));
                }

                var attributes = reader.ReadUInt32(checked(attributesOffset + (metatile * 4)));
                definitions.Add(new MetatileDefinition(start + metatile, subtiles, attributes, FireRedMapEncoding.DecodeLayerRoute(attributes)));
            }
            return definitions;
        }

        private static void AddReferencedPlaceholderTiles(
            IList<ushort> rawCells,
            TilesetDefinition primary,
            TilesetDefinition secondary,
            out TilesetDefinition updatedPrimary,
            out TilesetDefinition updatedSecondary,
            out int placeholderTileCount)
        {
            var availableTiles = new HashSet<int>();
            AddTileIds(availableTiles, primary);
            AddTileIds(availableTiles, secondary);

            var missingPrimary = new SortedSet<int>();
            var missingSecondary = new SortedSet<int>();
            var visitedMetatiles = new HashSet<int>();

            for (var cellIndex = 0; cellIndex < rawCells.Count; cellIndex++)
            {
                var metatileId = rawCells[cellIndex] & 0x03FF;
                if (!visitedMetatiles.Add(metatileId)) continue;

                var metatile = metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart
                    ? primary.Metatiles[metatileId]
                    : secondary.Metatiles[metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart];

                for (var subtileIndex = 0; subtileIndex < metatile.Subtiles.Count; subtileIndex++)
                {
                    var tileIndex = metatile.Subtiles[subtileIndex].TileIndex;
                    if (availableTiles.Contains(tileIndex)) continue;

                    if (tileIndex < FireRedRomLayoutRev1.SecondaryTileStart)
                    {
                        missingPrimary.Add(tileIndex);
                    }
                    else
                    {
                        missingSecondary.Add(tileIndex);
                    }
                }
            }

            updatedPrimary = WithPlaceholderTiles(primary, missingPrimary);
            updatedSecondary = WithPlaceholderTiles(secondary, missingSecondary);
            placeholderTileCount = checked(missingPrimary.Count + missingSecondary.Count);
        }

        private static TilesetDefinition WithPlaceholderTiles(TilesetDefinition source, IEnumerable<int> placeholderIds)
        {
            var tiles = new List<IndexedTileDefinition>(source.Tiles.Count);
            for (var index = 0; index < source.Tiles.Count; index++) tiles.Add(source.Tiles[index]);

            foreach (var tileId in placeholderIds)
            {
                tiles.Add(new IndexedTileDefinition(tileId, new byte[IndexedTileDefinition.PixelCount]));
            }

            tiles.Sort((left, right) => left.Index.CompareTo(right.Index));
            return new TilesetDefinition(
                source.Id,
                source.IsSecondary,
                tiles,
                new List<PaletteDefinition>(source.Palettes),
                new List<MetatileDefinition>(source.Metatiles),
                new List<TileAnimationDefinition>(source.Animations));
        }

        private static void ValidateReferencedResources(
            FireRedDiscoveredMapSpec spec,
            IList<ushort> rawCells,
            TilesetDefinition primary,
            TilesetDefinition secondary)
        {
            var availableTiles = new HashSet<int>();
            AddTileIds(availableTiles, primary);
            AddTileIds(availableTiles, secondary);

            var availablePalettes = new HashSet<int>();
            AddPaletteIds(availablePalettes, primary);
            AddPaletteIds(availablePalettes, secondary);

            var visitedMetatiles = new HashSet<int>();
            for (var cellIndex = 0; cellIndex < rawCells.Count; cellIndex++)
            {
                var metatileId = rawCells[cellIndex] & 0x03FF;
                if (!visitedMetatiles.Add(metatileId)) continue;

                var metatile = metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart
                    ? primary.Metatiles[metatileId]
                    : secondary.Metatiles[metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart];

                for (var subtileIndex = 0; subtileIndex < metatile.Subtiles.Count; subtileIndex++)
                {
                    var subtile = metatile.Subtiles[subtileIndex];
                    if (!availableTiles.Contains(subtile.TileIndex))
                    {
                        throw new InvalidOperationException(
                            spec.Name + " metatile " + metatile.Index + " still references unavailable tile " + subtile.TileIndex + ".");
                    }
                    if (!availablePalettes.Contains(subtile.PaletteIndex))
                    {
                        throw new InvalidOperationException(
                            spec.Name + " metatile " + metatile.Index + " references unavailable palette " + subtile.PaletteIndex + ".");
                    }
                }
            }
        }

        private static void AddTileIds(ISet<int> destination, TilesetDefinition tileset)
        {
            for (var index = 0; index < tileset.Tiles.Count; index++) destination.Add(tileset.Tiles[index].Index);
        }

        private static void AddPaletteIds(ISet<int> destination, TilesetDefinition tileset)
        {
            for (var index = 0; index < tileset.Palettes.Count; index++) destination.Add(tileset.Palettes[index].Index);
        }

        private static List<MapCellDefinition> DecodeCells(RomReader reader, FireRedDiscoveredMapSpec spec, IList<ushort> rawCells, TilesetDefinition primary, TilesetDefinition secondary)
        {
            var cells = new List<MapCellDefinition>(rawCells.Count);
            for (var index = 0; index < rawCells.Count; index++)
            {
                var raw = rawCells[index];
                var metatileId = raw & 0x03FF;
                var metatile = metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart
                    ? GetMetatile(primary, metatileId, spec.MapCellsOffset + (index * 2), reader, spec.Name)
                    : GetMetatile(secondary, metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart, spec.MapCellsOffset + (index * 2), reader, spec.Name);
                if (!metatile.LayerRoute.IsRenderable)
                {
                    throw new RomReadException(spec.Name + " references an invalid metatile layer type.", spec.MapCellsOffset + (index * 2), 2, reader.Length);
                }
                cells.Add(FireRedMapEncoding.DecodeMapCell(raw));
            }
            return cells;
        }

        private static List<WarpDefinition> ParseWarps(
            RomReader reader,
            FireRedDiscoveredMapSpec spec,
            IList<MapCellDefinition> cells,
            TilesetDefinition primary,
            TilesetDefinition secondary)
        {
            var result = new List<WarpDefinition>();
            if (spec.EventsOffset == 0) return result;

            reader.EnsureRange(spec.EventsOffset, FireRedRomLayoutRev1.MapEventsSize, spec.Name + " MapEvents is outside ROM bounds.");
            var warpCount = reader.ReadByte(checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsWarpCountOffset));
            if (warpCount == 0) return result;

            var warpPointerField = checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsWarpPointerOffset);
            var warpOffset = reader.ConvertGbaPointer(reader.ReadUInt32(warpPointerField), checked(warpCount * FireRedRomLayoutRev1.WarpEventSize));
            for (var index = 0; index < warpCount; index++)
            {
                var offset = checked(warpOffset + (index * FireRedRomLayoutRev1.WarpEventSize));
                var x = ReadInt16(reader, checked(offset + FireRedRomLayoutRev1.WarpEventXOffset));
                var y = ReadInt16(reader, checked(offset + FireRedRomLayoutRev1.WarpEventYOffset));
                var elevation = reader.ReadByte(checked(offset + FireRedRomLayoutRev1.WarpEventElevationOffset));
                if (x < 0 || y < 0 || x >= spec.Width || y >= spec.Height)
                {
                    throw new RomReadException(spec.Name + " warp source coordinates are outside the map.", offset, FireRedRomLayoutRev1.WarpEventSize, reader.Length);
                }

                var destinationWarp = reader.ReadByte(checked(offset + FireRedRomLayoutRev1.WarpEventDestinationWarpIndexOffset));
                var destinationMapNumber = reader.ReadByte(checked(offset + FireRedRomLayoutRev1.WarpEventDestinationMapNumberOffset));
                var destinationMapGroup = reader.ReadByte(checked(offset + FireRedRomLayoutRev1.WarpEventDestinationMapGroupOffset));
                var destinationId = FireRedMapCatalogScanner.MapId(destinationMapGroup, destinationMapNumber);
                var activation = ResolveActivation(cells[checked((y * spec.Width) + x)], primary, secondary);
                result.Add(new WarpDefinition(
                    spec.Id + ":warp:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    index,
                    x,
                    y,
                    elevation,
                    destinationId,
                    destinationWarp,
                    activation,
                    DestinationFacing(activation)));
            }
            return result;
        }

        private static WarpActivation ResolveActivation(MapCellDefinition cell, TilesetDefinition primary, TilesetDefinition secondary)
        {
            var metatile = cell.MetatileId < FireRedRomLayoutRev1.SecondaryMetatileStart
                ? primary.Metatiles[cell.MetatileId]
                : secondary.Metatiles[cell.MetatileId - FireRedRomLayoutRev1.SecondaryMetatileStart];
            switch (metatile.Behavior)
            {
                case FireRedRomLayoutRev1.WarpDoorBehavior: return WarpActivation.DoorNorth;
                case FireRedRomLayoutRev1.SouthArrowBehavior: return WarpActivation.ArrowSouth;
                case FireRedRomLayoutRev1.UpRightStairBehavior: return WarpActivation.StairEast;
                case FireRedRomLayoutRev1.DownLeftStairBehavior: return WarpActivation.StairWest;
                default: return WarpActivation.Inactive;
            }
        }

        private static SpriteDirection DestinationFacing(WarpActivation activation)
        {
            switch (activation)
            {
                case WarpActivation.DoorNorth: return SpriteDirection.South;
                case WarpActivation.ArrowSouth: return SpriteDirection.North;
                case WarpActivation.StairEast: return SpriteDirection.West;
                case WarpActivation.StairWest: return SpriteDirection.East;
                default: return SpriteDirection.South;
            }
        }

        private static MetatileDefinition GetMetatile(TilesetDefinition tileset, int index, int offset, RomReader reader, string mapName)
        {
            if (index < 0 || index >= tileset.Metatiles.Count)
            {
                throw new RomReadException(mapName + " references an unavailable metatile.", offset, 2, reader.Length);
            }
            return tileset.Metatiles[index];
        }

        private static short ReadInt16(RomReader reader, int offset)
        {
            return unchecked((short)reader.ReadUInt16(offset));
        }
    }
}
