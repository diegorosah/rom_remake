using System;
using System.IO;
using NUnit.Framework;
using RetroRPG.Importers.GBA.Common;

namespace RetroRPG.Tests.EditMode
{
    public sealed class RomFileTests
    {
        [Test]
        public void LoadedRomRemainsASnapshotWhenTheSourceFileChanges()
        {
            var path = Path.Combine(Path.GetTempPath(), "rrpg-rom-" + Guid.NewGuid().ToString("N") + ".gba");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x12, 0x34 });
                var rom = RomFile.Load(path);
                File.WriteAllBytes(path, new byte[] { 0xFE, 0xDC });

                Assert.That(rom.CreateReader().ReadByte(0), Is.EqualTo(0x12));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
