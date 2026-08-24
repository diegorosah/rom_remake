using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    internal static class FireRedObjectEventParser
    {
        private static readonly ObjectEventSpec[] Specs =
        {
            new ObjectEventSpec(FireRedRomLayoutRev1.PalletTownMapId, 0x3B4DF4, 0, 1, 23, 3, 10, 3, 0x02, 0x41, 0x081657D4, 0, false),
            new ObjectEventSpec(FireRedRomLayoutRev1.PalletTownMapId, 0x3B4DF4, 1, 2, 27, 13, 17, 3, 0x02, 0x26, 0x081658A7, 0, false),
            new ObjectEventSpec(FireRedRomLayoutRev1.PalletTownMapId, 0x3B4DF4, 2, 3, 71, 10, 8, 3, 0x07, 0x11, 0, 0x002C, false),
            new ObjectEventSpec(FireRedRomLayoutRev1.PlayersHouse1FMapId, 0x3B9778, 0, 1, 88, 8, 4, 3, 0x09, 0, 0x08168C81, 0, false),
            new ObjectEventSpec(FireRedRomLayoutRev1.RivalsHouseMapId, 0x3B9810, 0, 1, 76, 10, 6, 3, 0x02, 0x31, 0x08168DCE, 0, false),
            new ObjectEventSpec(FireRedRomLayoutRev1.RivalsHouseMapId, 0x3B9810, 1, 2, 93, 6, 4, 3, 0x08, 0x11, 0x08168FDB, 0x0039, true)
        };

        public static void Parse(RomReader reader, FireRedMapSpec map, IList<MapCellDefinition> cells, out List<NpcDefinition> npcs, out List<StaticMapPropDefinition> props)
        {
            if (reader == null || map == null || cells == null) throw new ArgumentNullException();
            npcs = new List<NpcDefinition>();
            props = new List<StaticMapPropDefinition>();

            var expected = SpecsFor(map.Id);
            var pointerField = checked(map.EventsOffset + FireRedRomLayoutRev1.MapEventsObjectPointerOffset);
            var rawPointer = reader.ReadUInt32(pointerField);

            if (map.ObjectEventCount == 0)
            {
                if (rawPointer != 0) reader.ConvertGbaPointer(rawPointer, 1);
                if (expected.Count != 0) throw new InvalidOperationException("The verified object-event whitelist does not match the map count.");
                return;
            }

            var arrayOffset = reader.ConvertGbaPointer(
                rawPointer,
                checked(map.ObjectEventCount * FireRedRomLayoutRev1.ObjectEventTemplateSize));

            if (expected.Count != map.ObjectEventCount || arrayOffset != expected[0].ArrayOffset)
            {
                throw new RomReadException(
                    "Object-event array does not match the verified MVP 4 layout.",
                    pointerField,
                    4,
                    reader.Length);
            }

            var localIds = new HashSet<int>();

            for (var i = 0; i < expected.Count; i++)
            {
                var spec = expected[i];
                var offset = checked(arrayOffset + (spec.ArrayIndex * FireRedRomLayoutRev1.ObjectEventTemplateSize));

                ValidateTemplate(reader, offset, spec, map, cells, localIds);

                var eventId = map.Id + ":object:" + spec.LocalId;
                var interactionKey = spec.ScriptPointer == 0
                    ? "none"
                    : "script:" + spec.ScriptPointer.ToString("X8");
                var visibilityKey = "flag:" + spec.VisibilityFlag.ToString("X4");
                var visibleByDefault = spec.VisibilityFlag != 0x002C;

                if (spec.IsStatic)
                {
                    props.Add(new StaticMapPropDefinition(
                        eventId,
                        spec.LocalId,
                        SpriteId(spec.GraphicsId),
                        spec.X,
                        spec.Y,
                        spec.Elevation,
                        InitialDirection(spec.Movement),
                        interactionKey,
                        visibilityKey,
                        visibleByDefault));
                }
                else
                {
                    var pattern = spec.Movement == 0x02
                        ? NpcMovementPattern.WanderCardinal
                        : NpcMovementPattern.FixedFacing;

                    var minX = spec.X;
                    var maxX = spec.X;
                    var minY = spec.Y;
                    var maxY = spec.Y;

                    if (pattern == NpcMovementPattern.WanderCardinal)
                    {
                        var rangeX = spec.Ranges & 0x0F;
                        var rangeY = (spec.Ranges >> 4) & 0x0F;
                        minX = spec.X - rangeX;
                        maxX = spec.X + rangeX;
                        minY = spec.Y - rangeY;
                        maxY = spec.Y + rangeY;
                    }

                    npcs.Add(new NpcDefinition(
                        eventId,
                        spec.LocalId,
                        SpriteId(spec.GraphicsId),
                        spec.X,
                        spec.Y,
                        spec.Elevation,
                        InitialDirection(spec.Movement),
                        pattern,
                        minX,
                        maxX,
                        minY,
                        maxY,
                        interactionKey,
                        visibilityKey,
                        visibleByDefault));
                }
            }
        }

        private static void ValidateTemplate(
            RomReader reader,
            int offset,
            ObjectEventSpec spec,
            FireRedMapSpec map,
            IList<MapCellDefinition> cells,
            ISet<int> localIds)
        {
            reader.EnsureRange(
                offset,
                FireRedRomLayoutRev1.ObjectEventTemplateSize,
                "Object-event template is outside ROM bounds.");

            Expect(reader, reader.ReadByte(offset), spec.LocalId, "Object-event local id", offset);
            Expect(reader, reader.ReadByte(offset + 1), spec.GraphicsId, "Object-event graphics id", offset + 1);
            Expect(reader, reader.ReadByte(offset + 2), 0, "Object-event kind", offset + 2);
            Expect(reader, reader.ReadByte(offset + 3), 0, "Object-event reserved byte", offset + 3);
            Expect(reader, ReadInt16(reader, offset + 4), spec.X, "Object-event x", offset + 4);
            Expect(reader, ReadInt16(reader, offset + 6), spec.Y, "Object-event y", offset + 6);
            Expect(reader, reader.ReadByte(offset + 8), spec.Elevation, "Object-event elevation", offset + 8);
            Expect(reader, reader.ReadByte(offset + 9), spec.Movement, "Object-event movement", offset + 9);
            Expect(reader, reader.ReadByte(offset + 10), spec.Ranges, "Object-event movement range", offset + 10);
            Expect(reader, reader.ReadByte(offset + 11), 0, "Object-event trainer type", offset + 11);
            Expect(reader, reader.ReadUInt16(offset + 12), 0, "Object-event trainer range", offset + 12);
            Expect(reader, reader.ReadUInt32(offset + 16), spec.ScriptPointer, "Object-event script identity", offset + 16);
            Expect(reader, reader.ReadUInt16(offset + 20), spec.VisibilityFlag, "Object-event visibility flag", offset + 20);

            if (!localIds.Add(spec.LocalId) ||
                spec.X < 0 ||
                spec.Y < 0 ||
                spec.X >= map.Width ||
                spec.Y >= map.Height)
            {
                throw new RomReadException(
                    "Object-event local id or coordinates are invalid.",
                    offset,
                    FireRedRomLayoutRev1.ObjectEventTemplateSize,
                    reader.Length);
            }

            // Do not require object-event elevation/collision to match the underlying
            // metatile cell. FireRed object events carry their own elevation/occupancy
            // semantics, and the Unity runtime applies occupancy independently through
            // MapCellOccupancy. The ROM template fields themselves were already checked
            // above against the audited whitelist.

            if (spec.GraphicsId >= FireRedRomLayoutRev1.ObjectEventGraphicsInfoCount)
            {
                throw new RomReadException(
                    "Object-event graphics id exceeds the graphics-info table.",
                    offset + 1,
                    1,
                    reader.Length);
            }
        }

        private static List<ObjectEventSpec> SpecsFor(string mapId)
        {
            var list = new List<ObjectEventSpec>();
            for (var i = 0; i < Specs.Length; i++)
            {
                if (Specs[i].MapId == mapId) list.Add(Specs[i]);
            }
            return list;
        }

        private static string SpriteId(int id)
        {
            switch (id)
            {
                case 23: return "object_woman1";
                case 27: return "object_fat_man";
                case 71: return "object_prof_oak";
                case 76: return "object_daisy";
                case 88: return "object_mom";
                case 93: return "prop_town_map";
                default: throw new InvalidOperationException("Object graphics id is not whitelisted.");
            }
        }

        private static SpriteDirection InitialDirection(int movement)
        {
            switch (movement)
            {
                case 0x02:
                case 0x08:
                    return SpriteDirection.South;
                case 0x07:
                    return SpriteDirection.North;
                case 0x09:
                    return SpriteDirection.West;
                default:
                    throw new InvalidOperationException("Object movement is not whitelisted.");
            }
        }

        private static short ReadInt16(RomReader reader, int offset)
        {
            return unchecked((short)reader.ReadUInt16(offset));
        }

        private static void Expect(RomReader reader, byte actual, int expected, string description, int offset)
        {
            if (actual != expected)
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 1, reader.Length);
        }

        private static void Expect(RomReader reader, short actual, int expected, string description, int offset)
        {
            if (actual != expected)
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 2, reader.Length);
        }

        private static void Expect(RomReader reader, ushort actual, int expected, string description, int offset)
        {
            if (actual != expected)
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 2, reader.Length);
        }

        private static void Expect(RomReader reader, uint actual, uint expected, string description, int offset)
        {
            if (actual != expected)
                throw new RomReadException(description + " does not match the verified rev1 layout.", offset, 4, reader.Length);
        }

        private sealed class ObjectEventSpec
        {
            public ObjectEventSpec(
                string mapId,
                int arrayOffset,
                int arrayIndex,
                int localId,
                int graphicsId,
                int x,
                int y,
                int elevation,
                int movement,
                int ranges,
                uint scriptPointer,
                int visibilityFlag,
                bool isStatic)
            {
                MapId = mapId;
                ArrayOffset = arrayOffset;
                ArrayIndex = arrayIndex;
                LocalId = localId;
                GraphicsId = graphicsId;
                X = x;
                Y = y;
                Elevation = elevation;
                Movement = movement;
                Ranges = ranges;
                ScriptPointer = scriptPointer;
                VisibilityFlag = visibilityFlag;
                IsStatic = isStatic;
            }

            public string MapId { get; }
            public int ArrayOffset { get; }
            public int ArrayIndex { get; }
            public int LocalId { get; }
            public int GraphicsId { get; }
            public int X { get; }
            public int Y { get; }
            public int Elevation { get; }
            public int Movement { get; }
            public int Ranges { get; }
            public uint ScriptPointer { get; }
            public int VisibilityFlag { get; }
            public bool IsStatic { get; }
        }
    }
}