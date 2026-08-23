using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using RetroRPG.Editor;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroRPG.Tests.EditMode
{
    public sealed class PalletTownGenerationIntegrationTests
    {
        private const string TestRomEnvironmentVariable = "RETRO_RPG_TEST_ROM";

        [Test, Explicit("Requires a locally owned supported ROM through RETRO_RPG_TEST_ROM.")]
        public void SupportedRom_GeneratesDeterministicPalletTownAssetsOnReimport()
        {
            var romPath = Environment.GetEnvironmentVariable(TestRomEnvironmentVariable);
            Assert.That(romPath, Is.Not.Null.And.Not.Empty, TestRomEnvironmentVariable + " must point to a local ROM.");

            var firstResult = ParseSupportedRom(romPath);
            PalletTownAssetBuilder.Import(firstResult.Bundle, firstResult.PlayerSprite, firstResult.ObjectSprites, firstResult.Report, null);
            var first = CaptureGeneratedAssets();
            Assert.That(first.Count, Is.GreaterThan(3));
            Assert.That(ContainsPath(first, "/Player/"), Is.True);
            Assert.That(ContainsPath(first, "/Objects/"), Is.True);
            Assert.That(ContainsPath(first, ".unity"), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(PalletTownAssetBuilder.GetOutputFolderAbsolutePath(), "PalletTown.ir.json")), Does.Contain("\"schemaVersion\": 3"));
            Assert.That(ContainsPath(first, "/MAP_PALLET_TOWN_PLAYERS_HOUSE_1F/"), Is.True);
            Assert.That(ContainsPath(first, "/MAP_PALLET_TOWN_PLAYERS_HOUSE_2F/"), Is.True);
            Assert.That(ContainsPath(first, "/MAP_PALLET_TOWN_RIVALS_HOUSE/"), Is.True);
            AssertGeneratedSceneComponents();

            var secondResult = ParseSupportedRom(romPath);
            PalletTownAssetBuilder.Import(secondResult.Bundle, secondResult.PlayerSprite, secondResult.ObjectSprites, secondResult.Report, null);
            var second = CaptureGeneratedAssets();

            CollectionAssert.AreEquivalent(first.Keys, second.Keys);
            foreach (var path in first.Keys)
            {
                Assert.That(second[path].Guid, Is.EqualTo(first[path].Guid), "GUID changed for " + path);
                Assert.That(second[path].ContentHash, Is.EqualTo(first[path].ContentHash), "content changed for " + path);
            }
        }

        private static bool ContainsPath(Dictionary<string, GeneratedAssetSnapshot> assets, string fragment)
        {
            foreach (var path in assets.Keys)
            {
                if (path.IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static void AssertGeneratedSceneComponents()
        {
            const string scenePath = PalletTownAssetBuilder.OutputRoot + "/PalletTown.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects();
            GameObject palletTown = null;
            GameObject camera = null;
            for (var i = 0; i < root.Length; i++)
            {
                if (root[i].name == "Pallet Town") palletTown = root[i];
                if (root[i].name == "Maps" && root[i].transform.Find("MAP_PALLET_TOWN") != null)
                {
                    palletTown = root[i].transform.Find("MAP_PALLET_TOWN").gameObject;
                }
                if (root[i].name == "Main Camera") camera = root[i];
            }

            Assert.That(palletTown, Is.Not.Null);
            Assert.That(palletTown.transform.Find("Bottom").GetComponent<UnityEngine.Tilemaps.Tilemap>(), Is.Not.Null);
            Assert.That(palletTown.transform.Find("Middle").GetComponent<UnityEngine.Tilemaps.Tilemap>(), Is.Not.Null);
            Assert.That(palletTown.transform.Find("Top").GetComponent<UnityEngine.Tilemaps.Tilemap>(), Is.Not.Null);
            Assert.That(palletTown.transform.Find("Collision").GetComponent<GridCollisionMap>(), Is.Not.Null);
            var mapRoots = palletTown.transform.parent.GetComponentsInChildren<MapRuntimeRoot>(true);
            Assert.That(mapRoots, Has.Length.EqualTo(4));
            var npcCount = 0;
            for (var index = 0; index < mapRoots.Length; index++)
            {
                Assert.That(mapRoots[index].CollisionMap, Is.Not.Null);
                Assert.That(mapRoots[index].Occupancy, Is.Not.Null);
                Assert.That(mapRoots[index].Warps, Is.Not.Null);
                Assert.That(mapRoots[index].GetComponent<NpcSimulationDriver>(), Is.Not.Null);
                npcCount += mapRoots[index].Npcs.Count;
            }
            Assert.That(npcCount, Is.EqualTo(5));
            Transform player = null;
            {
                var roots = scene.GetRootGameObjects();
                for (var index = 0; index < roots.Length; index++)
                {
                    if (roots[index].name == "Player") { player = roots[index].transform; break; }
                }
            }
            Assert.That(player, Is.Not.Null);
            var playerController = player.GetComponent<PlayerController>();
            Assert.That(playerController, Is.Not.Null);
            Assert.That(playerController.Occupancy, Is.SameAs(palletTown.GetComponent<MapRuntimeRoot>().Occupancy));
            Assert.That(player.GetComponent<DirectionalSpriteAnimator>(), Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.GetComponent<PixelPerfectCameraFollow>(), Is.Not.Null);
            var catalog = UnityEngine.Object.FindAnyObjectByType<RuntimeMapCatalog>();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Maps, Has.Count.EqualTo(4));
            Assert.That(catalog.GetComponent<MapTransitionSystem>(), Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
        }

        private static FireRedMapBundleParseResult ParseSupportedRom(string romPath)
        {
            var rom = RomFile.Load(romPath);
            Assert.That(rom.Fingerprint.Sha1, Is.EqualTo(PokemonFireRedAdapter.SupportedSha1));
            var result = new FireRedMapBundleParser().Parse(rom);
            Assert.That(result.Succeeded, Is.True, "Bundle parser failed with " + result.Report.Diagnostics.Count + " diagnostics.");
            Assert.That(result.Bundle.Maps, Has.Count.EqualTo(4));
            Assert.That(result.Bundle.GetMap(FireRedRomLayoutRev1.PalletTownMapId).Cells, Has.Count.EqualTo(480));
            return result;
        }

        private static Dictionary<string, GeneratedAssetSnapshot> CaptureGeneratedAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var absoluteRoot = PalletTownAssetBuilder.GetOutputFolderAbsolutePath();
            var results = new Dictionary<string, GeneratedAssetSnapshot>(StringComparer.Ordinal);
            foreach (var absolutePath in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (absolutePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                var relativePath = "Assets" + absolutePath.Substring(Path.GetFullPath("Assets").Length).Replace('\\', '/');
                results.Add(relativePath, new GeneratedAssetSnapshot(
                    AssetDatabase.AssetPathToGUID(relativePath),
                    ComputeSha256(absolutePath)));
            }
            return results;
        }

        private static string ComputeSha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class GeneratedAssetSnapshot
        {
            public GeneratedAssetSnapshot(string guid, string contentHash)
            {
                Guid = guid;
                ContentHash = contentHash;
            }

            public string Guid { get; }
            public string ContentHash { get; }
        }
    }
}
