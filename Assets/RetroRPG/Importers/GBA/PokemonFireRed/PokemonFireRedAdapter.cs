using RetroRPG.Importers.GBA.Common;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    public sealed class PokemonFireRedAdapter : IRomGameAdapter
    {
        public const string AdapterId = "pokemon-firered-gba";
        public const string SupportedSha1 = "dd5945db9b930750cb39d00c84da8571feebf417";
        public const long SupportedSize = 16 * 1024 * 1024;

        public string Id => AdapterId;

        public GameDetectionResult Detect(GbaHeader header, RomFingerprint fingerprint)
        {
            if (header.GameCode != "BPRE" || header.MakerCode != "01")
            {
                return new GameDetectionResult(
                    GameDetectionStatus.Unknown,
                    AdapterId,
                    "Header does not identify Pokemon FireRed USA.");
            }

            if (!header.HasValidFixedValue || !header.HasValidComplementCheck)
            {
                return new GameDetectionResult(
                    GameDetectionStatus.RecognizedButUnsupported,
                    AdapterId,
                    "FireRed-like header is invalid; import is disabled.");
            }

            if (header.SoftwareVersion == 1
                && fingerprint.Size == SupportedSize
                && fingerprint.Sha1 == SupportedSha1)
            {
                return new GameDetectionResult(
                    GameDetectionStatus.Supported,
                    AdapterId,
                    "Supported Pokemon FireRed USA revision 1 ROM.",
                    new GameDescriptor(
                        "pokemon-firered-usa-rev1",
                        "Pokemon FireRed",
                        PlatformId.Gba,
                        "USA rev1"));
            }

            return new GameDetectionResult(
                GameDetectionStatus.RecognizedButUnsupported,
                AdapterId,
                $"Pokemon FireRed was recognized, but revision {header.SoftwareVersion} / SHA-1 {fingerprint.Sha1} is not supported.");
        }
    }
}

