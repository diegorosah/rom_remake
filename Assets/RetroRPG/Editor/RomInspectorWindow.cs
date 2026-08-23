using System;
using System.Collections.Generic;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RetroRPG.Editor
{
    public sealed class RomInspectorWindow : EditorWindow
    {
        private const string LastRomPathKey = "RetroRPG.LastRomPath";

        private string selectedPath;
        private RomFile rom;
        private GbaHeader header;
        private GameDetectionResult detection;
        private ImportReport inspectionReport;
        private string error;
        private Vector2 scroll;

        [MenuItem("Tools/Retro RPG/ROM Inspector")]
        public static void Open()
        {
            GetWindow<RomInspectorWindow>("ROM Inspector");
        }

        private void OnEnable()
        {
            selectedPath = EditorPrefs.GetString(LastRomPathKey, string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Retro RPG ROM Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(selectedPath) ? "No ROM selected" : selectedPath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Select .gba", GUILayout.Width(100)))
                {
                    SelectRom();
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            if (rom != null && header != null && detection != null)
            {
                DrawMetadata();
            }

            DrawDiagnostics();

            EditorGUILayout.EndScrollView();
        }

        private void SelectRom()
        {
            var initialDirectory = string.IsNullOrEmpty(selectedPath)
                ? Application.dataPath
                : System.IO.Path.GetDirectoryName(selectedPath);
            var path = EditorUtility.OpenFilePanel("Select a GBA ROM", initialDirectory, "gba");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            selectedPath = path;
            EditorPrefs.SetString(LastRomPathKey, selectedPath);
            Inspect(path);
        }

        private void Inspect(string path)
        {
            error = string.Empty;
            rom = null;
            header = null;
            detection = null;
            inspectionReport = new ImportReport("ROM_INSPECTION");

            try
            {
                rom = RomFile.Load(path);
                header = GbaHeaderParser.Parse(rom.CreateReader());
                var detector = new GameDetector(new List<IRomGameAdapter>
                {
                    new PokemonFireRedAdapter()
                });
                detection = detector.Detect(header, rom.Fingerprint);
                inspectionReport.Add(new ParseDiagnostic(
                    "Detection",
                    detection.CanImport ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning,
                    detection.Message));
            }
            catch (RomReadException exception)
            {
                error = "The ROM could not be read within its available bounds.";
                inspectionReport.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, error, exception.Offset, SafeLength(exception.RequestedLength)));
            }
            catch (Exception)
            {
                error = "The ROM could not be inspected. Check that it is a readable .gba file.";
                inspectionReport.Add(new ParseDiagnostic("Inspection", DiagnosticSeverity.Error, error));
            }
        }

        private void DrawMetadata()
        {
            EditorGUILayout.LabelField("Header", EditorStyles.boldLabel);
            DrawField("File", rom.FileName);
            DrawField("Size", $"{rom.Fingerprint.Size:N0} bytes");
            DrawField("Title", header.Title);
            DrawField("Game code", header.GameCode);
            DrawField("Maker code", header.MakerCode);
            DrawField("Software version", header.SoftwareVersion.ToString());
            DrawField("Fixed value", $"0x{header.FixedValue:X2} ({(header.HasValidFixedValue ? "valid" : "invalid")})");
            DrawField("Header checksum", $"0x{header.ComplementCheck:X2} ({(header.HasValidComplementCheck ? "valid" : "invalid")})");
            DrawField("SHA-1", rom.Fingerprint.Sha1);
            DrawField("SHA-256", rom.Fingerprint.Sha256);

            EditorGUILayout.Space();
            var messageType = detection.Status == GameDetectionStatus.Supported
                ? MessageType.Info
                : detection.Status == GameDetectionStatus.RecognizedButUnsupported
                    ? MessageType.Warning
                    : MessageType.None;
            EditorGUILayout.HelpBox(detection.Message, messageType);

            using (new EditorGUI.DisabledScope(!detection.CanImport))
            {
                if (GUILayout.Button("Import Pallet Town"))
                {
                    ImportPalletTown(selectedPath);
                }
            }

            if (System.IO.Directory.Exists(PalletTownAssetBuilder.GetOutputFolderAbsolutePath()))
            {
                DrawField("Output", PalletTownAssetBuilder.OutputRoot);
                DrawField("Tile assets", PalletTownAssetBuilder.GetGeneratedTileAssetCount().ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Imported Folder"))
                    {
                        EditorUtility.RevealInFinder(PalletTownAssetBuilder.GetOutputFolderAbsolutePath());
                    }

                    if (GUILayout.Button("Open Pallet Town Scene"))
                    {
                        OpenImportedScene();
                    }
                }
            }
        }

        private void DrawDiagnostics()
        {
            if (inspectionReport == null || inspectionReport.Diagnostics.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
            for (var i = 0; i < inspectionReport.Diagnostics.Count; i++)
            {
                var diagnostic = inspectionReport.Diagnostics[i];
                var type = diagnostic.Severity == DiagnosticSeverity.Error
                    ? MessageType.Error
                    : diagnostic.Severity == DiagnosticSeverity.Warning ? MessageType.Warning : MessageType.Info;
                var location = diagnostic.Offset.HasValue
                    ? " (0x" + diagnostic.Offset.Value.ToString("X") + (diagnostic.Length.HasValue ? ", " + diagnostic.Length.Value + " bytes" : string.Empty) + ")"
                    : string.Empty;
                EditorGUILayout.HelpBox("[" + diagnostic.Stage + "/" + diagnostic.Category + "] " + diagnostic.Message + location, type);
            }
        }

        private static void DrawField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(130));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void ImportPalletTown(string path)
        {
            try
            {
                // Reload the file: the selection snapshot must not be trusted after time has elapsed.
                var snapshot = RomFile.Load(path);
                var snapshotHeader = GbaHeaderParser.Parse(snapshot.CreateReader());
                var snapshotDetection = new GameDetector(new List<IRomGameAdapter> { new PokemonFireRedAdapter() })
                    .Detect(snapshotHeader, snapshot.Fingerprint);
                if (!snapshotDetection.CanImport)
                {
                    inspectionReport = new ImportReport("ROM_INSPECTION");
                    inspectionReport.Add(new ParseDiagnostic("Detection", DiagnosticSeverity.Error, snapshotDetection.Message));
                    return;
                }

                var parsed = new PalletTownParser().Parse(snapshot);
                inspectionReport = parsed.Report;
                if (!parsed.Succeeded)
                {
                    return;
                }

                PalletTownAssetBuilder.Import(parsed.Map, parsed.PlayerSprite, parsed.Report, ShowImportProgress);
                EditorUtility.DisplayDialog("Pallet Town importer", "Pallet Town assets and scene were generated successfully.", "OK");
            }
            catch (OperationCanceledException exception)
            {
                inspectionReport = new ImportReport("UNITY_IMPORT");
                inspectionReport.Add(new ParseDiagnostic("Import", DiagnosticSeverity.Warning, exception.Message));
            }
            catch (RomReadException exception)
            {
                inspectionReport = new ImportReport("UNITY_IMPORT");
                inspectionReport.Add(new ParseDiagnostic("ROM", DiagnosticSeverity.Error, "The selected ROM could not be read within its available bounds.", exception.Offset, SafeLength(exception.RequestedLength)));
            }
            catch (Exception)
            {
                inspectionReport = new ImportReport("UNITY_IMPORT");
                inspectionReport.Add(new ParseDiagnostic("Import", DiagnosticSeverity.Error, "Pallet Town generation failed before completion. See the Inspector diagnostics after correcting the reported input."));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static int SafeLength(long requestedLength)
        {
            return requestedLength > int.MaxValue ? int.MaxValue : (int)Math.Max(0, requestedLength);
        }

        private static bool ShowImportProgress(string stage, float progress)
        {
            return EditorUtility.DisplayCancelableProgressBar("Import Pallet Town", stage, progress);
        }

        private static void OpenImportedScene()
        {
            const string scene = PalletTownAssetBuilder.OutputRoot + "/PalletTown.unity";
            if (!System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), scene))) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            }
        }
    }
}
