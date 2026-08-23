using System;

namespace RetroRPG.Importers.GBA.Common
{
    public sealed class RomReader
    {
        public const uint GbaRomAddressBase = 0x08000000;
        public const uint GbaRomAddressEndExclusive = 0x0A000000;

        private readonly byte[] data;

        public RomReader(byte[] data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int Length => data.Length;

        public byte ReadByte(long offset)
        {
            EnsureRange(offset, 1, "Byte read is outside ROM bounds.");
            return data[(int)offset];
        }

        public ushort ReadUInt16(long offset)
        {
            EnsureRange(offset, 2, "UInt16 read is outside ROM bounds.");
            var index = (int)offset;
            return (ushort)(data[index] | (data[index + 1] << 8));
        }

        public uint ReadUInt32(long offset)
        {
            EnsureRange(offset, 4, "UInt32 read is outside ROM bounds.");
            var index = (int)offset;
            return (uint)(data[index]
                | (data[index + 1] << 8)
                | (data[index + 2] << 16)
                | (data[index + 3] << 24));
        }

        public byte[] ReadBytes(long offset, int length)
        {
            EnsureRange(offset, length, "Byte range is outside ROM bounds.");
            var result = new byte[length];
            Buffer.BlockCopy(data, (int)offset, result, 0, length);
            return result;
        }

        public int ConvertGbaPointer(uint pointer, int requiredLength = 1)
        {
            if (pointer < GbaRomAddressBase || pointer >= GbaRomAddressEndExclusive)
            {
                throw new RomReadException(
                    $"Pointer 0x{pointer:X8} is not in the GBA ROM address window.",
                    pointer,
                    requiredLength,
                    data.Length);
            }

            var offset = (long)pointer - GbaRomAddressBase;
            EnsureRange(offset, requiredLength, $"Pointer 0x{pointer:X8} resolves outside the ROM.");
            return (int)offset;
        }

        public void EnsureRange(long offset, long length, string message)
        {
            if (offset < 0 || length < 0 || offset > data.LongLength || length > data.LongLength - offset)
            {
                throw new RomReadException(message, offset, length, data.LongLength);
            }
        }
    }
}

