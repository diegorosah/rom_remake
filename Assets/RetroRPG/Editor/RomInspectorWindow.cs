using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using UnityEditor;
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

            try
            {
                rom = RomFile.Load(path);
                header = GbaHeaderParser.Parse(rom.CreateReader());
                var detector = new GameDetector(new List<IRomGameAdapter>
                {
                    new PokemonFireRedAdapter()
                });
                detection = detector.Detect(header, rom.Fingerprint);
            }
            catch (Exception exception)
            {
                error = exception.Message;
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
                    ImportPalletTown(path: selectedPath);
                }
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

        private static void ImportPalletTown(string path)
        {
            EditorUtility.DisplayDialog(
                "Pallet Town importer",
                "The ROM is supported. The Pallet Town asset pipeline will run from this command once the MVP 1 builder is loaded.",
                "OK");
        }
    }
}

