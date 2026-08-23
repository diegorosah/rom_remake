using System;
using System.IO;
using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;

namespace RetroRPG.Tests.EditMode
{
    public sealed class PalletTownParserIntegrationTests
    {
        [TestCase(0, "BPRE", 0)]
        [TestCase(1, "BPRE", 1)]
        [TestCase(0, "XXXX", 1)]
        public void RejectsUnsupportedOrUnrecognizedSnapshots(int fill, string gameCode, byte version)
        {
            var bytes = BuildHeader((byte)fill, gameCode, version);
            var path = Path.Combine(Path.GetTempPath(), "rrpg-parser-" + Guid.NewGuid().ToString("N") + ".gba");
            try
            {
                File.WriteAllBytes(path, bytes);
                var result = new PalletTownParser().Parse(RomFile.Load(path));

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.Diagnostics[0].Message, Does.Not.Contain(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test, Explicit("Requires RETRO_RPG_TEST_ROM to point to the user's legal FireRed USA rev1 ROM.")]
        public void ParsesVerifiedPalletTownFromLocalRom()
        {
            var path = Environment.GetEnvironmentVariable("RETRO_RPG_TEST_ROM");
            Assert.That(path, Is.Not.Null.And.Not.Empty, "Set RETRO_RPG_TEST_ROM before explicitly running this integration test.");

            var result = new PalletTownParser().Parse(RomFile.Load(path));

            Assert.That(result.Succeeded, Is.True, "The supported ROM should parse without diagnostics errors.");
            Assert.That(result.Map.Width, Is.EqualTo(24));
            Assert.That(result.Map.Height, Is.EqualTo(20));
            Assert.That(result.Map.Cells.Count, Is.EqualTo(480));
            Assert.That(result.Map.PrimaryTileset.Id, Is.EqualTo("General"));
            Assert.That(result.Map.SecondaryTileset.Id, Is.EqualTo("PalletTown"));
            Assert.That(result.Map.PrimaryTileset.Animations.Count, Is.EqualTo(3));
        }

        [Test]
        public void RejectsRandomSnapshot()
        {
            var path = Path.Combine(Path.GetTempPath(), "rrpg-random-" + Guid.NewGuid().ToString("N") + ".gba");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x4A, 0xC7, 0x91 });
                var result = new PalletTownParser().Parse(RomFile.Load(path));

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Report.HasErrors, Is.True);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static byte[] BuildHeader(byte fill, string gameCode, byte version)
        {
            var bytes = new byte[0xC0];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = fill;
            var gameCodeBytes = System.Text.Encoding.ASCII.GetBytes(gameCode);
            Buffer.BlockCopy(gameCodeBytes, 0, bytes, 0xAC, 4);
            bytes[0xB0] = (byte)'0';
            bytes[0xB1] = (byte)'1';
            bytes[0xB2] = 0x96;
            bytes[0xBC] = version;
            var checksum = 0;
            for (var offset = 0xA0; offset <= 0xBC; offset++) checksum = (checksum - bytes[offset]) & 0xFF;
            bytes[0xBD] = (byte)((checksum - 0x19) & 0xFF);
            return bytes;
        }
    }
}
