using System;
using System.IO;
using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.IR;

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

            Assert.That(result.Succeeded, Is.True,
                "The supported ROM should parse without diagnostics errors. " + DescribeDiagnostics(result.Report));
            Assert.That(result.Map.Width, Is.EqualTo(24));
            Assert.That(result.Map.Height, Is.EqualTo(20));
            Assert.That(result.Map.Cells.Count, Is.EqualTo(480));
            Assert.That(result.Map.PrimaryTileset.Id, Is.EqualTo("General"));
            Assert.That(result.Map.SecondaryTileset.Id, Is.EqualTo("PalletTown"));
            Assert.That(result.Map.PrimaryTileset.Animations.Count, Is.EqualTo(3));
            Assert.That(result.PlayerSprite, Is.Not.Null);
            Assert.That(result.PlayerSprite.Id, Is.EqualTo("player_red_normal"));
            Assert.That(result.PlayerSprite.Width, Is.EqualTo(16));
            Assert.That(result.PlayerSprite.Height, Is.EqualTo(32));
            Assert.That(result.PlayerSprite.Frames, Has.Count.EqualTo(9));
            Assert.That(result.PlayerSprite.Palette, Has.Count.EqualTo(16));
            for (var frame = 0; frame < result.PlayerSprite.Frames.Count; frame++)
            {
                Assert.That(result.PlayerSprite.Frames[frame].Pixels, Has.Count.EqualTo(16 * 32));
                for (var pixel = 0; pixel < result.PlayerSprite.Frames[frame].Pixels.Count; pixel++)
                {
                    Assert.That(result.PlayerSprite.Frames[frame].Pixels[pixel], Is.InRange((byte)0, (byte)15));
                }
            }

            Assert.That(result.PlayerSprite.Animations, Has.Count.EqualTo(8));
            var expectedDirections = new[] { SpriteDirection.South, SpriteDirection.North, SpriteDirection.West, SpriteDirection.East };
            var expectedIdle = new[] { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 2 } };
            var expectedWalking = new[] { new[] { 3, 0, 4, 0 }, new[] { 5, 1, 6, 1 }, new[] { 7, 2, 8, 2 }, new[] { 7, 2, 8, 2 } };
            for (var direction = 0; direction < expectedDirections.Length; direction++)
            {
                var idle = result.PlayerSprite.Animations[direction];
                var walking = result.PlayerSprite.Animations[direction + 4];
                Assert.That(idle.Direction, Is.EqualTo(expectedDirections[direction]));
                Assert.That(idle.State, Is.EqualTo(SpriteAnimationState.Idle));
                Assert.That(idle.Steps, Has.Count.EqualTo(1));
                Assert.That(idle.Steps[0].FrameIndex, Is.EqualTo(expectedIdle[direction][0]));
                Assert.That(idle.Steps[0].DurationTicks, Is.EqualTo(16));
                Assert.That(idle.Steps[0].HorizontalFlip, Is.EqualTo(direction == 3));
                Assert.That(idle.Steps[0].VerticalFlip, Is.False);
                Assert.That(walking.Direction, Is.EqualTo(expectedDirections[direction]));
                Assert.That(walking.State, Is.EqualTo(SpriteAnimationState.Walking));
                Assert.That(walking.Steps, Has.Count.EqualTo(4));
                Assert.That(walking.Steps[0].DurationTicks, Is.EqualTo(8));
                Assert.That(walking.Steps[0].HorizontalFlip, Is.EqualTo(direction == 3));
                Assert.That(walking.Steps[0].VerticalFlip, Is.False);
                for (var step = 0; step < 4; step++) Assert.That(walking.Steps[step].FrameIndex, Is.EqualTo(expectedWalking[direction][step]));
            }

            var blocked = 0;
            var walkable = 0;
            var elevationZero = 0;
            var elevationOne = 0;
            var elevationThree = 0;
            for (var cell = 0; cell < result.Map.Cells.Count; cell++)
            {
                var definition = result.Map.Cells[cell];
                if (definition.IsBlocked) blocked++; else walkable++;
                if (definition.Elevation == 0) elevationZero++;
                else if (definition.Elevation == 1) elevationOne++;
                else if (definition.Elevation == 3) elevationThree++;
                Assert.That(definition.IsBlocked, Is.EqualTo(definition.Collision != 0));
            }
            Assert.That(walkable, Is.EqualTo(282));
            Assert.That(blocked, Is.EqualTo(198));
            Assert.That(elevationZero, Is.EqualTo(198));
            Assert.That(elevationOne, Is.EqualTo(12));
            Assert.That(elevationThree, Is.EqualTo(270));
        }

        private static string DescribeDiagnostics(RetroRPG.Core.ImportReport report)
        {
            var summary = new System.Text.StringBuilder();
            for (var i = 0; i < report.Diagnostics.Count; i++)
            {
                if (i > 0) summary.Append(" | ");
                summary.Append(report.Diagnostics[i].Category).Append(": ").Append(report.Diagnostics[i].Message);
            }

            return summary.ToString();
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
