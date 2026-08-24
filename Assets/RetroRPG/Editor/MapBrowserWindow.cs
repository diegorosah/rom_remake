using System;
using System.Collections.Generic;
using System.IO;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.IR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroRPG.Editor
{
    /// <summary>Editor-only browser for the map catalog emitted by an available ROM adapter.</summary>
    public sealed class MapBrowserWindow : EditorWindow
    {
        private const string LastRomPathKey = "RetroRPG.LastRomPath";
        private const string SelectedMapIdsKey = "RetroRPG.MapBrowser.SelectedMapIds";

        private readonly HashSet<string> selectedMapIds = new HashSet<string>(StringComparer.Ordinal);
        private string selectedPath;
        private MapCatalogDefinition catalog;
        private MapAssetImportSnapshot snapshot;
        private ImportReport report;
        private string error;
        private Vector2 scroll;
        private string filter = string.Empty;

        [MenuItem("Tools/Retro RPG/Map Browser")]
        public static void Open()
        {
            GetWindow<MapBrowserWindow>("Map Browser");
        }

        private void OnEnable()
        {
            selectedPath = EditorPrefs.GetString(LastRomPathKey, string.Empty);
            RestoreSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Retro RPG Map Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            DrawRomSelection();

            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (report != null) DrawDiagnostics(report);
            if (catalog != null) DrawCatalog();
            DrawOutputActions();
        }

        private void DrawRomSelection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(selectedPath) ? "No ROM selected" : selectedPath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Select .gba", GUILayout.Width(100))) SelectRom();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedPath)))
            {
                if (GUILayout.Button("Load Available Maps")) LoadCatalog(selectedPath);
            }
        }

        private void DrawCatalog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Available maps", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Maps are discovered from the supported ROM's map-group/header tables. "
                + "AUDITED maps keep the richer NPC/dialogue behavior already implemented; DISCOVERED maps currently import layout, collision and warps while unknown scripts/events are omitted safely.",
                MessageType.Info);

            filter = EditorGUILayout.TextField("Filter", filter ?? string.Empty);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Visible"))
                {
                    for (var index = 0; index < catalog.Maps.Count; index++)
                    {
                        if (MatchesFilter(catalog.Maps[index])) selectedMapIds.Add(catalog.Maps[index].Id);
                    }
                    PersistSelection();
                }
                if (GUILayout.Button("Select Audited"))
                {
                    selectedMapIds.Clear();
                    for (var index = 0; index < catalog.Maps.Count; index++)
                    {
                        if (FireRedMapCatalogScanner.IsAuditedMapId(catalog.Maps[index].Id)) selectedMapIds.Add(catalog.Maps[index].Id);
                    }
                    PersistSelection();
                }
                if (GUILayout.Button("Select None"))
                {
                    selectedMapIds.Clear();
                    PersistSelection();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180));
            for (var index = 0; index < catalog.Maps.Count; index++)
            {
                var map = catalog.Maps[index];
                if (!MatchesFilter(map)) continue;
                var wasSelected = selectedMapIds.Contains(map.Id);
                var support = FireRedMapCatalogScanner.IsAuditedMapId(map.Id) ? "[AUDITED] " : "[DISCOVERED] ";
                var isSelected = EditorGUILayout.ToggleLeft(
                    support + map.Name + " (" + map.Id + ", " + map.Width + " x " + map.Height + ")",
                    wasSelected);
                if (isSelected == wasSelected) continue;
                if (isSelected) selectedMapIds.Add(map.Id); else selectedMapIds.Remove(map.Id);
                PersistSelection();
            }
            EditorGUILayout.EndScrollView();

            var selected = GetSelectedIdsInCatalogOrder();
            using (new EditorGUI.DisabledScope(selected.Count == 0 || report == null || report.HasErrors))
            {
                if (GUILayout.Button("Import Selected Maps")) ImportSelected(selected);
            }
        }

        private void DrawOutputActions()
        {
            if (!Directory.Exists(MapAssetBuilder.GetOutputFolderAbsolutePath())) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(MapAssetBuilder.OutputRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Imported Folder")) EditorUtility.RevealInFinder(MapAssetBuilder.GetOutputFolderAbsolutePath());
                if (GUILayout.Button("Open Imported Scene")) OpenImportedScene();
            }
        }

        private void SelectRom()
        {
            var initialDirectory = string.IsNullOrEmpty(selectedPath) ? Application.dataPath : Path.GetDirectoryName(selectedPath);
            var path = EditorUtility.OpenFilePanel("Select a GBA ROM", initialDirectory, "gba");
            if (string.IsNullOrEmpty(path)) return;
            selectedPath = path;
            EditorPrefs.SetString(LastRomPathKey, selectedPath);
            LoadCatalog(selectedPath);
        }

        private void LoadCatalog(string path)
        {
            error = string.Empty;
            snapshot = null;
            catalog = null;
            report = new ImportReport("MAP_BROWSER");
            try
            {
                var rom = RomFile.Load(path);
                var header = GbaHeaderParser.Parse(rom.CreateReader());
                var detection = new GameDetector(new List<IRomGameAdapter> { new PokemonFireRedAdapter() })
                    .Detect(header, rom.Fingerprint);
                if (!detection.CanImport)
                {
                    report.Add(new ParseDiagnostic("Detection", DiagnosticSeverity.Error, detection.Message));
                    return;
                }

                // Enumerate the real rev1 map-group/header tables without decoding map
                // assets yet. Full selected maps are parsed only when the user commits.
                catalog = FireRedMapCatalogScanner.Scan(rom.CreateReader());
                report.Add(new ParseDiagnostic("Detection", DiagnosticSeverity.Info, detection.Message));
                report.Add(new ParseDiagnostic(
                    "Catalog",
                    DiagnosticSeverity.Info,
                    "Discovered " + catalog.Maps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " importable map headers from the ROM."));
                RemoveUnavailableSelections();
            }
            catch (RomReadException exception)
            {
                error = "The ROM could not be read within its available bounds.";
                report.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, error, exception.Offset, SafeLength(exception.RequestedLength)));
            }
            catch (Exception exception)
            {
                error = "The map catalog could not be loaded from this ROM.";
                report.Add(new ParseDiagnostic("Catalog", DiagnosticSeverity.Error, error + " " + exception.Message));
            }
        }

        private void ImportSelected(IList<string> selected)
        {
            try
            {
                // Reopen and parse immediately before commit so a changed on-disk ROM
                // cannot reuse a stale browser preview.
                var rom = RomFile.Load(selectedPath);
                var parser = new FireRedMapBundleParser();
                var parsed = parser.Parse(rom, selected);
                report = parsed.Report;
                if (!parsed.Succeeded) return;
                snapshot = new MapAssetImportSnapshot(
                    parsed.Bundle,
                    parsed.PlayerSprite,
                    parsed.ObjectSprites,
                    parsed.DialogueCatalog,
                    parsed.EncounterCatalog,
                    parsed.BattleContent,
                    parsed.Report,
                    parsed.MapCatalog ?? FireRedMapCatalogScanner.Scan(rom.CreateReader()));
                var successfulIds = new List<string>(parsed.ResolvedMapIds);
                var result = MapAssetBuilder.Import(new MapAssetImportRequest(snapshot, successfulIds), ShowImportProgress);
                var skipped = Math.Max(0, selected.Count - successfulIds.Count);
                EditorUtility.DisplayDialog(
                    "Map import complete",
                    result.RequestedMapIds.Count + " map(s) imported"
                    + (skipped > 0 ? "; " + skipped + " selected map(s) were skipped as not yet supported" : string.Empty)
                    + ". The generated scene is ready to open.",
                    "OK");
            }
            catch (OperationCanceledException exception)
            {
                report = new ImportReport("MAP_IMPORT");
                report.Add(new ParseDiagnostic("Import", DiagnosticSeverity.Warning, exception.Message));
            }
            catch (Exception exception)
            {
                report = new ImportReport("MAP_IMPORT");
                report.Add(new ParseDiagnostic("Import", DiagnosticSeverity.Error, "Map generation failed before completion: " + exception.Message));
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool MatchesFilter(MapImportDescriptorDefinition map)
        {
            if (map == null) return false;
            if (string.IsNullOrWhiteSpace(filter)) return true;
            return map.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || map.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<string> GetSelectedIdsInCatalogOrder()
        {
            var ids = new List<string>();
            if (catalog == null) return ids;
            for (var index = 0; index < catalog.Maps.Count; index++)
            {
                var id = catalog.Maps[index].Id;
                if (selectedMapIds.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        private void RestoreSelection()
        {
            selectedMapIds.Clear();
            var saved = EditorPrefs.GetString(SelectedMapIdsKey, string.Empty);
            var ids = saved.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < ids.Length; index++) selectedMapIds.Add(ids[index]);
        }

        private void PersistSelection()
        {
            var ids = new List<string>(selectedMapIds);
            ids.Sort(StringComparer.Ordinal);
            EditorPrefs.SetString(SelectedMapIdsKey, string.Join("\n", ids.ToArray()));
        }

        private void RemoveUnavailableSelections()
        {
            var available = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < catalog.Maps.Count; index++) available.Add(catalog.Maps[index].Id);
            selectedMapIds.RemoveWhere(id => !available.Contains(id));
            PersistSelection();
        }

        private static bool ShowImportProgress(string stage, float progress)
        {
            return EditorUtility.DisplayCancelableProgressBar("Import Maps", stage, progress);
        }

        private static void OpenImportedScene()
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), MapAssetBuilder.SceneAssetPath);
            if (!File.Exists(absolutePath)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(MapAssetBuilder.SceneAssetPath, OpenSceneMode.Single);
            }
        }

        private static void DrawDiagnostics(ImportReport importReport)
        {
            if (importReport.Diagnostics.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
            for (var index = 0; index < importReport.Diagnostics.Count; index++)
            {
                var diagnostic = importReport.Diagnostics[index];
                var type = diagnostic.Severity == DiagnosticSeverity.Error
                    ? MessageType.Error
                    : diagnostic.Severity == DiagnosticSeverity.Warning ? MessageType.Warning : MessageType.Info;
                var location = diagnostic.Offset.HasValue
                    ? " (0x" + diagnostic.Offset.Value.ToString("X") + (diagnostic.Length.HasValue ? ", " + diagnostic.Length.Value + " bytes" : string.Empty) + ")"
                    : string.Empty;
                EditorGUILayout.HelpBox("[" + diagnostic.Stage + "/" + diagnostic.Category + "] " + diagnostic.Message + location, type);
            }
        }

        private static int SafeLength(long requestedLength)
        {
            return requestedLength > int.MaxValue ? int.MaxValue : (int)Math.Max(0, requestedLength);
        }
    }
}
