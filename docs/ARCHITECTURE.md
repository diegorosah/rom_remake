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

## Verified MVP 2 state

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

`PalletTownAssetBuilder` validates the complete map and player-sprite IR before touching
generated assets. It expands 16x16 metatiles into 8x8 sprites at PPU 16, uses 0.5 Unity
cells, reverses the map Y axis deterministically, and writes explicit `Bottom`, `Middle`,
`Top`, `Collision`, and `Player` objects. Tilemap sorting is Bottom=0, Middle=1,
Player=2, Top=3. The generated player uses `DirectionalSpriteAnimator`,
`PlayerController`, and the verified 16x32 FireRed overworld sprite. `GridCollisionMap`
stores bottom-up collision/elevation data and `PixelPerfectCameraFollow` clamps and
quantizes the orthographic camera. `DeterministicAnimatedTile` supplies fixed-speed
synchronized map frames and avoids the incompatible Tilemap Extras editor package on
this Unity version.

Evidence: Unity compile exit 0; full EditMode 48 total, 46 passed, 2 explicit skipped,
0 failed; explicit local-ROM parse 1/1; explicit double import 1/1 with stable content
hashes and GUIDs; PlayMode smoke `mvp2-playmode-4` 1/1, including open movement,
blocked collision, animation, and camera quantization. The generated scene supports
walking in the Pallet Town grid; warps, NPCs, dialogs, and battles remain out of scope.

## Accelerated MVP 3–7 implementation checkpoint

The generated runtime now uses a persistent `RuntimeMapCatalog` and
`MapTransitionSystem` for Pallet Town, three interiors, and Route 1. Game-agnostic IR
contains maps, warps, object events, dialogue tokens, encounter tables, and battle
content; FireRed-specific pointer layouts and whitelist parsers remain isolated in the
FireRed importer. Runtime components consume only serialized/generated Unity data.

`RetroRPG.Core` owns the deterministic one-on-one `BattleState`, actions, turn order,
HP, outcomes, and the `IBattleContentCatalog`/`IBattleView` ports. Runtime coordinates
encounter events and overworld locks, while `Classic2D` renders the selectable battle
panel and imported front/back sprites. The preview keeps party HP only in memory for
the lifetime of the scene. It deliberately excludes save persistence and native
FireRed formula details such as IVs, EVs, nature, STAB, types, random damage, status,
PP, abilities, and executable move effects.

These milestones have static review and synthetic test coverage in source. Their Unity
compile, EditMode, explicit ROM/generation, and PlayMode evidence is intentionally
deferred to the final integrated renderer gate; the earlier MVP 2 evidence above is the
last executed Unity baseline.
