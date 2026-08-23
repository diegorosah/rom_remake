using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.IR;
using UnityEngine;

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

        [Test, Explicit("Requires a locally owned supported ROM through RETRO_RPG_TEST_ROM.")]
        public void ParsesVerifiedPalletTownBundleWithFourMapsAndTransitionFlow()
        {
            var path = Environment.GetEnvironmentVariable("RETRO_RPG_TEST_ROM");
            Assert.That(path, Is.Not.Null.And.Not.Empty, "Set RETRO_RPG_TEST_ROM before explicitly running this integration test.");

            var result = new FireRedMapBundleParser().Parse(RomFile.Load(path));
            Assert.That(result.Succeeded, Is.True, DescribeDiagnostics(result.Report));
            Assert.That(result.ObjectSprites, Is.Not.Null);
            Assert.That(result.ObjectSprites.MobileSprites, Has.Count.EqualTo(5));
            Assert.That(result.ObjectSprites.StaticSprites, Has.Count.EqualTo(1));
            for (var mobileIndex = 0; mobileIndex < result.ObjectSprites.MobileSprites.Count; mobileIndex++)
            {
                var mobile = result.ObjectSprites.MobileSprites[mobileIndex];
                Assert.That(mobile.Width, Is.EqualTo(16));
                Assert.That(mobile.Height, Is.EqualTo(32));
                Assert.That(mobile.Palette, Has.Count.EqualTo(16));
                for (var frameIndex = 0; frameIndex < mobile.Frames.Count; frameIndex++)
                {
                    Assert.That(mobile.Frames[frameIndex].Pixels, Has.Count.EqualTo(16 * 32));
                }
            }
            Assert.That(result.ObjectSprites.TryGetStatic("prop_town_map", out var mapProp), Is.True);
            Assert.That(mapProp.Width, Is.EqualTo(32));
            Assert.That(mapProp.Height, Is.EqualTo(16));
            Assert.That(mapProp.Frames, Has.Count.EqualTo(1));
            Assert.That(result.DialogueCatalog, Is.Not.Null);
            Assert.That(result.DialogueCatalog.Dialogues, Has.Count.EqualTo(2));
            Assert.That(result.DialogueCatalog.TryGetForTarget(FireRedRomLayoutRev1.PalletTownMapId + ":object:2", out var fatManDialogue), Is.True);
            Assert.That(fatManDialogue.FacePlayer, Is.True);
            Assert.That(result.DialogueCatalog.TryGetForTarget(FireRedRomLayoutRev1.RivalsHouseMapId + ":object:2", out var townMapDialogue), Is.True);
            Assert.That(townMapDialogue.FacePlayer, Is.False);
            Assert.That(result.Bundle.Maps, Has.Count.EqualTo(5));
            Assert.That(result.Bundle.Maps[0].Id, Is.EqualTo(FireRedRomLayoutRev1.PalletTownMapId));

            var expected = new Dictionary<string, Vector2Int>
            {
                { FireRedRomLayoutRev1.PalletTownMapId, new Vector2Int(24, 20) },
                { FireRedRomLayoutRev1.PlayersHouse1FMapId, new Vector2Int(13, 10) },
                { FireRedRomLayoutRev1.PlayersHouse2FMapId, new Vector2Int(12, 9) },
                { FireRedRomLayoutRev1.RivalsHouseMapId, new Vector2Int(13, 10) },
                { FireRedRomLayoutRev1.Route1MapId, new Vector2Int(24, 40) },
            };
            var totalCells = 0;
            var totalWarps = 0;
            var palletTown = result.Bundle.GetMap(FireRedRomLayoutRev1.PalletTownMapId);
            var sawOakExternal = false;
            for (var mapIndex = 0; mapIndex < result.Bundle.Maps.Count; mapIndex++)
            {
                var map = result.Bundle.Maps[mapIndex];
                Assert.That(expected.ContainsKey(map.Id), Is.True, "Unexpected map in bounded bundle: " + map.Id);
                Assert.That(new Vector2Int(map.Width, map.Height), Is.EqualTo(expected[map.Id]));
                Assert.That(map.Cells, Has.Count.EqualTo(map.Width * map.Height));
                Assert.That(map.PrimaryTileset, Is.Not.Null);
                Assert.That(map.SecondaryTileset, Is.Not.Null);
                totalCells += map.Cells.Count;
                totalWarps += map.Warps.Count;
                for (var warpIndex = 0; warpIndex < map.Warps.Count; warpIndex++)
                {
                    var warp = map.Warps[warpIndex];
                    Assert.That(warp.SourceX, Is.InRange(0, map.Width - 1));
                    Assert.That(warp.SourceY, Is.InRange(0, map.Height - 1));
                    if (warp.DestinationMapId == FireRedRomLayoutRev1.OakLabMapId) sawOakExternal = true;
                }
            }

            Assert.That(totalCells, Is.EqualTo(1808));
            Assert.That(totalWarps, Is.EqualTo(11));
            Assert.That(palletTown.PrimaryTileset.Id, Is.EqualTo("General"));
            Assert.That(palletTown.SecondaryTileset.Id, Is.EqualTo("PalletTown"));
            Assert.That(palletTown.Warps, Has.Count.EqualTo(3));
            Assert.That(sawOakExternal, Is.True);
            var sawOakWarning = false;
            for (var diagnosticIndex = 0; diagnosticIndex < result.Report.Diagnostics.Count; diagnosticIndex++)
            {
                var diagnostic = result.Report.Diagnostics[diagnosticIndex];
                if (diagnostic.Category == "Warp" && diagnostic.Severity == RetroRPG.Core.DiagnosticSeverity.Warning &&
                    diagnostic.Message.Contains("Oak's Lab") && diagnostic.Message.Contains("external"))
                {
                    sawOakWarning = true;
                    break;
                }
            }
            Assert.That(sawOakWarning, Is.True);
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
