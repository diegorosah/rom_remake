using System;

namespace RetroRPG.Importers.GBA.Common
{
    public sealed class RomReadException : Exception
    {
        public RomReadException(string message, long offset, long requestedLength, long romLength)
            : base($"{message} Offset=0x{offset:X}, requested={requestedLength}, ROM length={romLength}.")
        {
            Offset = offset;
            RequestedLength = requestedLength;
            RomLength = romLength;
        }

        public long Offset { get; }

        public long RequestedLength { get; }

        public long RomLength { get; }
    }
}

