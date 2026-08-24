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
            : this(bundle, playerSprite, objectSprites, dialogueCatalog, encounterCatalog, battleContent, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, DialogueCatalogDefinition dialogueCatalog, EncounterCatalogDefinition encounterCatalog, BattleContentCatalogDefinition battleContent, MapCatalogDefinition mapCatalog, ImportReport report)
            : this(bundle, playerSprite, objectSprites, dialogueCatalog, encounterCatalog, battleContent, mapCatalog, null, null, report)
        {
        }

        public FireRedMapBundleParseResult(MapBundleDefinition bundle, OverworldSpriteDefinition playerSprite, ObjectSpriteCatalogDefinition objectSprites, DialogueCatalogDefinition dialogueCatalog, EncounterCatalogDefinition encounterCatalog, BattleContentCatalogDefinition battleContent, MapCatalogDefinition mapCatalog, IList<string> requestedMapIds, IList<string> resolvedMapIds, ImportReport report)
        {
            Bundle = bundle;
            PlayerSprite = playerSprite;
            ObjectSprites = objectSprites;
            DialogueCatalog = dialogueCatalog;
            EncounterCatalog = encounterCatalog;
            BattleContent = battleContent;
            MapCatalog = mapCatalog;
            RequestedMapIds = CopyMapIds(requestedMapIds);
            ResolvedMapIds = CopyMapIds(resolvedMapIds);
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public MapBundleDefinition Bundle { get; }
        public OverworldSpriteDefinition PlayerSprite { get; }
        public ObjectSpriteCatalogDefinition ObjectSprites { get; }
        public ObjectSpriteCatalogDefinition ObjectSpriteCatalog => ObjectSprites;
        public DialogueCatalogDefinition DialogueCatalog { get; }
        public EncounterCatalogDefinition EncounterCatalog { get; }
        public BattleContentCatalogDefinition BattleContent { get; }
        public MapCatalogDefinition MapCatalog { get; }
        public IReadOnlyList<string> RequestedMapIds { get; }
        public IReadOnlyList<string> ResolvedMapIds { get; }
        public ImportReport Report { get; }
        public bool Succeeded => Bundle != null && PlayerSprite != null && ObjectSprites != null && DialogueCatalog != null && EncounterCatalog != null && BattleContent != null && !Report.HasErrors;

        private static IReadOnlyList<string> CopyMapIds(IList<string> ids)
        {
            var copied = new List<string>();
            if (ids != null)
            {
                var unique = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < ids.Count; index++)
                {
                    var id = ids[index];
                    if (string.IsNullOrWhiteSpace(id) || !unique.Add(id)) throw new ArgumentException("Map result ids must be non-blank and unique.", nameof(ids));
                    copied.Add(id);
                }
            }

            copied.Sort(StringComparer.Ordinal);
            return new System.Collections.ObjectModel.ReadOnlyCollection<string>(copied);
        }
    }

    /// <summary>Bounds-safe parser for the deliberately small Pallet Town transition bundle.</summary>
    public sealed class FireRedMapBundleParser
    {
        private const string Stage = "PalletTownBundle";

        /// <summary>Descriptors for every currently audited map. No ROM is read to enumerate them.</summary>
        public MapCatalogDefinition MapCatalog => FireRedAuditedMapCatalog.Definition;

        public FireRedMapBundleParseResult Parse(RomFile rom)
        {
            return ParseInternal(rom, null);
        }

        /// <summary>Parses only requested audited maps plus their declared internal dependency closure.</summary>
        public FireRedMapBundleParseResult Parse(RomFile rom, IList<string> selectedMapIds)
        {
            if (selectedMapIds == null) throw new ArgumentNullException(nameof(selectedMapIds));
            return ParseInternal(rom, selectedMapIds);
        }

        private static FireRedMapBundleParseResult ParseInternal(RomFile rom, IList<string> selectedMapIds)
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

                return ParseSupportedReader(rom.CreateReader(), report, selectedMapIds);
            }
            catch (RomReadException exception)
            {
                report.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, exception.Message, exception.Offset, ToDiagnosticLength(exception.RequestedLength)));
            }
            catch (InvalidOperationException exception)
            {
                report.Add(new ParseDiagnostic("Format", DiagnosticSeverity.Error, exception.Message));
            }
            catch (ArgumentException exception)
            {
                report.Add(new ParseDiagnostic("MapSelection", DiagnosticSeverity.Error, exception.Message));
            }
            catch (OverflowException exception)
            {
                report.Add(new ParseDiagnostic("Safety", DiagnosticSeverity.Error, exception.Message));
            }

            return new FireRedMapBundleParseResult(null, null, report);
        }

        private static FireRedMapBundleParseResult ParseSupportedReader(RomReader reader, ImportReport report, IList<string> selectedMapIds)
        {
            try
            {
                // Keep the no-selection overload backward compatible with the original
                // audited vertical slice. The Map Browser supplies an explicit selection
                // and therefore uses ROM-backed discovery.
                FireRedMapCatalogScanResult discovery = null;
                MapCatalogDefinition mapCatalog;
                List<MapImportDescriptorDefinition> descriptors;
                List<string> requestedIds;

                if (selectedMapIds == null)
                {
                    mapCatalog = FireRedAuditedMapCatalog.Definition;
                    descriptors = new List<MapImportDescriptorDefinition>(mapCatalog.Maps);
                    requestedIds = MapIds(mapCatalog.Maps);
                }
                else
                {
                    discovery = FireRedMapCatalogScanner.ScanDetailed(reader);
                    mapCatalog = discovery.Catalog;
                    descriptors = new List<MapImportDescriptorDefinition>(mapCatalog.ResolveDependencyClosure(selectedMapIds));
                    requestedIds = new List<string>(selectedMapIds);
                }

                var tilesets = new Dictionary<string, TilesetDefinition>(StringComparer.Ordinal);
                var maps = new List<MapDefinition>(descriptors.Count);
                MapDefinition route1 = null;
                var genericMapCount = 0;
                var skippedGenericCount = 0;

                for (var index = 0; index < descriptors.Count; index++)
                {
                    var descriptor = descriptors[index];
                    FireRedMapSpec auditedSpec;
                    MapDefinition map;
                    if (TryGetAuditedSpec(descriptor.Id, out auditedSpec))
                    {
                        map = ParseMap(reader, auditedSpec, tilesets);
                    }
                    else
                    {
                        if (discovery == null || !discovery.TryGetSpec(descriptor.Id, out var discoveredSpec))
                        {
                            report.Add(new ParseDiagnostic("MapSupport", DiagnosticSeverity.Warning, "Skipped map without a discovered FireRed specification: " + descriptor.Id + "."));
                            skippedGenericCount++;
                            continue;
                        }

                        try
                        {
                            int placeholderTileCount;
                            map = FireRedDiscoveredMapParser.Parse(reader, discoveredSpec, discovery, out placeholderTileCount);
                            genericMapCount++;
                            if (placeholderTileCount > 0)
                            {
                                report.Add(new ParseDiagnostic(
                                    "MapSupport",
                                    DiagnosticSeverity.Warning,
                                    descriptor.Name + " uses "
                                    + placeholderTileCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                    + " tile slot(s) not present in the base compressed tileset. Transparent placeholders were generated; these slots are likely supplied by tileset animation/callback data that the generic importer does not execute yet."));
                            }
                        }
                        catch (RomReadException exception)
                        {
                            report.Add(new ParseDiagnostic(
                                "MapSupport",
                                DiagnosticSeverity.Warning,
                                "Skipped " + descriptor.Name + " because the generic importer does not support one of its structures yet: " + exception.Message,
                                exception.Offset,
                                ToDiagnosticLength(exception.RequestedLength)));
                            skippedGenericCount++;
                            continue;
                        }
                        catch (InvalidOperationException exception)
                        {
                            report.Add(new ParseDiagnostic(
                                "MapSupport",
                                DiagnosticSeverity.Warning,
                                "Skipped " + descriptor.Name + " because the generic importer does not support one of its structures yet: " + exception.Message));
                            skippedGenericCount++;
                            continue;
                        }
                    }

                    if (discovery != null && discovery.TryGetSpec(map.Id, out var connectionSpec))
                    {
                        map = AttachConnections(
                            map,
                            FireRedMapConnectionParser.Parse(reader, connectionSpec, discovery));
                    }

                    maps.Add(map);
                    if (map.Id == FireRedRomLayoutRev1.Route1MapId) route1 = map;
                }

                if (maps.Count == 0)
                {
                    throw new InvalidOperationException("None of the selected maps could be parsed by the current audited or generic import paths.");
                }

                var resolvedIds = MapIds(maps);
                var externalDependencies = CollectExternalDestinations(maps);
                var bundle = new MapBundleDefinition(maps, externalDependencies);
                var playerSprite = PlayerRedNormalParser.Parse(reader);
                var objectSprites = ObjectEventSpriteDecoder.Decode(reader);
                var dialogues = FireRedDialogueDecoder.Decode(reader, report, resolvedIds);
                var encounters = route1 == null
                    ? new EncounterCatalogDefinition(new EncounterZoneDefinition[0], new EncounterTableDefinition[0])
                    : FireRedRoute1EncounterParser.Parse(reader, route1);
                var battleContent = FireRedBattleContentParser.Parse(reader);

                if (discovery != null)
                {
                    report.Add(new ParseDiagnostic(
                        "Catalog",
                        DiagnosticSeverity.Info,
                        "Discovered " + discovery.Maps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " valid map headers across " + discovery.GroupCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " map groups."));
                }

                report.Add(new ParseDiagnostic(
                    "MapSelection",
                    DiagnosticSeverity.Info,
                    "Resolved " + requestedIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " requested map ids to " + resolvedIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " selected maps."));
                report.Add(new ParseDiagnostic(
                    "MapBundle",
                    DiagnosticSeverity.Info,
                    "Parsed " + maps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " selected maps (" + CountCells(maps).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " cells, " + CountWarps(maps).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " warp records, " + CountConnections(maps).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " cardinal map connections)."));

                if (genericMapCount > 0)
                {
                    report.Add(new ParseDiagnostic(
                        "MapSupport",
                        DiagnosticSeverity.Warning,
                        genericMapCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " ROM-discovered map(s) were imported through the generic map/warp path. "
                        + "Unknown object events and scripts are intentionally omitted until their semantics are supported."));
                }
                if (skippedGenericCount > 0)
                {
                    report.Add(new ParseDiagnostic(
                        "MapSupport",
                        DiagnosticSeverity.Warning,
                        skippedGenericCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " selected discovered map(s) were skipped without aborting the rest of the import."));
                }

                if (route1 != null)
                {
                    report.Add(new ParseDiagnostic("ObjectEvent", DiagnosticSeverity.Warning, "Route 1 object-event records were bounds-validated but intentionally omitted because no MVP 4 object whitelist is declared for them.", FireRedRomLayoutRev1.Route1Events, FireRedRomLayoutRev1.MapEventsSize));
                    report.Add(new ParseDiagnostic("Encounter", DiagnosticSeverity.Info, "Parsed the audited Route 1 land encounter zone (178 cells, 12 weighted slots).", FireRedRomLayoutRev1.Route1WildHeader, FireRedRomLayoutRev1.WildPokemonHeaderSize));
                }

                report.Add(new ParseDiagnostic("PlayerSprite", DiagnosticSeverity.Info, "Parsed the normal on-foot player sprite (9 frames, 8 animations).", FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfo, FireRedRomLayoutRev1.ObjectEventGraphicsInfoSize));
                report.Add(new ParseDiagnostic("BattleContent", DiagnosticSeverity.Info, "Parsed the audited battle-content whitelist (Bulbasaur, Pidgey, Rattata, and Tackle).", FireRedRomLayoutRev1.PokemonSpeciesInfoTable, FireRedRomLayoutRev1.PokemonSpeciesInfoRecordSize));
                return new FireRedMapBundleParseResult(bundle, playerSprite, objectSprites, dialogues, encounters, battleContent, mapCatalog, requestedIds, resolvedIds, report);
            }
            catch (RomReadException exception)
            {
                report.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, exception.Message, exception.Offset, ToDiagnosticLength(exception.RequestedLength)));
            }
            catch (InvalidOperationException exception)
            {
                report.Add(new ParseDiagnostic("Format", DiagnosticSeverity.Error, exception.Message));
            }
            catch (ArgumentException exception)
            {
                report.Add(new ParseDiagnostic("MapSelection", DiagnosticSeverity.Error, exception.Message));
            }
            catch (OverflowException exception)
            {
                report.Add(new ParseDiagnostic("Safety", DiagnosticSeverity.Error, exception.Message));
            }

            return new FireRedMapBundleParseResult(null, null, report);
        }

        private static bool TryGetAuditedSpec(string mapId, out FireRedMapSpec spec)
        {
            for (var index = 0; index < FireRedRomLayoutRev1.AuditedMapSpecs.Count; index++)
            {
                var candidate = FireRedRomLayoutRev1.AuditedMapSpecs[index];
                if (string.Equals(candidate.Id, mapId, StringComparison.Ordinal))
                {
                    spec = candidate;
                    return true;
                }
            }

            spec = null;
            return false;
        }

        private static List<string> CollectExternalDestinations(IList<MapDefinition> maps)
        {
            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < maps.Count; index++) selected.Add(maps[index].Id);

            var external = new HashSet<string>(StringComparer.Ordinal);
            for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var map = maps[mapIndex];
                for (var warpIndex = 0; warpIndex < map.Warps.Count; warpIndex++)
                {
                    var destination = map.Warps[warpIndex].DestinationMapId;
                    if (!selected.Contains(destination)) external.Add(destination);
                }

                for (var connectionIndex = 0; connectionIndex < map.Connections.Count; connectionIndex++)
                {
                    var destination = map.Connections[connectionIndex].DestinationMapId;
                    if (!selected.Contains(destination)) external.Add(destination);
                }
            }

            var ordered = new List<string>(external);
            ordered.Sort(StringComparer.Ordinal);
            return ordered;
        }

        private static MapDefinition AttachConnections(
            MapDefinition map,
            IList<MapConnectionDefinition> connections)
        {
            return new MapDefinition(
                map.Id,
                map.Name,
                map.Width,
                map.Height,
                new List<MapCellDefinition>(map.Cells),
                map.PrimaryTileset,
                map.SecondaryTileset,
                new List<WarpDefinition>(map.Warps),
                new List<NpcDefinition>(map.Npcs),
                new List<StaticMapPropDefinition>(map.Props),
                connections == null
                    ? new List<MapConnectionDefinition>()
                    : new List<MapConnectionDefinition>(connections));
        }

        private static int CountConnections(IList<MapDefinition> maps)
        {
            var count = 0;
            for (var index = 0; index < maps.Count; index++) count = checked(count + maps[index].Connections.Count);
            return count;
        }

        private static int CountCells(IList<MapDefinition> maps)
        {
            var count = 0;
            for (var index = 0; index < maps.Count; index++) count = checked(count + maps[index].Cells.Count);
            return count;
        }

        private static int CountWarps(IList<MapDefinition> maps)
        {
            var count = 0;
            for (var index = 0; index < maps.Count; index++) count = checked(count + maps[index].Warps.Count);
            return count;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (var index = 0; index < ids.Count; index++) if (string.Equals(ids[index], id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static List<string> MapIds(IReadOnlyList<MapImportDescriptorDefinition> descriptors)
        {
            var ids = new List<string>(descriptors.Count);
            for (var index = 0; index < descriptors.Count; index++) ids.Add(descriptors[index].Id);
            return ids;
        }

        private static List<string> MapIds(IList<MapDefinition> maps)
        {
            var ids = new List<string>(maps.Count);
            for (var index = 0; index < maps.Count; index++) ids.Add(maps[index].Id);
            ids.Sort(StringComparer.Ordinal);
            return ids;
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
            for (var i = 0; i < FireRedRomLayoutRev1.AuditedMapSpecs.Count; i++)
            {
                var map = FireRedRomLayoutRev1.AuditedMapSpecs[i];
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
                    // Tile ids are 10-bit indexes in the combined primary+secondary
                    // tileset address space. A metatile from one bank may legitimately
                    // reference a tile supplied by its companion bank, so validating
                    // against this individual spec's TileStart/TileCount is too strict.
                    // PalletTownAssetBuilder.ValidateMapContent performs the correct
                    // map-level validation after both tilesets are combined.
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
