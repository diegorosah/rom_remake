using System;
using System.Collections.Generic;
using RetroRPG.Core;

namespace RetroRPG.Importers.GBA.Common
{
    public enum PlatformId
    {
        Unknown,
        Gba
    }

    public enum GameDetectionStatus
    {
        Unknown,
        RecognizedButUnsupported,
        Supported
    }

    [Serializable]
    public sealed class GameDescriptor
    {
        public GameDescriptor(string id, string title, PlatformId platform, string revision)
        {
            Id = id;
            Title = title;
            Platform = platform;
            Revision = revision;
        }

        public string Id { get; }
        public string Title { get; }
        public PlatformId Platform { get; }
        public string Revision { get; }
    }

    public sealed class GameDetectionResult
    {
        public GameDetectionResult(
            GameDetectionStatus status,
            string adapterId,
            string message,
            GameDescriptor game = null)
        {
            Status = status;
            AdapterId = adapterId;
            Message = message;
            Game = game;
        }

        public GameDetectionStatus Status { get; }
        public string AdapterId { get; }
        public string Message { get; }
        public GameDescriptor Game { get; }
        public bool CanImport => Status == GameDetectionStatus.Supported;
    }

    public interface IRomGameAdapter
    {
        string Id { get; }
        GameDetectionResult Detect(GbaHeader header, RomFingerprint fingerprint);
    }

    public sealed class GameDetector
    {
        private readonly IReadOnlyList<IRomGameAdapter> adapters;

        public GameDetector(IReadOnlyList<IRomGameAdapter> adapters)
        {
            this.adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
        }

        public GameDetectionResult Detect(GbaHeader header, RomFingerprint fingerprint)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (fingerprint == null) throw new ArgumentNullException(nameof(fingerprint));

            GameDetectionResult recognized = null;
            for (var i = 0; i < adapters.Count; i++)
            {
                var result = adapters[i].Detect(header, fingerprint);
                if (result.Status == GameDetectionStatus.Supported)
                {
                    return result;
                }

                if (result.Status == GameDetectionStatus.RecognizedButUnsupported)
                {
                    recognized = result;
                }
            }

            return recognized ?? new GameDetectionResult(
                GameDetectionStatus.Unknown,
                string.Empty,
                "No installed adapter recognizes this ROM.");
        }
    }
}

