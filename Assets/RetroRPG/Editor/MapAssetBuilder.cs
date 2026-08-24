using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RetroRPG.IR;
using RetroRPG.Core;

namespace RetroRPG.Editor
{
    /// <summary>
    /// Complete, already-validated input to the Unity map asset pipeline.  This is
    /// intentionally source-game agnostic: ROM adapters create this snapshot, while
    /// this editor layer only turns its IR into Unity assets.
    /// </summary>
    public sealed class MapAssetImportSnapshot
    {
        public MapAssetImportSnapshot(
            MapBundleDefinition bundle,
            OverworldSpriteDefinition playerSprite,
            ObjectSpriteCatalogDefinition objectSprites,
            DialogueCatalogDefinition dialogueCatalog,
            EncounterCatalogDefinition encounterCatalog,
            BattleContentCatalogDefinition battleContent,
            ImportReport report,
            MapCatalogDefinition catalog = null)
        {
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            PlayerSprite = playerSprite ?? throw new ArgumentNullException(nameof(playerSprite));
            ObjectSprites = objectSprites;
            DialogueCatalog = dialogueCatalog;
            EncounterCatalog = encounterCatalog;
            BattleContent = battleContent;
            Report = report ?? throw new ArgumentNullException(nameof(report));
            Catalog = catalog ?? CreateCatalogFromBundle(bundle);
            ValidateCatalogMatchesBundle(Catalog, bundle);
        }

        public MapBundleDefinition Bundle { get; }
        public OverworldSpriteDefinition PlayerSprite { get; }
        public ObjectSpriteCatalogDefinition ObjectSprites { get; }
        public DialogueCatalogDefinition DialogueCatalog { get; }
        public EncounterCatalogDefinition EncounterCatalog { get; }
        public BattleContentCatalogDefinition BattleContent { get; }
        public ImportReport Report { get; }
        public MapCatalogDefinition Catalog { get; }

        /// <summary>
        /// Provides a deterministic presentation catalog for adapters which have not
        /// yet published one explicitly. It does not infer game-specific dependencies.
        /// </summary>
        public static MapCatalogDefinition CreateCatalogFromBundle(MapBundleDefinition bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            var descriptors = new List<MapImportDescriptorDefinition>(bundle.Maps.Count);
            for (var index = 0; index < bundle.Maps.Count; index++)
            {
                var map = bundle.Maps[index];
                descriptors.Add(new MapImportDescriptorDefinition(
                    map.Id,
                    map.Name,
                    map.Width,
                    map.Height,
                    false,
                    new List<string>(),
                    new List<string>()));
            }

            return new MapCatalogDefinition(descriptors);
        }

        private static void ValidateCatalogMatchesBundle(MapCatalogDefinition catalog, MapBundleDefinition bundle)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            for (var index = 0; index < bundle.Maps.Count; index++)
            {
                if (!catalog.TryGetMap(bundle.Maps[index].Id, out _))
                {
                    throw new ArgumentException("Import snapshot contains a map absent from its catalog: " + bundle.Maps[index].Id, nameof(catalog));
                }
            }
        }
    }

    /// <summary>Selection submitted by Map Browser, kept separate from immutable parsed IR.</summary>
    public sealed class MapAssetImportRequest
    {
        public MapAssetImportRequest(MapAssetImportSnapshot snapshot, IList<string> selectedMapIds)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (selectedMapIds == null || selectedMapIds.Count == 0)
            {
                throw new ArgumentException("At least one map must be selected.", nameof(selectedMapIds));
            }

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            var copiedIds = new List<string>(selectedMapIds.Count);
            for (var index = 0; index < selectedMapIds.Count; index++)
            {
                var id = selectedMapIds[index];
                if (string.IsNullOrWhiteSpace(id) || !uniqueIds.Add(id))
                {
                    throw new ArgumentException("Selected map ids must be non-empty and unique.", nameof(selectedMapIds));
                }

                copiedIds.Add(id);
            }

            copiedIds.Sort(StringComparer.Ordinal);
            SelectedMapIds = new ReadOnlyCollection<string>(copiedIds);
        }

        public MapAssetImportSnapshot Snapshot { get; }
        public IReadOnlyList<string> SelectedMapIds { get; }
    }

    /// <summary>Stable result metadata for editor windows; no ROM paths are retained.</summary>
    public sealed class MapAssetImportResult
    {
        internal MapAssetImportResult(IReadOnlyList<string> requestedMapIds, IReadOnlyList<string> resolvedMapIds)
        {
            RequestedMapIds = requestedMapIds;
            ResolvedMapIds = resolvedMapIds;
        }

        public IReadOnlyList<string> RequestedMapIds { get; }
        public IReadOnlyList<string> ResolvedMapIds { get; }
        public string OutputRoot => MapAssetBuilder.OutputRoot;
        public string SceneAssetPath => MapAssetBuilder.SceneAssetPath;
    }

    /// <summary>
    /// Generic editor-facing facade over the deterministic map asset pipeline.
    ///
    /// The current vertical slice owns one coherent generated scene and shared
    /// object/dialogue/encounter catalogs. Each request therefore commits the complete
    /// already-parsed selection closure as one transaction. This preserves manifest
    /// ownership, cache reuse, and GUID stability across reimports.
    /// </summary>
    public static class MapAssetBuilder
    {
        public const string OutputRoot = PalletTownAssetBuilder.OutputRoot;
        public const string SceneAssetPath = OutputRoot + "/PalletTown.unity";

        public static MapAssetImportResult Import(MapAssetImportRequest request, Func<string, float, bool> shouldCancel)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Snapshot.Report.HasErrors)
            {
                throw new InvalidOperationException("An import report with errors cannot generate map assets.");
            }

            var resolved = request.Snapshot.Catalog.ResolveDependencyClosure(ToMutableList(request.SelectedMapIds));
            var resolvedIds = new List<string>(resolved.Count);
            for (var index = 0; index < resolved.Count; index++)
            {
                var id = resolved[index].Id;
                if (!request.Snapshot.Bundle.TryGetMap(id, out _))
                {
                    throw new InvalidOperationException("The parsed snapshot does not include selected map dependency: " + id);
                }
                resolvedIds.Add(id);
            }

            // PalletTownAssetBuilder is the existing transactional writer. Keeping this
            // delegation centralized ensures its manifest can remove only prior owned
            // assets and preserves GUIDs on stable reimport paths.
            var selectedDialogues = FilterDialoguesForBundle(request.Snapshot.Bundle, request.Snapshot.DialogueCatalog);
            PalletTownAssetBuilder.Import(
                request.Snapshot.Bundle,
                request.Snapshot.PlayerSprite,
                request.Snapshot.ObjectSprites,
                selectedDialogues,
                request.Snapshot.EncounterCatalog,
                request.Snapshot.BattleContent,
                request.Snapshot.Report,
                shouldCancel);

            return new MapAssetImportResult(request.SelectedMapIds, new ReadOnlyCollection<string>(resolvedIds));
        }

        public static string GetOutputFolderAbsolutePath() => PalletTownAssetBuilder.GetOutputFolderAbsolutePath();

        private static List<string> ToMutableList(IReadOnlyList<string> ids)
        {
            var copied = new List<string>(ids.Count);
            for (var index = 0; index < ids.Count; index++) copied.Add(ids[index]);
            return copied;
        }

        private static DialogueCatalogDefinition FilterDialoguesForBundle(MapBundleDefinition bundle, DialogueCatalogDefinition catalog)
        {
            if (catalog == null) return null;
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (var mapIndex = 0; mapIndex < bundle.Maps.Count; mapIndex++)
            {
                var map = bundle.Maps[mapIndex];
                for (var npcIndex = 0; npcIndex < map.Npcs.Count; npcIndex++) eventIds.Add(map.Npcs[npcIndex].EventId);
                for (var propIndex = 0; propIndex < map.Props.Count; propIndex++) eventIds.Add(map.Props[propIndex].EventId);
            }

            var selected = new List<DialogueDefinition>();
            for (var dialogueIndex = 0; dialogueIndex < catalog.Dialogues.Count; dialogueIndex++)
            {
                var dialogue = catalog.Dialogues[dialogueIndex];
                if (eventIds.Contains(dialogue.TargetEventId)) selected.Add(dialogue);
            }

            return new DialogueCatalogDefinition(selected);
        }
    }
}
