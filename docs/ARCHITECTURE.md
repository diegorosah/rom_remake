# Architecture

The pipeline is `user ROM -> game adapter -> RetroRPG IR -> Unity Editor builders -> local
generated assets`. Runtime code consumes generated assets and never reads the ROM.

## Assembly boundaries

- `RetroRPG.Core`: generic diagnostics and future game-domain contracts; no Unity.
- `RetroRPG.IR`: game-agnostic import representation; depends only on Core.
- `RetroRPG.Importers.GBA.Common`: safe ROM/header primitives; no Unity.
- `RetroRPG.Importers.GBA.PokemonFireRed`: revision-specific layouts and parsers; emits IR.
- `RetroRPG.Unity`: reusable Unity-facing presentation types.
- `RetroRPG.Editor`: ROM selection and deterministic asset generation; Editor only.
- `RetroRPG.Runtime`: gameplay over generated assets; no ROM access.

Dependencies are unidirectional and enforced by assembly definitions. Generated
proprietary content is local-only under `Assets/Imported`.

## Verified vertical-slice state

`RomFile` opens a user-selected snapshot without retaining its absolute path in the
project. It exposes length and SHA-1/SHA-256 fingerprints; the Inspector stores only
the last selection in `EditorPrefs`. `GameDetector` selects FireRed USA rev1 only when
the valid GBA header and exact SHA-1 agree. Unsupported BPRE revisions and unknown
files stop before map parsing.

`PalletTownParser` consumes the verified `FireRedRomLayoutRev1` offsets, performs a
bounds-checked parse and emits game-agnostic map/tileset IR. It confirms a 24x20 map
(480 cells), `General` primary tileset, `PalletTown` secondary tileset, decoded 4bpp
tiles/BGR555 palettes, metatile attributes/layer routes, and declarative animation
frames. It never executes ROM callbacks.

`PalletTownAssetBuilder` validates the complete IR before touching generated assets. It
expands 16x16 metatiles into 8x8 sprites at PPU 16, uses 0.5 Unity cells, reverses the
map Y axis deterministically, and writes explicit `Bottom`, `Middle`, and `Top`
Tilemaps. `DeterministicAnimatedTile` is the project-local `TileBase` implementation;
it supplies fixed-speed synchronized frames and avoids the incompatible Tilemap Extras
editor package on this Unity version.

Evidence: Unity compile exit 0; full EditMode 37 total, 35 passed, 2 explicit skipped,
0 failed; explicit local-ROM parse 1/1; explicit double import 1/1 with stable content
hashes and GUIDs; PlayMode smoke 1/1. The generated scene is preview-only: no player,
NPC, collision runtime, or gameplay is included.
