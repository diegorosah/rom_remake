using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Whitelisted decoder for the six audited MVP 4 object graphics only.</summary>
    internal static class ObjectEventSpriteDecoder
    {
        private static readonly ObjectSpriteSpec[] Specs =
        {
            new ObjectSpriteSpec(23, "object_woman1", 0x3A3DD0, 0x3A06F8, 0x370418, 10, 0xA00, 0x1105, 16, 32, true),
            new ObjectSpriteSpec(27, "object_fat_man", 0x3A3E84, 0x3A0830, 0x373418, 9, 0x900, 0x1106, 16, 32, true),
            new ObjectSpriteSpec(71, "object_prof_oak", 0x3A43DC, 0x3A1408, 0x389B98, 9, 0x900, 0x1106, 16, 32, true),
            new ObjectSpriteSpec(76, "object_daisy", 0x3A4838, 0x3A1A90, 0x36B198, 9, 0x900, 0x1105, 16, 32, true),
            new ObjectSpriteSpec(88, "object_mom", 0x3A515C, 0x3A2978, 0x391B98, 9, 0x300, 0x1103, 16, 32, true, new[] { 0x391B98, 0x391C98, 0x391D98, 0x391B98, 0x391B98, 0x391C98, 0x391C98, 0x391D98, 0x391D98 }),
            new ObjectSpriteSpec(93, "prop_town_map", 0x3A49A0, 0x3A1C70, 0x369E98, 1, 0x100, 0x1103, 32, 16, false)
        };

        public static ObjectSpriteCatalogDefinition Decode(RomReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            var mobile = new List<OverworldSpriteDefinition>();
            var statics = new List<StaticSpriteDefinition>();
            for (var i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                var decoded = DecodeSprite(reader, spec);
                if (spec.IsMobile) mobile.Add(CreateMobile(reader, spec, decoded.Palette, decoded.Frames));
                else statics.Add(new StaticSpriteDefinition(spec.Id, spec.Width, spec.Height, decoded.Palette, decoded.Frames));
            }

            return new ObjectSpriteCatalogDefinition(mobile, statics);
        }

        private static DecodedSprite DecodeSprite(RomReader reader, ObjectSpriteSpec spec)
        {
            ValidateGraphicsInfo(reader, spec);
            var palette = DecodePalette(reader, spec.PaletteTag);
            var frames = DecodeFrames(reader, spec);
            return new DecodedSprite(palette, frames);
        }

        private static void ValidateGraphicsInfo(RomReader reader, ObjectSpriteSpec spec)
        {
            if (spec.GraphicsId < 0 || spec.GraphicsId >= FireRedRomLayoutRev1.ObjectEventGraphicsInfoCount) throw new InvalidOperationException("Object graphics id is outside the verified graphics-info table.");
            var entry = checked(FireRedRomLayoutRev1.ObjectEventGraphicsInfoPointerTable + (spec.GraphicsId * FireRedRomLayoutRev1.GbaPointerSize));
            ExpectPointer(reader, entry, spec.InfoOffset, spec.Id + " graphics-info entry");
            reader.EnsureRange(spec.InfoOffset, FireRedRomLayoutRev1.ObjectEventGraphicsInfoSize, spec.Id + " graphics-info is outside ROM bounds.");
            ExpectEqual(reader, reader.ReadUInt16(checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteTagOffset)), spec.PaletteTag, spec.Id + " palette tag", checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoPaletteTagOffset));
            ExpectEqual(reader, reader.ReadUInt16(checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAllocationSizeOffset)), 0x100, spec.Id + " allocation size", checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAllocationSizeOffset));
            ExpectEqual(reader, reader.ReadUInt16(checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoWidthOffset)), (ushort)spec.Width, spec.Id + " width", checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoWidthOffset));
            ExpectEqual(reader, reader.ReadUInt16(checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoHeightOffset)), (ushort)spec.Height, spec.Id + " height", checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoHeightOffset));
            ExpectPointer(reader, checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoAnimationsOffset), spec.IsMobile ? FireRedRomLayoutRev1.StandardObjectAnimationTable : FireRedRomLayoutRev1.InanimateObjectAnimationTable, spec.Id + " animation table");
            ExpectPointer(reader, checked(spec.InfoOffset + FireRedRomLayoutRev1.ObjectEventGraphicsInfoImagesOffset), spec.ImageTableOffset, spec.Id + " image table");
            if (!spec.IsMobile)
            {
                ExpectPointer(reader, FireRedRomLayoutRev1.InanimateObjectAnimationTable, FireRedRomLayoutRev1.InanimateObjectAnimationScript, spec.Id + " inanimate animation script");
            }
        }

        private static List<Rgba32> DecodePalette(RomReader reader, ushort tag)
        {
            var entry = PaletteEntry(tag, out var data);
            reader.EnsureRange(entry, FireRedRomLayoutRev1.SpritePaletteSize, "Object sprite palette entry is outside ROM bounds.");
            ExpectPointer(reader, entry, data, "Object sprite palette data");
            ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.SpritePaletteTagOffset)), tag, "Object sprite palette tag", checked(entry + FireRedRomLayoutRev1.SpritePaletteTagOffset));
            reader.EnsureRange(data, OverworldSpriteDefinition.PaletteColorCount * FireRedRomLayoutRev1.GbaHalfwordSize, "Object sprite palette is outside ROM bounds.");
            var palette = new List<Rgba32>(OverworldSpriteDefinition.PaletteColorCount);
            for (var color = 0; color < OverworldSpriteDefinition.PaletteColorCount; color++) palette.Add(FireRedGraphicsDecoder.DecodeBgr555(reader.ReadUInt16(checked(data + (color * 2))), color == 0 ? (byte)0 : (byte)255));
            return palette;
        }

        private static List<IndexedSpriteFrameDefinition> DecodeFrames(RomReader reader, ObjectSpriteSpec spec)
        {
            reader.EnsureRange(spec.ImageTableOffset, checked(spec.LogicalFrameCount * FireRedRomLayoutRev1.SpriteFrameImageSize), spec.Id + " image table is outside ROM bounds.");
            reader.EnsureRange(spec.GraphicsOffset, spec.GraphicsByteCount, spec.Id + " graphics range is outside ROM bounds.");
            var byteSize = checked((spec.Width / IndexedTileDefinition.Width) * (spec.Height / IndexedTileDefinition.Height) * FireRedGraphicsDecoder.BytesPer4BppTile);
            if (byteSize != 0x100) throw new InvalidOperationException("The audited object sprite frame dimensions must occupy 0x100 bytes.");
            var frames = new List<IndexedSpriteFrameDefinition>(spec.LogicalFrameCount);
            for (var index = 0; index < spec.LogicalFrameCount; index++)
            {
                var entry = checked(spec.ImageTableOffset + (index * FireRedRomLayoutRev1.SpriteFrameImageSize));
                var expected = spec.FrameOffsets == null ? checked(spec.GraphicsOffset + (index * byteSize)) : spec.FrameOffsets[index];
                ExpectPointer(reader, entry, expected, spec.Id + " frame data");
                ExpectEqual(reader, reader.ReadUInt16(checked(entry + FireRedRomLayoutRev1.SpriteFrameImageByteSizeOffset)), (ushort)byteSize, spec.Id + " frame byte size", checked(entry + FireRedRomLayoutRev1.SpriteFrameImageByteSizeOffset));
                reader.EnsureRange(expected, byteSize, spec.Id + " frame is outside ROM bounds.");
                frames.Add(new IndexedSpriteFrameDefinition(index, spec.Width, spec.Height, Expand(reader.ReadBytes(expected, byteSize), spec.Width, spec.Height)));
            }

            return frames;
        }

        private static OverworldSpriteDefinition CreateMobile(RomReader reader, ObjectSpriteSpec spec, IList<Rgba32> palette, IList<IndexedSpriteFrameDefinition> frames)
        {
            ValidateStandardAnimations(reader, spec, frames.Count);
            var animations = new List<DirectionalSpriteAnimationDefinition>(FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts.Count);
            for (var i = 0; i < FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts.Count; i++)
            {
                var script = FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts[i];
                var steps = new List<SpriteAnimationStepDefinition>(script.FrameIndices.Count);
                for (var frame = 0; frame < script.FrameIndices.Count; frame++) steps.Add(new SpriteAnimationStepDefinition(script.FrameIndices[frame], script.HorizontalFlip, script.VerticalFlip, script.DurationTicks));
                animations.Add(new DirectionalSpriteAnimationDefinition(Direction(i), i < 4 ? SpriteAnimationState.Idle : SpriteAnimationState.Walking, steps));
            }

            return new OverworldSpriteDefinition(spec.Id, spec.Width, spec.Height, palette, frames, animations);
        }

        private static void ValidateStandardAnimations(RomReader reader, ObjectSpriteSpec spec, int frameCount)
        {
            if (frameCount < 9) throw new InvalidOperationException("Audited humanoid sprites require the standard nine-frame animation set.");
            reader.EnsureRange(FireRedRomLayoutRev1.StandardObjectAnimationTable, FireRedRomLayoutRev1.SpriteAnimationPointerCount * 4, "Standard object animation table is outside ROM bounds.");
            for (var index = 0; index < FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts.Count; index++)
            {
                var script = FireRedRomLayoutRev1.PlayerRedNormalAnimationScripts[index];
                ExpectPointer(reader, checked(FireRedRomLayoutRev1.StandardObjectAnimationTable + (index * 4)), script.Offset, "Standard object animation pointer");
                for (var command = 0; command < script.FrameIndices.Count; command++)
                {
                    var offset = checked(script.Offset + (command * 4));
                    var flags = checked((ushort)(script.DurationTicks | (script.HorizontalFlip ? FireRedRomLayoutRev1.SpriteAnimationHorizontalFlipMask : 0) | (script.VerticalFlip ? FireRedRomLayoutRev1.SpriteAnimationVerticalFlipMask : 0)));
                    ExpectEqual(reader, reader.ReadUInt16(offset), checked((ushort)script.FrameIndices[command]), "Standard object animation frame", offset);
                    ExpectEqual(reader, reader.ReadUInt16(checked(offset + 2)), flags, "Standard object animation flags", checked(offset + 2));
                }

                var jump = checked(script.Offset + (script.FrameIndices.Count * 4));
                ExpectEqual(reader, reader.ReadUInt16(jump), FireRedRomLayoutRev1.SpriteAnimationJumpOpcode, "Standard object animation loop opcode", jump);
                ExpectEqual(reader, reader.ReadUInt16(checked(jump + 2)), FireRedRomLayoutRev1.SpriteAnimationJumpTargetZero, "Standard object animation loop target", checked(jump + 2));
            }
        }

        private static byte[] Expand(byte[] source, int width, int height)
        {
            var tiles = FireRedGraphicsDecoder.Decode4BppTiles(source, 0); var pixels = new byte[checked(width * height)]; var tileWidth = width / 8;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) pixels[(y * width) + x] = tiles[((y / 8) * tileWidth) + (x / 8)].Pixels[((y % 8) * 8) + (x % 8)];
            return pixels;
        }

        private static int PaletteEntry(ushort tag, out int data)
        {
            switch (tag)
            {
                case 0x1103: data = FireRedRomLayoutRev1.ObjectPalette1103Data; return FireRedRomLayoutRev1.ObjectPalette1103Entry;
                case 0x1105: data = FireRedRomLayoutRev1.ObjectPalette1105Data; return FireRedRomLayoutRev1.ObjectPalette1105Entry;
                case 0x1106: data = FireRedRomLayoutRev1.ObjectPalette1106Data; return FireRedRomLayoutRev1.ObjectPalette1106Entry;
                default: throw new InvalidOperationException("Object sprite palette is not in the audited MVP 4 whitelist.");
            }
        }

        private static SpriteDirection Direction(int index) { switch (index % 4) { case 0: return SpriteDirection.South; case 1: return SpriteDirection.North; case 2: return SpriteDirection.West; default: return SpriteDirection.East; } }
        private static void ExpectPointer(RomReader reader, int field, int expected, string description) { var actual = reader.ConvertGbaPointer(reader.ReadUInt32(field)); if (actual != expected) throw new RomReadException(description + " does not match the verified rev1 location.", field, 4, reader.Length); }
        private static void ExpectEqual(RomReader reader, ushort actual, ushort expected, string description, int offset) { if (actual != expected) throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 2, reader.Length); }

        private sealed class DecodedSprite { public DecodedSprite(List<Rgba32> palette, List<IndexedSpriteFrameDefinition> frames) { Palette = palette; Frames = frames; } public List<Rgba32> Palette { get; } public List<IndexedSpriteFrameDefinition> Frames { get; } }
        private sealed class ObjectSpriteSpec
        {
            public ObjectSpriteSpec(int graphicsId, string id, int infoOffset, int imageTableOffset, int graphicsOffset, int logicalFrameCount, int graphicsByteCount, ushort paletteTag, int width, int height, bool isMobile, int[] frameOffsets = null) { GraphicsId = graphicsId; Id = id; InfoOffset = infoOffset; ImageTableOffset = imageTableOffset; GraphicsOffset = graphicsOffset; LogicalFrameCount = logicalFrameCount; GraphicsByteCount = graphicsByteCount; PaletteTag = paletteTag; Width = width; Height = height; IsMobile = isMobile; FrameOffsets = frameOffsets; }
            public int GraphicsId { get; } public string Id { get; } public int InfoOffset { get; } public int ImageTableOffset { get; } public int GraphicsOffset { get; } public int LogicalFrameCount { get; } public int GraphicsByteCount { get; } public ushort PaletteTag { get; } public int Width { get; } public int Height { get; } public bool IsMobile { get; } public int[] FrameOffsets { get; }
        }
    }
}
