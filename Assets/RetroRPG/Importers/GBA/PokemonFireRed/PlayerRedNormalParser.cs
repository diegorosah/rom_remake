using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>
    /// Reads the verified on-foot player sprite for the supported FireRed revision.
    /// ROM animation data is accepted only as exact declarative frame sequences.
    /// </summary>
    internal static class PlayerRedNormalParser
    {
        private const string SpriteId = "player_red_normal";

        public static OverworldSpriteDefinition Parse(RomReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            ValidateGraphicsInfo(reader);
            ValidatePalette(reader);
            ValidateImageTable(reader);
            var animations = DecodeAnimations(reader);
            var frames = DecodeFrames(reader);
            if (frames.Count != FireRedRomLayoutRev1.PlayerRedNormalFrameCount)
            {
                throw new InvalidOperationException("The verified Player Red sprite must contain exactly nine frames.");
            }

            var palette = DecodePalette(reader);

            return new OverworldSpriteDefinition(
                SpriteId,
                FireRedRomLayoutRev1.PlayerRedNormalWidth,
                FireRedRomLayoutRev1.PlayerRedNormalHeight,
                palette,
                frames,
                animations);
        }

        private static void ValidateGraphicsInfo(RomReader reader)
        {
            var pointerEntry = checked(
                FireRedRomLayoutRev1.ObjectEventGraphicsInfoPointerTable
                + (FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfoPointerIndex * FireRedRomLayoutRev1.GbaPointerSize));
            ExpectPointer(reader, pointerEntry, FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfo, "Player Red graphics-info table entry");

            var info = FireRedRomLayoutRev1.PlayerRedNormalGraphicsInfo;
            reader.EnsureRange(info, FireRedRomLayoutRev1.ObjectEventGraphicsInfoSize, "Player Red ObjectEventGraphicsInfo is outside ROM bounds.");
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoTileTagOffset)), FireRedRomLayoutRev1.PlayerRedNormalTileTag, "Player Red tile tag", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoTileTagOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteTagOffset)), FireRedRomLayoutRev1.PlayerRedNormalPaletteTag, "Player Red palette tag", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteTagOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoReflectionPaletteTagOffset)), FireRedRomLayoutRev1.PlayerRedNormalReflectionPaletteTag, "Player Red reflection palette tag", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoReflectionPaletteTagOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAllocationSizeOffset)), FireRedRomLayoutRev1.PlayerRedNormalAllocationSize, "Player Red allocation size", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAllocationSizeOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoWidthOffset)), FireRedRomLayoutRev1.PlayerRedNormalWidth, "Player Red width", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoWidthOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoHeightOffset)), FireRedRomLayoutRev1.PlayerRedNormalHeight, "Player Red height", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoHeightOffset);
            ExpectEqual(reader, reader.ReadByte(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteAndShadowOffset)), FireRedRomLayoutRev1.PlayerRedNormalPaletteAndShadow, "Player Red palette/shadow flags", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteAndShadowOffset);
            ExpectEqual(reader, reader.ReadByte(checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoTracksOffset)), FireRedRomLayoutRev1.PlayerRedNormalTracks, "Player Red track type", info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoTracksOffset);
            ExpectPointer(reader, checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoOamOffset), FireRedRomLayoutRev1.PlayerRedNormalOam, "Player Red OAM data");
            ExpectPointer(reader, checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoSubspriteTablesOffset), FireRedRomLayoutRev1.PlayerRedNormalSubspriteTables, "Player Red subsprite tables");
            ExpectPointer(reader, checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAnimationsOffset), FireRedRomLayoutRev1.PlayerRedNormalAnimationTable, "Player Red animation table");
            ExpectPointer(reader, checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoImagesOffset), FireRedRomLayoutRev1.PlayerRedNormalImageTable, "Player Red image table");
            ExpectPointer(reader, checked(info + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAffineAnimationsOffset), FireRedRomLayoutRev1.PlayerRedNormalAffineAnimationTable, "Player Red affine-animation table");
        }

        private static void ValidatePalette(RomReader reader)
        {
            reader.EnsureRange(FireRedRomLayoutRev1.ObjectEventPaletteEntry, FireRedRomLayoutRev1.SpritePaletteSize, "Player Red sprite palette entry is outside ROM bounds.");
            ExpectPointer(reader, checked(FireRedRomLayoutRev1.ObjectEventPaletteEntry + FireRedRomLayoutRev1.SpritePaletteDataOffset), FireRedRomLayoutRev1.PlayerRedNormalPalette, "Player Red palette data");
            ExpectEqual(reader, reader.ReadUInt16(checked(FireRedRomLayoutRev1.ObjectEventPaletteEntry + FireRedRomLayoutRev1.SpritePaletteTagOffset)), FireRedRomLayoutRev1.PlayerRedNormalPaletteTag, "Player Red palette entry tag", FireRedRomLayoutRev1.ObjectEventPaletteEntry + FireRedRomLayoutRev1.SpritePaletteTagOffset);
            reader.EnsureRange(FireRedRomLayoutRev1.PlayerRedNormalPalette, FireRedRomLayoutRev1.PlayerRedNormalPaletteByteSize, "Player Red palette colours are outside ROM bounds.");
        }

        private static void ValidateImageTable(RomReader reader)
        {
            var imageTableBytes = checked(FireRedRomLayoutRev1.PlayerRedNormalFrameCount * FireRedRomLayoutRev1.SpriteFrameImageSize);
            var graphicsByteSize = checked(FireRedRomLayoutRev1.PlayerRedNormalFrameCount * FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize);
            if (graphicsByteSize != FireRedRomLayoutRev1.PlayerRedNormalGraphicsByteSize)
            {
                throw new InvalidOperationException("The verified Player Red frame table size does not match the raw graphics range.");
            }

            reader.EnsureRange(FireRedRomLayoutRev1.PlayerRedNormalImageTable, imageTableBytes, "Player Red SpriteFrameImage table is outside ROM bounds.");
            reader.EnsureRange(FireRedRomLayoutRev1.PlayerRedNormalGraphics, FireRedRomLayoutRev1.PlayerRedNormalGraphicsByteSize, "Player Red raw frame graphics are outside ROM bounds.");

            for (var frameIndex = 0; frameIndex < FireRedRomLayoutRev1.PlayerRedNormalFrameCount; frameIndex++)
            {
                var entry = checked(FireRedRomLayoutRev1.PlayerRedNormalImageTable + (frameIndex * FireRedRomLayoutRev1.SpriteFrameImageSize));
                var expectedFrameOffset = checked(FireRedRomLayoutRev1.PlayerRedNormalGraphics + (frameIndex * FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize));
                ExpectPointer(reader, checked(entry + FireRedRomLayoutRev1.SpriteFrameImageDataOffset), expectedFrameOffset, "Player Red SpriteFrameImage data");
                ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.SpriteFrameImageByteSizeOffset)), FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize, "Player Red SpriteFrameImage byte size", entry + FireRedRomLayoutRev1.SpriteFrameImageByteSizeOffset);
                reader.EnsureRange(expectedFrameOffset, FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize, "Player Red frame is outside ROM bounds.");
            }
        }

        private static List<DirectionalSpriteAnimationDefinition> DecodeAnimations(RomReader reader)
        {
            var tableBytes = checked(FireRedRomLayoutRev1.SpriteAnimationPointerCount * FireRedRomLayoutRev1.GbaPointerSize);
            reader.EnsureRange(FireRedRomLayoutRev1.PlayerRedNormalAnimationTable, tableBytes, "Player Red animation pointer table is outside ROM bounds.");
            if (FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts.Count != FireRedRomLayoutRev1.SpriteAnimationPointerCount)
            {
                throw new InvalidOperationException("The verified Player Red animation script layout is incomplete.");
            }

            var animations = new List<DirectionalSpriteAnimationDefinition>(FireRedRomLayoutRev1.SpriteAnimationPointerCount);
            for (var scriptIndex = 0; scriptIndex < FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts.Count; scriptIndex++)
            {
                var script = FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts[scriptIndex];
                if (script == null || script.TableIndex != scriptIndex)
                {
                    throw new InvalidOperationException("The verified Player Red animation script order is invalid.");
                }

                var expectedFrameCommandCount = script.TableIndex < FireRedRomLayoutRev1.PlayerRedNormalDirectionCount
                    ? FireRedRomLayoutRev1.PlayerRedIdleAnimationCommandCount - 1
                    : FireRedRomLayoutRev1.PlayerRedWalkingAnimationCommandCount - 1;
                if (script.FrameIndices.Count != expectedFrameCommandCount)
                {
                    throw new InvalidOperationException("The verified Player Red animation command count is invalid.");
                }

                var tableEntry = checked(FireRedRomLayoutRev1.PlayerRedNormalAnimationTable + (script.TableIndex * FireRedRomLayoutRev1.GbaPointerSize));
                ExpectPointer(reader, tableEntry, script.Offset, "Player Red animation script pointer");
                var expectedCommandCount = script.FrameIndices.Count + 1;
                var expectedByteCount = checked(expectedCommandCount * FireRedRomLayoutRev1.SpriteAnimationCommandSize);
                reader.EnsureRange(script.Offset, expectedByteCount, "Player Red animation script is outside ROM bounds.");
                ValidateAnimationScript(reader, script);

                var direction = GetDirection(script.TableIndex);
                var state = script.TableIndex < FireRedRomLayoutRev1.PlayerRedNormalDirectionCount
                    ? SpriteAnimationState.Idle
                    : SpriteAnimationState.Walking;
                animations.Add(new DirectionalSpriteAnimationDefinition(direction, state, CreateSteps(script)));
            }

            return animations;
        }

        private static void ValidateAnimationScript(RomReader reader, FireRedRomLayoutRev1.PlayerRedAnimationScript script)
        {
            var expectedFlagBits = checked((ushort)(script.DurationTicks
                | (script.HorizontalFlip ? FireRedRomLayoutRev1.SpriteAnimationHorizontalFlipMask : 0)
                | (script.VerticalFlip ? FireRedRomLayoutRev1.SpriteAnimationVerticalFlipMask : 0)));
            if ((expectedFlagBits & ~FireRedRomLayoutRev1.SpriteAnimationAllowedFlagsMask) != 0)
            {
                throw new InvalidOperationException("The verified Player Red animation duration exceeds the command format.");
            }

            for (var commandIndex = 0; commandIndex < script.FrameIndices.Count; commandIndex++)
            {
                var commandOffset = checked(script.Offset + (commandIndex * FireRedRomLayoutRev1.SpriteAnimationCommandSize));
                var expectedFrame = script.FrameIndices[commandIndex];
                if (expectedFrame < 0 || expectedFrame >= FireRedRomLayoutRev1.PlayerRedNormalFrameCount)
                {
                    throw new InvalidOperationException("The verified Player Red animation references an invalid frame.");
                }

                ExpectEqual(reader, reader.ReadUInt16(checked(commandOffset + FireRedRomLayoutRev1.SpriteAnimationFrameValueOffset)), checked((ushort)expectedFrame), "Player Red animation frame", commandOffset + FireRedRomLayoutRev1.SpriteAnimationFrameValueOffset);
                ExpectEqual(reader, reader.ReadUInt16(checked(commandOffset + FireRedRomLayoutRev1.SpriteAnimationFlagsOffset)), expectedFlagBits, "Player Red animation frame flags", commandOffset + FireRedRomLayoutRev1.SpriteAnimationFlagsOffset);
            }

            var jumpOffset = checked(script.Offset + (script.FrameIndices.Count * FireRedRomLayoutRev1.SpriteAnimationCommandSize));
            ExpectEqual(reader, reader.ReadUInt16(checked(jumpOffset + FireRedRomLayoutRev1.SpriteAnimationFrameValueOffset)), FireRedRomLayoutRev1.SpriteAnimationJumpOpcode, "Player Red animation loop opcode", jumpOffset + FireRedRomLayoutRev1.SpriteAnimationFrameValueOffset);
            ExpectEqual(reader, reader.ReadUInt16(checked(jumpOffset + FireRedRomLayoutRev1.SpriteAnimationFlagsOffset)), FireRedRomLayoutRev1.SpriteAnimationJumpTargetZero, "Player Red animation loop target", jumpOffset + FireRedRomLayoutRev1.SpriteAnimationFlagsOffset);
        }

        private static List<SpriteAnimationStepDefinition> CreateSteps(FireRedRomLayoutRev1.PlayerRedAnimationScript script)
        {
            var steps = new List<SpriteAnimationStepDefinition>(script.FrameIndices.Count);
            for (var i = 0; i < script.FrameIndices.Count; i++)
            {
                steps.Add(new SpriteAnimationStepDefinition(script.FrameIndices[i], script.HorizontalFlip, script.VerticalFlip, script.DurationTicks));
            }

            return steps;
        }

        private static List<IndexedSpriteFrameDefinition> DecodeFrames(RomReader reader)
        {
            var frames = new List<IndexedSpriteFrameDefinition>(FireRedRomLayoutRev1.PlayerRedNormalFrameCount);
            var expectedTileCount = checked(FireRedRomLayoutRev1.PlayerRedNormalTilesWide * FireRedRomLayoutRev1.PlayerRedNormalTilesHigh);
            var expectedFrameByteSize = checked(expectedTileCount * FireRedGraphicsDecoder.BytesPer4BppTile);
            if (expectedFrameByteSize != FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize)
            {
                throw new InvalidOperationException("The verified Player Red frame dimensions do not match the 4bpp frame byte size.");
            }

            for (var frameIndex = 0; frameIndex < FireRedRomLayoutRev1.PlayerRedNormalFrameCount; frameIndex++)
            {
                var frameOffset = checked(FireRedRomLayoutRev1.PlayerRedNormalGraphics + (frameIndex * FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize));
                var tiles = FireRedGraphicsDecoder.Decode4BppTiles(reader.ReadBytes(frameOffset, FireRedRomLayoutRev1.PlayerRedNormalFrameByteSize), 0);
                if (tiles.Count != expectedTileCount)
                {
                    throw new InvalidOperationException("The decoded Player Red frame has an unexpected tile count.");
                }

                frames.Add(new IndexedSpriteFrameDefinition(
                    frameIndex,
                    FireRedRomLayoutRev1.PlayerRedNormalWidth,
                    FireRedRomLayoutRev1.PlayerRedNormalHeight,
                    ExpandTilesToFrame(tiles)));
            }

            return frames;
        }

        private static byte[] ExpandTilesToFrame(IReadOnlyList<IndexedTileDefinition> tiles)
        {
            var width = FireRedRomLayoutRev1.PlayerRedNormalWidth;
            var height = FireRedRomLayoutRev1.PlayerRedNormalHeight;
            var pixels = new byte[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                var tileRow = y / IndexedTileDefinition.Height;
                var pixelY = y % IndexedTileDefinition.Height;
                for (var x = 0; x < width; x++)
                {
                    var tileColumn = x / IndexedTileDefinition.Width;
                    var pixelX = x % IndexedTileDefinition.Width;
                    var tileIndex = checked((tileRow * FireRedRomLayoutRev1.PlayerRedNormalTilesWide) + tileColumn);
                    pixels[checked((y * width) + x)] = tiles[tileIndex].Pixels[checked((pixelY * IndexedTileDefinition.Width) + pixelX)];
                }
            }

            return pixels;
        }

        private static List<Rgba32> DecodePalette(RomReader reader)
        {
            var palette = new List<Rgba32>(FireRedRomLayoutRev1.PlayerRedNormalPaletteColorCount);
            for (var colorIndex = 0; colorIndex < FireRedRomLayoutRev1.PlayerRedNormalPaletteColorCount; colorIndex++)
            {
                var offset = checked(FireRedRomLayoutRev1.PlayerRedNormalPalette + (colorIndex * FireRedRomLayoutRev1.GbaHalfwordSize));
                palette.Add(FireRedGraphicsDecoder.DecodeBgr555(reader.ReadUInt16(offset), colorIndex == 0 ? (byte)0 : (byte)255));
            }

            return palette;
        }

        private static SpriteDirection GetDirection(int tableIndex)
        {
            switch (tableIndex % FireRedRomLayoutRev1.PlayerRedNormalDirectionCount)
            {
                case 0: return SpriteDirection.South;
                case 1: return SpriteDirection.North;
                case 2: return SpriteDirection.West;
                case 3: return SpriteDirection.East;
                default: throw new InvalidOperationException("The verified Player Red animation direction table is invalid.");
            }
        }

        private static void ExpectPointer(RomReader reader, int pointerField, int expectedOffset, string description)
        {
            reader.EnsureRange(pointerField, FireRedRomLayoutRev1.GbaPointerSize, description + " pointer is outside ROM bounds.");
            var actual = reader.ConvertGbaPointer(reader.ReadUInt32(pointerField));
            if (actual != expectedOffset)
            {
                throw new RomReadException(description + " does not match the verified rev1 location.", pointerField, FireRedRomLayoutRev1.GbaPointerSize, reader.Length);
            }
        }

        private static void ExpectEqual(RomReader reader, ushort actual, ushort expected, string description, int offset)
        {
            if (actual != expected)
            {
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, FireRedRomLayoutRev1.GbaHalfwordSize, reader.Length);
            }
        }

        private static void ExpectEqual(RomReader reader, byte actual, byte expected, string description, int offset)
        {
            if (actual != expected)
            {
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 1, reader.Length);
            }
        }
    }
}
