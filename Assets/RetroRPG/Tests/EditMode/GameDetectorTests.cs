using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;

namespace RetroRPG.Tests.EditMode
{
    public sealed class GameDetectorTests
    {
        [Test]
        public void AcceptsOnlyExactFireRedRevisionOneFingerprint()
        {
            var header = GbaHeaderParser.Parse(new RomReader(
                GbaHeaderParserTests.BuildHeader("POKEMON FIRE", "BPRE", "01", 1)));
            var detector = CreateDetector();

            var supported = detector.Detect(header, new RomFingerprint(
                PokemonFireRedAdapter.SupportedSize,
                PokemonFireRedAdapter.SupportedSha1,
                "diagnostic"));
            var modified = detector.Detect(header, new RomFingerprint(
                PokemonFireRedAdapter.SupportedSize,
                new string('0', 40),
                "diagnostic"));

            Assert.That(supported.Status, Is.EqualTo(GameDetectionStatus.Supported));
            Assert.That(supported.CanImport, Is.True);
            Assert.That(modified.Status, Is.EqualTo(GameDetectionStatus.RecognizedButUnsupported));
            Assert.That(modified.CanImport, Is.False);
        }

        [Test]
        public void RecognizesButRejectsRevisionZero()
        {
            var header = GbaHeaderParser.Parse(new RomReader(
                GbaHeaderParserTests.BuildHeader("POKEMON FIRE", "BPRE", "01", 0)));

            var result = CreateDetector().Detect(header, new RomFingerprint(
                PokemonFireRedAdapter.SupportedSize,
                "41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc",
                "diagnostic"));

            Assert.That(result.Status, Is.EqualTo(GameDetectionStatus.RecognizedButUnsupported));
        }

        private static GameDetector CreateDetector()
        {
            return new GameDetector(new List<IRomGameAdapter> { new PokemonFireRedAdapter() });
        }
    }
}

