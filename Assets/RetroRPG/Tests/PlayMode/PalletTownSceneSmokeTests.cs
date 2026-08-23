using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroRPG.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace RetroRPG.Tests.PlayMode
{
    public sealed class PalletTownSceneSmokeTests : IPrebuildSetup, IPostBuildCleanup
    {
        private const string SceneAssetPath = "Assets/Imported/FireRed/rev1/PalletTown/PalletTown.unity";
#if UNITY_EDITOR
        private const string BuildSettingsBackupKey = "RetroRPG.Tests.PalletTownSceneSmoke.BuildSettings";
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            var previousScenes = EditorBuildSettings.scenes;
            var backup = new System.Text.StringBuilder();
            for (var i = 0; i < previousScenes.Length; i++)
            {
                if (i > 0) backup.Append('\n');
                backup.Append(previousScenes[i].enabled ? '1' : '0').Append(previousScenes[i].path);
            }
            SessionState.SetString(BuildSettingsBackupKey, backup.ToString());
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneAssetPath, true) };
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            var backup = SessionState.GetString(BuildSettingsBackupKey, string.Empty);
            if (string.IsNullOrEmpty(backup))
            {
                EditorBuildSettings.scenes = System.Array.Empty<EditorBuildSettingsScene>();
            }
            else
            {
                var lines = backup.Split('\n');
                var restored = new EditorBuildSettingsScene[lines.Length];
                for (var i = 0; i < lines.Length; i++)
                {
                    restored[i] = new EditorBuildSettingsScene(lines[i].Substring(1), lines[i][0] == '1');
                }
                EditorBuildSettings.scenes = restored;
            }
            SessionState.EraseString(BuildSettingsBackupKey);
#endif
        }

        [UnityTest, Explicit("Run after the local RETRO_RPG_TEST_ROM integration import has generated Pallet Town.")]
        public IEnumerator GeneratedScene_HasLayeredTilemapsAndSynchronizedAnimation()
        {
            Assert.That(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SceneAssetPath)), Is.True, "Generate Pallet Town through the local integration import first.");
            var load = SceneManager.LoadSceneAsync(SceneAssetPath, LoadSceneMode.Single);
            yield return load;

            var maps = Object.FindObjectsByType<Tilemap>();
            Assert.That(maps, Has.Length.EqualTo(3));
            var byName = new Dictionary<string, Tilemap>();
            for (var i = 0; i < maps.Length; i++) byName.Add(maps[i].name, maps[i]);
            Assert.That(byName.ContainsKey("Bottom"), Is.True);
            Assert.That(byName.ContainsKey("Middle"), Is.True);
            Assert.That(byName.ContainsKey("Top"), Is.True);
            Assert.That(Camera.main, Is.Not.Null);

            Tilemap animatedMap = null;
            Vector3Int animatedPosition = default;
            DeterministicAnimatedTile animatedTile = null;
            foreach (var map in maps)
            {
                foreach (var position in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(position);
                    if (tile == null) continue;
                    Assert.That(map.GetSprite(position), Is.Not.Null, "Missing sprite at " + position);
                    if (tile is DeterministicAnimatedTile)
                    {
                        animatedMap = map;
                        animatedPosition = position;
                        animatedTile = (DeterministicAnimatedTile)tile;
                        break;
                    }
                }
                if (animatedMap != null) break;
            }

            Assert.That(animatedMap, Is.Not.Null, "No generated animated tile was found.");
            var animationData = default(TileAnimationData);
            Assert.That(animatedTile.GetTileAnimationData(animatedPosition, null, ref animationData), Is.True);
            Assert.That(animationData.animatedSprites, Has.Length.GreaterThan(1));
            Assert.That(animationData.animationSpeed, Is.GreaterThan(0f));

            var firstFrame = Mathf.FloorToInt(Time.time * animationData.animationSpeed)
                % animationData.animatedSprites.Length;
            yield return new WaitForSeconds((1f / animationData.animationSpeed) + 0.05f);
            var secondFrame = Mathf.FloorToInt(Time.time * animationData.animationSpeed)
                % animationData.animatedSprites.Length;
            Assert.That(secondFrame, Is.Not.EqualTo(firstFrame), "Animated tile clock did not advance.");
            Assert.That(
                animationData.animatedSprites[secondFrame],
                Is.Not.SameAs(animationData.animatedSprites[firstFrame]),
                "Animated tile did not select a different sprite frame.");
        }
    }
}
