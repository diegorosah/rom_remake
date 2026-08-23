using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using RetroRPG.Editor;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using UnityEditor;

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
            PalletTownAssetBuilder.Import(firstResult.Map, firstResult.Report, null);
            var first = CaptureGeneratedAssets();
            Assert.That(first.Count, Is.GreaterThan(3));

            var secondResult = ParseSupportedRom(romPath);
            PalletTownAssetBuilder.Import(secondResult.Map, secondResult.Report, null);
            var second = CaptureGeneratedAssets();

            CollectionAssert.AreEquivalent(first.Keys, second.Keys);
            foreach (var path in first.Keys)
            {
                Assert.That(second[path].Guid, Is.EqualTo(first[path].Guid), "GUID changed for " + path);
                Assert.That(second[path].ContentHash, Is.EqualTo(first[path].ContentHash), "content changed for " + path);
            }
        }

        private static PalletTownParseResult ParseSupportedRom(string romPath)
        {
            var rom = RomFile.Load(romPath);
            Assert.That(rom.Fingerprint.Sha1, Is.EqualTo(PokemonFireRedAdapter.SupportedSha1));
            var result = new PalletTownParser().Parse(rom);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Report.Diagnostics));
            Assert.That(result.Map.Width, Is.EqualTo(24));
            Assert.That(result.Map.Height, Is.EqualTo(20));
            Assert.That(result.Map.Cells.Count, Is.EqualTo(480));
            Assert.That(result.Map.PrimaryTileset.Id, Is.EqualTo("General"));
            Assert.That(result.Map.SecondaryTileset.Id, Is.EqualTo("PalletTown"));
            Assert.That(result.Map.PrimaryTileset.Animations.Count, Is.GreaterThan(0));
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
