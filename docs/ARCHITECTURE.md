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

