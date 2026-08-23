using System;
using System.Collections.Generic;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    public sealed class FireRedMapBundleParseResult
    {
        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ImportReport report)
            : this(bundle, playerSprite, null, null, null, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, ImportReport report)
            : this(bundle, playerSprite, objectSprites, null, null, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, DialogueCatalogDefinition dialogueCatalog, ImportReport report)
            : this(bundle, playerSprite, objectSprites, dialogueCatalog, null, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, DialogueCatalogDefinition dialogueCatalog, EncounterCatalogDefinition encounterCatalog, ImportReport report)
            : this(bundle, playerSprite, objectSprites, dialogueCatalog, encounterCatalog, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, DialogueCatalogDefinition dialogueCatalog, EncounterCatalogDefinition encounterCatalog, BattleContentCatalogDefinition battleContent, ImportReport report)
        {
            Bundle = bundle;
            PlayerSprite = playerSprite;
            ObjectSprites = objectSprites;
            DialogueCatalog = dialogueCatalog;
            EncounterCatalog = encounterCatalog;
            BattleContent = battleContent;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public MapBundleDefinition Bundle { get; }
        public OverworldSpriteDefinition PlayerSprite { get; }
        public ObjectSpriteCatalogDefinition ObjectSprites { get; }
        public ObjectSpriteCatalogDefinition ObjectSpriteCatalog => ObjectSprites;
        public DialogueCatalogDefinition DialogueCatalog { get; }
        public EncounterCatalogDefinition EncounterCatalog { get; }
        public BattleContentCatalogDefinition BattleContent { get; }
        public ImportReport Report { get; }
        public bool Succeeded => Bundle != null && PlayerSprite != null && ObjectSprites != null && DialogueCatalog != null && EncounterCatalog != null && BattleContent != null && !Report.HasErrors;
    }

    /// <summary>Bounds-safe parser for the deliberately small Pallet Town transition bundle.</summary>
    public sealed class FireRedMapBundleParser
    {
        private const string Stage = "PalletTownBundle";

        public FireRedMapBundleParseResult Parse(RomFile rom)
        {
            if (rom == null) throw new ArgumentNullException(nameof(rom));
            var report = new ImportReport(Stage);
            try
            {
                var header = GbaHeaderParser.Parse(rom.CreateReader());
                var detection = new PokemonFireRedAdapter().Detect(header, rom.Fingerprint);
                if (!detection.CanImport)
                {
                    report.Add(new ParseDiagnostic("Game", DiagnosticSeverity.Error, detection.Message));
                    return new FireRedMapBundleParseResult(null, null, report);
                }

                return ParseSupportedReader(rom.CreateReader(), report);
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

            return new FireRedMapBundleParseResult(null, null, report);
        }

        private static FireRedMapBundleParseResult ParseSupportedReader(RomReader reader, ImportReport report)
        {
            try
            {
                var tilesets = new Dictionary<string, TilesetDefinition>(StringComparer.Ordinal);
                var maps = new List<MapDefinition>(FireRedRomLayoutRev1.SelectedMapSpecs.Count + 1);
                for (var index = 0; index < FireRedRomLayoutRev1.SelectedMapSpecs.Count; index++)
                {
                    maps.Add(ParseMap(reader, FireRedRomLayoutRev1.SelectedMapSpecs[index], tilesets));
                }

                var route1 = ParseMap(reader, FireRedRomLayoutRev1.Route1MapSpec, tilesets);
                maps.Add(route1);

                var bundle = new MapBundleDefinition(maps, new[] { FireRedRomLayoutRev1.OakLabMapId });
                var playerSprite = PlayerRedNormalParser.Parse(reader);
                var objectSprites = ObjectEventSpriteDecoder.Decode(reader);
                var dialogues = FireRedDialogueDecoder.Decode(reader, report);
                var encounters = FireRedRoute1EncounterParser.Parse(reader, route1);
                var battleContent = FireRedBattleContentParser.Parse(reader);
                report.Add(new ParseDiagnostic("MapBundle", DiagnosticSeverity.Info, "Parsed Pallet Town, three interior maps, and Route 1 (1,808 cells, 11 warp records).", FireRedRomLayoutRev1.PalletTownMapHeader, FireRedRomLayoutRev1.MapHeaderSize));
                report.Add(new ParseDiagnostic("ObjectEvent", DiagnosticSeverity.Warning, "Route 1 object-event records were bounds-validated but intentionally omitted because no MVP 4 object whitelist is declared for them.", FireRedRomLayoutRev1.Route1Events, FireRedRomLayoutRev1.MapEventsSize));
                report.Add(new ParseDiagnostic("Encounter", DiagnosticSeverity.Info, "Parsed the audited Route 1 land encounter zone (178 cells, 12 weighted slots).", FireRedRomLayoutRev1.Route1WildHeader, FireRedRomLayoutRev1.WildPokemonHeaderSize));
                report.Add(new ParseDiagnostic("Warp", DiagnosticSeverity.Warning, "Oak's Lab is intentionally external to this bundle; its Pallet Town warp remains unresolved.", FireRedRomLayoutRev1.PalletTownEvents, FireRedRomLayoutRev1.MapEventsSize));
                report.Add(new ParseDiagnostic("PlayerSprite", DiagnosticSeverity.Info, "Parsed the normal on-foot player sprite (9 frames, 8 animations).", FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfo, FireRedRomLayoutRev1.ObjectEventGraphicsInfoSize));
                report.Add(new ParseDiagnostic("BattleContent", DiagnosticSeverity.Info, "Parsed the audited battle-content whitelist (Bulbasaur, Pidgey, Rattata, and Tackle).", FireRedRomLayoutRev1.PokemonSpeciesInfoTable, FireRedRomLayoutRev1.PokemonSpeciesInfoRecordSize));
                return new FireRedMapBundleParseResult(bundle, playerSprite, objectSprites, dialogues, encounters, battleContent, report);
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

            return new FireRedMapBundleParseResult(null, null, report);
        }

        private static MapDefinition ParseMap(RomReader reader, FireRedMapSpec spec, IDictionary<string, TilesetDefinition> tilesets)
        {
            ValidateMapRoutes(reader, spec);
            var primary = GetTileset(reader, spec.PrimaryTileset, tilesets);
            var secondary = GetTileset(reader, spec.SecondaryTileset, tilesets);
            var cells = ParseCells(reader, spec, primary, secondary);
            var warps = ParseWarps(reader, spec, cells, primary, secondary);
            var npcs = new List<NpcDefinition>();
            var props = new List<StaticMapPropDefinition>();
            if (spec.ImportObjectEvents) FireRedObjectEventParser.Parse(reader, spec, cells, out npcs, out props);
            return new MapDefinition(spec.Id, spec.Name, spec.Width, spec.Height, cells, primary, secondary, warps, npcs, props);
        }

        private static void ValidateMapRoutes(RomReader reader, FireRedMapSpec spec)
        {
            reader.EnsureRange(spec.HeaderOffset, FireRedRomLayoutRev1.MapHeaderSize, spec.Name + " MapHeader is outside ROM bounds.");
            ExpectPointer(reader, checked(FireRedRomLayoutRev1.MapLayoutsTable + ((spec.LayoutId - 1) * 4)), spec.LayoutOffset, spec.Name + " layout table entry");
            ExpectPointer(reader, checked(FireRedRomLayoutRev1.MapGroupsTable + (spec.MapGroup * 4)), spec.MapGroupPointerOffset, spec.Name + " map group");
            ExpectPointer(reader, checked(spec.MapGroupPointerOffset + (spec.MapNumber * 4)), spec.HeaderOffset, spec.Name + " map header");
            ExpectPointer(reader, spec.HeaderOffset, spec.LayoutOffset, spec.Name + " header layout");
            ExpectPointer(reader, checked(spec.HeaderOffset + 4), spec.EventsOffset, spec.Name + " header events");
            ExpectEqual(reader, reader.ReadUInt16(checked(spec.HeaderOffset + 0x12)), (uint)spec.LayoutId, spec.Name + " layout id", checked(spec.HeaderOffset + 0x12));

            reader.EnsureRange(spec.LayoutOffset, FireRedRomLayoutRev1.MapLayoutSize, spec.Name + " MapLayout is outside ROM bounds.");
            ExpectEqual(reader, reader.ReadUInt32(spec.LayoutOffset), (uint)spec.Width, spec.Name + " layout width", spec.LayoutOffset);
            ExpectEqual(reader, reader.ReadUInt32(checked(spec.LayoutOffset + 4)), (uint)spec.Height, spec.Name + " layout height", checked(spec.LayoutOffset + 4));
            ExpectPointer(reader, checked(spec.LayoutOffset + 8), spec.BorderCellsOffset, spec.Name + " border data");
            ExpectPointer(reader, checked(spec.LayoutOffset + 0x0C), spec.MapCellsOffset, spec.Name + " map cells");
            ExpectPointer(reader, checked(spec.LayoutOffset + 0x10), spec.PrimaryTileset.HeaderOffset, spec.Name + " primary tileset");
            ExpectPointer(reader, checked(spec.LayoutOffset + 0x14), spec.SecondaryTileset.HeaderOffset, spec.Name + " secondary tileset");
        }

        private static TilesetDefinition GetTileset(RomReader reader, FireRedTilesetSpec spec, IDictionary<string, TilesetDefinition> cache)
        {
            if (cache.TryGetValue(spec.Id, out var existing)) return existing;
            reader.EnsureRange(spec.HeaderOffset, FireRedRomLayoutRev1.TilesetSize, spec.Id + " tileset header is outside ROM bounds.");
            if (reader.ReadByte(spec.HeaderOffset) != 1 || reader.ReadByte(checked(spec.HeaderOffset + 1)) != (spec.IsSecondary ? 1 : 0))
            {
                throw new RomReadException(spec.Id + " tileset flags do not match the verified rev1 layout.", spec.HeaderOffset, 2, reader.Length);
            }

            ExpectPointer(reader, checked(spec.HeaderOffset + 4), spec.TilesOffset, spec.Id + " compressed tiles");
            ExpectPointer(reader, checked(spec.HeaderOffset + 8), spec.PalettesOffset, spec.Id + " palettes");
            ExpectPointer(reader, checked(spec.HeaderOffset + 0x0C), spec.MetatilesOffset, spec.Id + " metatiles");
            ExpectPointer(reader, checked(spec.HeaderOffset + 0x14), spec.AttributesOffset, spec.Id + " metatile attributes");
            var callback = reader.ReadUInt32(checked(spec.HeaderOffset + 0x10));
            if ((callback & FireRedRomLayoutRev1.ThumbPointerAddressMask)
                != (spec.AnimationCallback & FireRedRomLayoutRev1.ThumbPointerAddressMask))
            {
                throw new RomReadException(spec.Id + " animation callback does not match the verified rev1 layout.", checked(spec.HeaderOffset + 0x10), 4, reader.Length);
            }

            var expectedTileBytes = checked(spec.TileCount * FireRedGraphicsDecoder.BytesPer4BppTile);
            var packedTiles = GbaLz10Decoder.Decode(reader, spec.TilesOffset, expectedTileBytes);
            if (packedTiles.Length != expectedTileBytes)
            {
                throw new RomReadException(spec.Id + " LZ10 output length does not match its verified tile count.", spec.TilesOffset, packedTiles.Length, reader.Length);
            }
            var tiles = FireRedGraphicsDecoder.Decode4BppTiles(packedTiles, spec.TileStart);
            var palettes = DecodePalettes(reader, spec.PalettesOffset, spec.PaletteCount, spec.IsSecondary ? FireRedRomLayoutRev1.PrimaryPaletteCount : 0);
            var metatiles = DecodeMetatiles(reader, spec);
            var animations = spec.Id == "General" ? DecodeGeneralAnimations(reader) : new List<TileAnimationDefinition>();
            var parsed = new TilesetDefinition(spec.Id, spec.IsSecondary, tiles, palettes, metatiles, animations);
            cache.Add(spec.Id, parsed);
            return parsed;
        }

        private static List<MapCellDefinition> ParseCells(RomReader reader, FireRedMapSpec spec, TilesetDefinition primary, TilesetDefinition secondary)
        {
            var count = checked(spec.Width * spec.Height);
            reader.EnsureRange(spec.MapCellsOffset, checked(count * 2), spec.Name + " map cells are outside ROM bounds.");
            var cells = new List<MapCellDefinition>(count);
            for (var i = 0; i < count; i++)
            {
                var offset = checked(spec.MapCellsOffset + (i * 2));
                var raw = reader.ReadUInt16(offset);
                var metatileId = raw & 0x03FF;
                var metatile = metatileId < FireRedRomLayoutRev1.SecondaryMetatileStart
                    ? GetMetatile(primary, metatileId, offset, reader, spec.Name)
                    : GetMetatile(secondary, metatileId - FireRedRomLayoutRev1.SecondaryMetatileStart, offset, reader, spec.Name);
                if (!metatile.LayerRoute.IsRenderable) throw new RomReadException(spec.Name + " references an invalid metatile layer type.", offset, 2, reader.Length);
                cells.Add(FireRedMapEncoding.DecodeMapCell(raw));
            }

            return cells;
        }

        private static List<WarpDefinition> ParseWarps(RomReader reader, FireRedMapSpec spec, IList<MapCellDefinition> cells, TilesetDefinition primary, TilesetDefinition secondary)
        {
            reader.EnsureRange(spec.EventsOffset, FireRedRomLayoutRev1.MapEventsSize, spec.Name + " MapEvents is outside ROM bounds.");
            ExpectCount(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsObjectCountOffset), spec.ObjectEventCount, spec.Name + " object events");
            ExpectCount(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsWarpCountOffset), spec.WarpCount, spec.Name + " warp events");
            ExpectCount(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsCoordCountOffset), spec.CoordEventCount, spec.Name + " coordinate events");
            ExpectCount(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsBackgroundCountOffset), spec.BackgroundEventCount, spec.Name + " background events");
            ValidateEventArray(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsObjectPointerOffset), spec.ObjectEventCount, FireRedRomLayoutRev1.ObjectEventSize, null, spec.Name + " object events");
            var warpOffset = ValidateEventArray(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsWarpPointerOffset), spec.WarpCount, FireRedRomLayoutRev1.WarpEventSize, spec.WarpArrayOffset, spec.Name + " warp events");
            ValidateEventArray(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsCoordPointerOffset), spec.CoordEventCount, FireRedRomLayoutRev1.CoordEventSize, null, spec.Name + " coordinate events");
            ValidateEventArray(reader, checked(spec.EventsOffset + FireRedRomLayoutRev1.MapEventsBackgroundPointerOffset), spec.BackgroundEventCount, FireRedRomLayoutRev1.BackgroundEventSize, null, spec.Name + " background events");

            var warps = new List<WarpDefinition>(spec.WarpCount);
            for (var index = 0; index < spec.WarpCount; index++)
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
                var destinationId = ResolveDestinationMapId(destinationMapGroup, destinationMapNumber, offset, reader);
                var activation = ResolveActivation(cells[checked((y * spec.Width) + x)], primary, secondary);
                warps.Add(new WarpDefinition(
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

            return warps;
        }

        private static int ValidateEventArray(RomReader reader, int pointerField, int count, int stride, int? expectedOffset, string description)
        {
            var byteCount = checked(count * stride);
            var raw = reader.ReadUInt32(pointerField);
            if (count == 0)
            {
                if (raw == 0) return 0;
                var offset = reader.ConvertGbaPointer(raw, 1);
                if (expectedOffset.HasValue && offset != expectedOffset.Value) throw new RomReadException(description + " pointer does not match the verified rev1 location.", pointerField, 4, reader.Length);
                return offset;
            }

            var resolved = reader.ConvertGbaPointer(raw, byteCount);
            if (expectedOffset.HasValue && resolved != expectedOffset.Value) throw new RomReadException(description + " pointer does not match the verified rev1 location.", pointerField, 4, reader.Length);
            return resolved;
        }

        private static void ExpectCount(RomReader reader, int offset, int expected, string description)
        {
            if (reader.ReadByte(offset) != expected) throw new RomReadException(description + " count does not match the verified rev1 layout.", offset, 1, reader.Length);
        }

        private static string ResolveDestinationMapId(int group, int number, int offset, RomReader reader)
        {
            for (var i = 0; i < FireRedRomLayoutRev1.SelectedMapSpecs.Count; i++)
            {
                var map = FireRedRomLayoutRev1.SelectedMapSpecs[i];
                if (map.MapGroup == group && map.MapNumber == number) return map.Id;
            }

            if (group == FireRedRomLayoutRev1.PlayersHouse1FMapGroup && number == FireRedRomLayoutRev1.OakLabMapNumber) return FireRedRomLayoutRev1.OakLabMapId;
            throw new RomReadException("Warp destination is outside the verified MVP 3 map set.", offset, FireRedRomLayoutRev1.WarpEventSize, reader.Length);
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
            if (index < 0 || index >= tileset.Metatiles.Count) throw new RomReadException(mapName + " references an unavailable metatile.", offset, 2, reader.Length);
            return tileset.Metatiles[index];
        }

        private static List<PaletteDefinition> DecodePalettes(RomReader reader, int offset, int count, int start)
        {
            reader.EnsureRange(offset, checked(count * 32), "Tileset palette data is outside ROM bounds.");
            var palettes = new List<PaletteDefinition>(count);
            for (var palette = 0; palette < count; palette++)
            {
                var colors = new List<Rgba32>(16);
                for (var color = 0; color < 16; color++) colors.Add(FireRedGraphicsDecoder.DecodeBgr555(reader.ReadUInt16(checked(offset + (palette * 32) + (color * 2))), color == 0 ? (byte)0 : (byte)255));
                palettes.Add(new PaletteDefinition(start + palette, colors));
            }

            return palettes;
        }

        private static List<MetatileDefinition> DecodeMetatiles(RomReader reader, FireRedTilesetSpec spec)
        {
            reader.EnsureRange(spec.MetatilesOffset, checked(spec.MetatileCount * 16), spec.Id + " metatile data is outside ROM bounds.");
            reader.EnsureRange(spec.AttributesOffset, checked(spec.MetatileCount * 4), spec.Id + " metatile attributes are outside ROM bounds.");
            var definitions = new List<MetatileDefinition>(spec.MetatileCount);
            for (var metatile = 0; metatile < spec.MetatileCount; metatile++)
            {
                var subtiles = new List<SubtileDefinition>(FireRedRomLayoutRev1.SubtilesPerMetatile);
                for (var subtile = 0; subtile < FireRedRomLayoutRev1.SubtilesPerMetatile; subtile++)
                {
                    var offset = checked(spec.MetatilesOffset + (metatile * 16) + (subtile * 2));
                    var value = reader.ReadUInt16(offset);
                    if ((value & 0x03FF) >= spec.TileStart + spec.TileCount) throw new RomReadException(spec.Id + " metatile references a tile outside its verified range.", offset, 2, reader.Length);
                    subtiles.Add(FireRedMapEncoding.DecodeSubtile(value));
                }

                var attributes = reader.ReadUInt32(checked(spec.AttributesOffset + (metatile * 4)));
                definitions.Add(new MetatileDefinition(spec.MetatileStart + metatile, subtiles, attributes, FireRedMapEncoding.DecodeLayerRoute(attributes)));
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

        private static void ExpectPointer(RomReader reader, int field, int expected, string description)
        {
            var actual = reader.ConvertGbaPointer(reader.ReadUInt32(field));
            if (actual != expected) throw new RomReadException(description + " does not match the verified rev1 location.", field, 4, reader.Length);
        }

        private static void ExpectEqual(RomReader reader, uint actual, uint expected, string description, int offset)
        {
            if (actual != expected) throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 4, reader.Length);
        }

        private static short ReadInt16(RomReader reader, int offset) => unchecked((short)reader.ReadUInt16(offset));
        private static int ToDiagnosticLength(long length) => length > int.MaxValue ? int.MaxValue : (int)Math.Max(0, length);
    }
}
