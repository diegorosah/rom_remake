using System;
using System.Collections.Generic;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    public sealed class PalletTownParseResult
    {
        public PalletTownParseResult(MapDefinition map, OverworldSpriteDefinition playerSprite, ImportReport report)
        {
            Map = map;
            PlayerSprite = playerSprite;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public MapDefinition Map { get; }
        public OverworldSpriteDefinition PlayerSprite { get; }
        public ImportReport Report { get; }
        public bool Succeeded => Map != null && PlayerSprite != null && !Report.HasErrors;
    }

    public sealed class PalletTownParser
    {
        private const string Stage = "PalletTown";

        public PalletTownParseResult Parse(RomFile rom)
        {
            if (rom == null) throw new ArgumentNullException(nameof(rom));
            var bundleResult = new FireRedMapBundleParser().Parse(rom);
            if (!bundleResult.Succeeded)
            {
                return new PalletTownParseResult(null, null, bundleResult.Report);
            }

            return new PalletTownParseResult(bundleResult.Bundle.GetMap(FireRedRomLayoutRev1.PalletTownMapId), bundleResult.PlayerSprite, bundleResult.Report);
        }

        private static PalletTownParseResult ParseSupportedReader(RomReader reader, ImportReport report)
        {
            try
            {
                ValidateMapPointers(reader);
                var layout = FireRedRomLayoutRev1.PalletTownMapLayout;
                ValidateLayout(reader, layout);

                var primary = ParseTileset(
                    reader,
                    FireRedRomLayoutRev1.PalletTownPrimaryTileset,
                    "General",
                    false,
                    FireRedRomLayoutRev1.PrimaryTileCount,
                    FireRedRomLayoutRev1.PrimaryMetatileCount,
                    FireRedRomLayoutRev1.PrimaryPaletteCount,
                    0,
                    0,
                    FireRedRomLayoutRev1.GeneralTiles,
                    FireRedRomLayoutRev1.GeneralPalettes,
                    FireRedRomLayoutRev1.GeneralMetatiles,
                    FireRedRomLayoutRev1.GeneralMetatileAttributes,
                    FireRedRomLayoutRev1.GeneralAnimationCallback);
                var secondary = ParseTileset(
                    reader,
                    FireRedRomLayoutRev1.PalletTownSecondaryTileset,
                    "PalletTown",
                    true,
                    FireRedRomLayoutRev1.SecondaryTileCount,
                    FireRedRomLayoutRev1.SecondaryMetatileCount,
                    FireRedRomLayoutRev1.SecondaryPaletteCount,
                    FireRedRomLayoutRev1.SecondaryTileStart,
                    FireRedRomLayoutRev1.SecondaryMetatileStart,
                    FireRedRomLayoutRev1.PalletTownTiles,
                    FireRedRomLayoutRev1.PalletTownPalettes,
                    FireRedRomLayoutRev1.PalletTownMetatiles,
                    FireRedRomLayoutRev1.PalletTownMetatileAttributes,
                    0);

                var cells = ParseMapCells(reader, layout + 0x0C, primary, secondary);
                var map = new MapDefinition(
                    "MAP_PALLET_TOWN",
                    "Pallet Town",
                    FireRedRomLayoutRev1.PalletTownWidth,
                    FireRedRomLayoutRev1.PalletTownHeight,
                    cells,
                    primary,
                    secondary);
                var playerSprite = PlayerRedNormalParser.Parse(reader);
                report.Add(new ParseDiagnostic("Map", DiagnosticSeverity.Info, "Parsed Pallet Town (24x20, 480 cells).", FireRedRomLayoutRev1.PalletTownMapLayout, FireRedRomLayoutRev1.MapLayoutSize));
                report.Add(new ParseDiagnostic("PlayerSprite", DiagnosticSeverity.Info, "Parsed the normal on-foot player sprite (9 frames, 8 animations).", FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfo, FireRedRomLayoutRev1.ObjectEventGraphicsInfoSize));
                return new PalletTownParseResult(map, playerSprite, report);
            }
            catch (RomReadException exception)
            {
                report.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, exception.Message, exception.Offset, ToDiagnosticLength(exception.RequestedLength)));
            }
            catch (InvalidOperationException exception)
            {
                report.Add(new ParseDiagnostic("Format", DiagnosticSeverity.Error, exception.Message));
            }
            catch (OverflowException exception)
            {
                report.Add(new ParseDiagnostic("Safety", DiagnosticSeverity.Error, exception.Message));
            }

            return new PalletTownParseResult(null, null, report);
        }

        private static void ValidateMapPointers(RomReader reader)
        {
            reader.EnsureRange(FireRedRomLayoutRev1.PalletTownMapHeader, FireRedRomLayoutRev1.MapHeaderSize, "Pallet Town MapHeader is outside ROM bounds.");
            var layoutEntry = checked(FireRedRomLayoutRev1.MapLayoutsTable + ((FireRedRomLayoutRev1.PalletTownLayoutId - 1) * 4));
            ExpectPointer(reader, layoutEntry, FireRedRomLayoutRev1.PalletTownMapLayout, "Pallet Town layout table entry");
            ExpectPointer(reader, FireRedRomLayoutRev1.MapGroupsTable + (FireRedRomLayoutRev1.PalletTownMapGroup * 4), FireRedRomLayoutRev1.TownsAndRoutesMapGroup, "Towns and Routes map group");
            ExpectPointer(reader, FireRedRomLayoutRev1.TownsAndRoutesMapGroup + (FireRedRomLayoutRev1.PalletTownMapNumber * 4), FireRedRomLayoutRev1.PalletTownMapHeader, "Pallet Town map header");
            ExpectPointer(reader, FireRedRomLayoutRev1.PalletTownMapHeader, FireRedRomLayoutRev1.PalletTownMapLayout, "Pallet Town header layout");
            ExpectPointer(reader, FireRedRomLayoutRev1.PalletTownMapHeader + 4, FireRedRomLayoutRev1.PalletTownEvents, "Pallet Town header events");
            ExpectPointer(reader, FireRedRomLayoutRev1.PalletTownMapHeader + 8, FireRedRomLayoutRev1.PalletTownScripts, "Pallet Town header scripts");
            ExpectPointer(reader, FireRedRomLayoutRev1.PalletTownMapHeader + 0x0C, FireRedRomLayoutRev1.PalletTownConnections, "Pallet Town header connections");
            ExpectEqual(reader.ReadUInt16(FireRedRomLayoutRev1.PalletTownMapHeader + 0x10), 0x012C, "Pallet Town music", FireRedRomLayoutRev1.PalletTownMapHeader + 0x10);
            ExpectEqual(reader.ReadUInt16(FireRedRomLayoutRev1.PalletTownMapHeader + 0x12), FireRedRomLayoutRev1.PalletTownLayoutId, "Pallet Town layout id", FireRedRomLayoutRev1.PalletTownMapHeader + 0x12);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x14), 0x58, "Pallet Town region section", FireRedRomLayoutRev1.PalletTownMapHeader + 0x14);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x15), 0, "Pallet Town cave flag", FireRedRomLayoutRev1.PalletTownMapHeader + 0x15);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x16), 2, "Pallet Town weather", FireRedRomLayoutRev1.PalletTownMapHeader + 0x16);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x17), 1, "Pallet Town map type", FireRedRomLayoutRev1.PalletTownMapHeader + 0x17);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x18), 1, "Pallet Town biking flag", FireRedRomLayoutRev1.PalletTownMapHeader + 0x18);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x19), 0x06, "Pallet Town flags", FireRedRomLayoutRev1.PalletTownMapHeader + 0x19);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x1A), 0, "Pallet Town floor", FireRedRomLayoutRev1.PalletTownMapHeader + 0x1A);
            ExpectEqual(reader.ReadByte(FireRedRomLayoutRev1.PalletTownMapHeader + 0x1B), 0, "Pallet Town battle scene", FireRedRomLayoutRev1.PalletTownMapHeader + 0x1B);
        }

        private static void ValidateLayout(RomReader reader, int layout)
        {
            reader.EnsureRange(layout, FireRedRomLayoutRev1.MapLayoutSize, "Pallet Town MapLayout is outside ROM bounds.");
            ExpectEqual(reader.ReadUInt32(layout), FireRedRomLayoutRev1.PalletTownWidth, "Pallet Town layout width", layout);
            ExpectEqual(reader.ReadUInt32(layout + 4), FireRedRomLayoutRev1.PalletTownHeight, "Pallet Town layout height", layout + 4);
            ExpectPointer(reader, layout + 8, FireRedRomLayoutRev1.PalletTownBorderCells, "Pallet Town border data");
            ExpectPointer(reader, layout + 0x0C, FireRedRomLayoutRev1.PalletTownMapCells, "Pallet Town map cells");
            ExpectPointer(reader, layout + 0x10, FireRedRomLayoutRev1.PalletTownPrimaryTileset, "Pallet Town primary tileset");
            ExpectPointer(reader, layout + 0x14, FireRedRomLayoutRev1.PalletTownSecondaryTileset, "Pallet Town secondary tileset");
            if (reader.ReadByte(layout + 0x18) != 2 || reader.ReadByte(layout + 0x19) != 2)
            {
                throw new RomReadException("Pallet Town border dimensions do not match the verified rev1 layout.", layout + 0x18, 2, reader.Length);
            }
        }

        private static TilesetDefinition ParseTileset(
            RomReader reader,
            int headerOffset,
            string id,
            bool secondary,
            int tileCount,
            int metatileCount,
            int paletteCount,
            int tileStart,
            int metatileStart,
            int expectedTiles,
            int expectedPalettes,
            int expectedMetatiles,
            int expectedAttributes,
            uint expectedCallback)
        {
            reader.EnsureRange(headerOffset, FireRedRomLayoutRev1.TilesetSize, id + " tileset header is outside ROM bounds.");
            if (reader.ReadByte(headerOffset) != 1 || reader.ReadByte(headerOffset + 1) != (secondary ? 1 : 0))
            {
                throw new RomReadException(id + " tileset flags do not match the verified rev1 layout.", headerOffset, 2, reader.Length);
            }

            ExpectPointer(reader, headerOffset + 4, expectedTiles, id + " compressed tiles");
            ExpectPointer(reader, headerOffset + 8, expectedPalettes, id + " palettes");
            ExpectPointer(reader, headerOffset + 0x0C, expectedMetatiles, id + " metatiles");
            ExpectPointer(reader, headerOffset + 0x14, expectedAttributes, id + " metatile attributes");
            ExpectCallback(reader, headerOffset + 0x10, expectedCallback, id + " animation callback");

            var packedTiles = GbaLz10Decoder.Decode(reader, expectedTiles, checked(tileCount * FireRedGraphicsDecoder.BytesPer4BppTile));
            if (packedTiles.Length != tileCount * FireRedGraphicsDecoder.BytesPer4BppTile)
            {
                throw new RomReadException(id + " LZ10 output length does not match its verified tile count.", expectedTiles, packedTiles.Length, reader.Length);
            }

            var tiles = FireRedGraphicsDecoder.Decode4BppTiles(packedTiles, tileStart);
            var palettes = DecodePalettes(reader, expectedPalettes, paletteCount, secondary ? FireRedRomLayoutRev1.PrimaryPaletteCount : 0);
            var metatiles = DecodeMetatiles(reader, expectedMetatiles, expectedAttributes, metatileCount, metatileStart, tileStart, tileCount, id);
            var animations = secondary ? new List<TileAnimationDefinition>() : DecodeGeneralAnimations(reader);
            return new TilesetDefinition(id, secondary, tiles, palettes, metatiles, animations);
        }

        private static List<MapCellDefinition> ParseMapCells(RomReader reader, int mapPointerField, TilesetDefinition primary, TilesetDefinition secondary)
        {
            var mapOffset = ResolvePointer(reader, mapPointerField, "Pallet Town map cells");
            var count = checked(FireRedRomLayoutRev1.PalletTownWidth * FireRedRomLayoutRev1.PalletTownHeight);
            reader.EnsureRange(mapOffset, checked(count * 2), "Pallet Town map cells are outside ROM bounds.");
            var cells = new List<MapCellDefinition>(count);
            for (var i = 0; i < count; i++)
            {
                var raw = reader.ReadUInt16(mapOffset + (i * 2));
                var metatileId = raw & 0x03FF;
                if (metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart)
                {
                    if (metatileId >= primary.Metatiles.Count) throw new RomReadException("Pallet Town references an unavailable primary metatile.", mapOffset + (i * 2), 2, reader.Length);
                    if (!primary.Metatiles[metatileId].LayerRoute.IsRenderable) throw new RomReadException("Pallet Town references an invalid primary metatile layer type.", mapOffset + (i * 2), 2, reader.Length);
                }
                else if (metatileId >= FireRedRomLayoutRev1.SecondaryMetatileStart + secondary.Metatiles.Count)
                {
                    throw new RomReadException("Pallet Town references an unavailable secondary metatile.", mapOffset + (i * 2), 2, reader.Length);
                }
                else if (!secondary.Metatiles[metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart].LayerRoute.IsRenderable)
                {
                    throw new RomReadException("Pallet Town references an invalid secondary metatile layer type.", mapOffset + (i * 2), 2, reader.Length);
                }

                cells.Add(FireRedMapEncoding.DecodeMapCell(raw));
            }

            return cells;
        }

        private static List<PaletteDefinition> DecodePalettes(RomReader reader, int offset, int paletteCount, int paletteStart)
        {
            reader.EnsureRange(offset, checked(paletteCount * 32), "Tileset palette data is outside ROM bounds.");
            var palettes = new List<PaletteDefinition>(paletteCount);
            for (var palette = 0; palette < paletteCount; palette++)
            {
                var colors = new List<Rgba32>(16);
                for (var color = 0; color < 16; color++)
                {
                    var value = reader.ReadUInt16(offset + (palette * 32) + (color * 2));
                    colors.Add(FireRedGraphicsDecoder.DecodeBgr555(value, color == 0 ? (byte)0 : (byte)255));
                }

                palettes.Add(new PaletteDefinition(paletteStart + palette, colors));
            }

            return palettes;
        }

        private static List<MetatileDefinition> DecodeMetatiles(RomReader reader, int metatilesOffset, int attributesOffset, int count, int metatileStart, int tileStart, int tileCount, string tilesetId)
        {
            reader.EnsureRange(metatilesOffset, checked(count * 16), tilesetId + " metatile data is outside ROM bounds.");
            reader.EnsureRange(attributesOffset, checked(count * 4), tilesetId + " metatile attributes are outside ROM bounds.");
            var definitions = new List<MetatileDefinition>(count);
            for (var metatile = 0; metatile < count; metatile++)
            {
                var subtiles = new List<SubtileDefinition>(FireRedRomLayoutRev1.SubtilesPerMetatile);
                for (var subtile = 0; subtile < FireRedRomLayoutRev1.SubtilesPerMetatile; subtile++)
                {
                    var value = reader.ReadUInt16(metatilesOffset + (metatile * 16) + (subtile * 2));
                    var tileIndex = value & 0x03FF;
                    // Secondary metatiles may reference already-loaded primary tiles as well as
                    // their own global slots. Both use the PPU's global tile index space.
                    if (tileIndex >= tileStart + tileCount)
                    {
                        throw new RomReadException(tilesetId + " metatile references a tile outside its verified tile range.", metatilesOffset + (metatile * 16) + (subtile * 2), 2, reader.Length);
                    }

                    subtiles.Add(FireRedMapEncoding.DecodeSubtile(value));
                }

                var attributes = reader.ReadUInt32(attributesOffset + (metatile * 4));
                definitions.Add(new MetatileDefinition(metatileStart + metatile, subtiles, attributes, FireRedMapEncoding.DecodeLayerRoute(attributes)));
            }

            return definitions;
        }

        private static List<TileAnimationDefinition> DecodeGeneralAnimations(RomReader reader)
        {
            return new List<TileAnimationDefinition>
            {
                DecodeAnimation(reader, "flower", 508, 4, 16, FireRedRomLayoutRev1.GeneralFlowerAnimationFrames),
                DecodeAnimation(reader, "water", 416, 48, 16, FireRedRomLayoutRev1.GeneralWaterAnimationFrames),
                DecodeAnimation(reader, "sand", 464, 18, 8, FireRedRomLayoutRev1.GeneralSandAnimationFrames)
            };
        }

        private static TileAnimationDefinition DecodeAnimation(RomReader reader, string id, int destination, int tilesPerFrame, int durationTicks, int[] frameOffsets)
        {
            var frames = new List<TileAnimationFrameDefinition>(frameOffsets.Length);
            var byteCount = checked(tilesPerFrame * FireRedGraphicsDecoder.BytesPer4BppTile);
            for (var frame = 0; frame < frameOffsets.Length; frame++)
            {
                reader.EnsureRange(frameOffsets[frame], byteCount, id + " animation frame is outside ROM bounds.");
                frames.Add(new TileAnimationFrameDefinition(FireRedGraphicsDecoder.Decode4BppTiles(reader.ReadBytes(frameOffsets[frame], byteCount), destination)));
            }

            return new TileAnimationDefinition(id, destination, durationTicks, frames);
        }

        private static void ExpectPointer(RomReader reader, int pointerField, int expectedOffset, string description)
        {
            var actual = ResolvePointer(reader, pointerField, description);
            if (actual != expectedOffset)
            {
                throw new RomReadException(description + " does not match the verified rev1 location.", pointerField, 4, reader.Length);
            }
        }

        private static int ResolvePointer(RomReader reader, int pointerField, string description)
        {
            reader.EnsureRange(pointerField, 4, description + " pointer is outside ROM bounds.");
            return reader.ConvertGbaPointer(reader.ReadUInt32(pointerField));
        }

        private static void ExpectEqual(uint actual, uint expected, string description, int offset)
        {
            if (actual != expected) throw new InvalidOperationException(description + " does not match the verified rev1 layout at 0x" + offset.ToString("X") + ".");
        }

        private static void ExpectCallback(RomReader reader, int pointerField, uint expectedPointer, string description)
        {
            var actual = reader.ReadUInt32(pointerField);
            if ((actual & FireRedRomLayoutRev1.ThumbPointerAddressMask)
                != (expectedPointer & FireRedRomLayoutRev1.ThumbPointerAddressMask))
            {
                throw new RomReadException(description + " does not match the verified rev1 callback.", pointerField, 4, reader.Length);
            }
        }

        private static int ToDiagnosticLength(long length)
        {
            return length > int.MaxValue ? int.MaxValue : (int)Math.Max(0, length);
        }
    }
}
