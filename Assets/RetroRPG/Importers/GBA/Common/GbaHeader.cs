using System;

namespace RetroRPG.Importers.GBA.Common
{
    [Serializable]
    public sealed class GbaHeader
    {
        public GbaHeader(
            string title,
            string gameCode,
            string makerCode,
            byte fixedValue,
            byte unitCode,
            byte softwareVersion,
            byte complementCheck,
            byte calculatedComplementCheck)
        {
            Title = title;
            GameCode = gameCode;
            MakerCode = makerCode;
            FixedValue = fixedValue;
            UnitCode = unitCode;
            SoftwareVersion = softwareVersion;
            ComplementCheck = complementCheck;
            CalculatedComplementCheck = calculatedComplementCheck;
        }

        public string Title { get; }
        public string GameCode { get; }
        public string MakerCode { get; }
        public byte FixedValue { get; }
        public byte UnitCode { get; }
        public byte SoftwareVersion { get; }
        public byte ComplementCheck { get; }
        public byte CalculatedComplementCheck { get; }
        public bool HasValidFixedValue => FixedValue == 0x96;
        public bool HasValidComplementCheck => ComplementCheck == CalculatedComplementCheck;
    }
}

