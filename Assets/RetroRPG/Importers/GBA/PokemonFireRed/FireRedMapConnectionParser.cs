using System;
using System.Collections.Generic;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Bounds-safe decoder for FireRed cardinal map-edge connections.</summary>
    internal static class FireRedMapConnectionParser
    {
        private const int MapConnectionsSize = 8;
        private const int MapConnectionSize = 12;
        private const int MaximumConnectionCount = 32;

        private const int DirectionOffset = 0;
        private const int OffsetOffset = 4;
        private const int MapGroupOffset = 8;
        private const int MapNumberOffset = 9;

        public static List<MapConnectionDefinition> Parse(
            RomReader reader,
            FireRedDiscoveredMapSpec spec,
            FireRedMapCatalogScanResult discovery)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (discovery == null) throw new ArgumentNullException(nameof(discovery));

            var result = new List<MapConnectionDefinition>();
            if (spec.ConnectionsOffset == 0) return result;

            reader.EnsureRange(spec.ConnectionsOffset, MapConnectionsSize, spec.Name + " map connections are outside ROM bounds.");
            var count = unchecked((int)reader.ReadUInt32(spec.ConnectionsOffset));
            if (count < 0 || count > MaximumConnectionCount)
            {
                throw new RomReadException(
                    spec.Name + " map connection count exceeds the configured safety bound.",
                    spec.ConnectionsOffset,
                    MapConnectionsSize,
                    reader.Length);
            }

            if (count == 0) return result;

            var pointerField = checked(spec.ConnectionsOffset + 4);
            var connectionOffset = reader.ConvertGbaPointer(
                reader.ReadUInt32(pointerField),
                checked(count * MapConnectionSize));

            for (var index = 0; index < count; index++)
            {
                var offset = checked(connectionOffset + (index * MapConnectionSize));
                var rawDirection = reader.ReadByte(checked(offset + DirectionOffset));

                // Dive/emerge connections are not cardinal overworld edges and are
                // intentionally left to later surf/dive gameplay support.
                if (rawDirection < 1 || rawDirection > 4) continue;

                var connectionDelta = unchecked((int)reader.ReadUInt32(checked(offset + OffsetOffset)));
                var destinationGroup = reader.ReadByte(checked(offset + MapGroupOffset));
                var destinationNumber = reader.ReadByte(checked(offset + MapNumberOffset));
                var destinationId = FireRedMapCatalogScanner.MapId(destinationGroup, destinationNumber);
                if (!discovery.TryGetSpec(destinationId, out _))
                {
                    continue;
                }

                result.Add(new MapConnectionDefinition(
                    (MapConnectionDirection)rawDirection,
                    connectionDelta,
                    destinationId));
            }

            return result;
        }
    }
}
