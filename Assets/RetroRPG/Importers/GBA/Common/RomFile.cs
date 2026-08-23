using System;
using System.IO;
using System.Security.Cryptography;

namespace RetroRPG.Importers.GBA.Common
{
    [Serializable]
    public sealed class RomFingerprint
    {
        public RomFingerprint(long size, string sha1, string sha256)
        {
            Size = size;
            Sha1 = sha1 ?? throw new ArgumentNullException(nameof(sha1));
            Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
        }

        public long Size { get; }

        public string Sha1 { get; }

        public string Sha256 { get; }
    }

    public sealed class RomFile
    {
        private RomFile(string fileName, byte[] data, RomFingerprint fingerprint)
        {
            FileName = fileName;
            this.data = data;
            Fingerprint = fingerprint;
        }

        private readonly byte[] data;

        public string FileName { get; }

        public RomFingerprint Fingerprint { get; }

        public RomReader CreateReader()
        {
            return RomReader.FromOwnedBuffer(data);
        }

        public static RomFile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A ROM path is required.", nameof(path));
            }

            var data = File.ReadAllBytes(path);
            string sha1;
            string sha256;

            using (var algorithm = SHA1.Create())
            {
                sha1 = ToLowerHex(algorithm.ComputeHash(data));
            }

            using (var algorithm = SHA256.Create())
            {
                sha256 = ToLowerHex(algorithm.ComputeHash(data));
            }

            return new RomFile(
                Path.GetFileName(path),
                data,
                new RomFingerprint(data.LongLength, sha1, sha256));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
