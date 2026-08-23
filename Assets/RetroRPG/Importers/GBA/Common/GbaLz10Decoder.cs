using System;
using System.Collections.Generic;

namespace RetroRPG.Importers.GBA.Common
{
    public static class GbaLz10Decoder
    {
        public static byte[] Decode(RomReader reader, int offset, int maximumOutputLength)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (maximumOutputLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputLength));

            reader.EnsureRange(offset, 4, "LZ10 header is outside ROM bounds.");
            if (reader.ReadByte(offset) != 0x10)
            {
                throw new RomReadException("Expected an LZ10 stream marker (0x10).", offset, 1, reader.Length);
            }

            var declaredLength = reader.ReadByte(offset + 1) | (reader.ReadByte(offset + 2) << 8) | (reader.ReadByte(offset + 3) << 16);
            if (declaredLength > maximumOutputLength)
            {
                throw new RomReadException("LZ10 declared output exceeds the configured safety limit.", offset, declaredLength, maximumOutputLength);
            }

            var output = new List<byte>(declaredLength);
            var source = checked(offset + 4);
            while (output.Count < declaredLength)
            {
                reader.EnsureRange(source, 1, "LZ10 flag byte is outside ROM bounds.");
                var flags = reader.ReadByte(source++);
                for (var bit = 7; bit >= 0 && output.Count < declaredLength; bit--)
                {
                    if ((flags & (1 << bit)) == 0)
                    {
                        reader.EnsureRange(source, 1, "LZ10 literal is outside ROM bounds.");
                        output.Add(reader.ReadByte(source++));
                        continue;
                    }

                    reader.EnsureRange(source, 2, "LZ10 back-reference is outside ROM bounds.");
                    var first = reader.ReadByte(source++);
                    var second = reader.ReadByte(source++);
                    var length = (first >> 4) + 3;
                    var distance = (((first & 0x0F) << 8) | second) + 1;
                    if (distance > output.Count)
                    {
                        throw new RomReadException("LZ10 back-reference precedes the beginning of output.", source - 2, 2, reader.Length);
                    }

                    if (length > declaredLength - output.Count)
                    {
                        throw new RomReadException("LZ10 back-reference exceeds its declared output length.", source - 2, 2, reader.Length);
                    }

                    for (var copy = 0; copy < length; copy++)
                    {
                        output.Add(output[output.Count - distance]);
                    }
                }
            }

            return output.ToArray();
        }
    }
}
