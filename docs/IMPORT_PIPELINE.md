# Import pipeline

1. Read and fingerprint a user-selected local file.
2. Parse and validate the GBA header.
3. Select an exact game/revision adapter.
4. Parse all requested content into IR without changing Unity assets.
5. Validate IR invariants.
6. Generate or update stable local assets and a manifest.
7. Open the generated preview scene and report warnings/errors.

Any failure before step 6 leaves existing generated assets unchanged.

## Current Pallet Town output

After exact revision detection, **Tools > Retro RPG > ROM Inspector** enables
**Import Pallet Town**. The command parses and validates the complete IR first, shows
cancelable progress and diagnostics, then updates this ignored tree:

```text
Assets/Imported/FireRed/rev1/PalletTown/
  PalletTown.ir.json
  ImportReport.json
  ImportManifest.json
  PalletTown.unity
  Player/*.png
  Textures/*.png
  Tiles/*.asset
```

Textures are point-filtered, uncompressed and mip-free. Stable names include tile,
palette and both flip bits; animated variants include a stable frame suffix. The scene
uses a Grid with 0.5 x 0.5 cells, PPU 16, an orthographic Pixel Perfect camera at
240x160, deterministic inverted Y, and sorting orders Bottom=0, Middle=1, Player=2,
Top=3. The Player folder contains stable point-filtered, uncompressed, mip-free 16x32
sprites generated from the player-sprite IR. The scene contains a bottom-up
`GridCollisionMap`, a grid-stepped `PlayerController` (4 cells/second by default), a
directional idle/walking animator, and a pixel-quantized camera follow component.

`ImportManifest.json` (schema 1) owns the sorted list of generated paths. On reimport,
only stale paths previously listed by that manifest and still under the output root are
removed. Stable paths preserve Unity GUIDs. The ROM filename, size and hashes may appear
in diagnostics, but its absolute path and raw bytes do not enter generated assets.

The Inspector displays filename, size, title/game/maker/version/fixed-byte/checksum,
SHA-1/SHA-256 and detection diagnostics. Import remains disabled for malformed,
unknown, altered, or unsupported revisions.

## Validation commands

Do not combine `-quit` with `-runTests`; the Test Framework controls test exit:

```powershell
$env:RETRO_RPG_TEST_ROM='D:\rom_remake\POKEMON_FIRERED_ROM\Pokemon_FireRed.gba'
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\rom_remake' -runTests -testPlatform EditMode -testResults 'D:\rom_remake\TestResults\editmode-full.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\rom_remake' -runTests -testPlatform PlayMode -testResults 'D:\rom_remake\TestResults\playmode-smoke.xml'
```

Recorded gates: compile exit 0; EditMode 48 total with 46 passed, 2 explicit skipped,
0 failed; explicit parser 1/1; explicit double generation 1/1 with equal content
hashes and GUIDs; and PlayMode smoke `mvp2-playmode-4` 1/1, including movement and
collision checks. Test XML is outside tracked output and is
ignored by Git.
