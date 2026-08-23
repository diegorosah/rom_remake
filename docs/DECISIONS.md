# Decisions

## Baseline

- Unity: `6000.5.9f1`, Universal 2D, Windows Editor.
- ROM: Pokemon FireRed USA rev1 (`BPRE`, software version 1), exact SHA-1
  `dd5945db9b930750cb39d00c84da8571feebf417`.
- Other revisions may be recognized but are blocked from import.

## Rendering

- A metatile is one world unit; 8x8 subtiles use PPU 16 and Tilemap cell size 0.5.
- Bottom, middle, and top Tilemaps preserve FireRed layer routing.
- MVP 1 generated the layered Pallet Town preview. MVP 2 adds the verified Red normal
  player sprite, bottom-up collision/elevation data, grid movement, directional idle and
  walking animation, and a pixel-perfect camera follow. Warps, NPCs, and battle systems
  remain subsequent milestones.
- `com.unity.2d.tilemap.extras` is not used because its 6.0.0 editor code does not
  compile on Unity 6000.5.9f1. `RetroRPG.Unity.DeterministicAnimatedTile` implements
  only the required `TileBase` animation contract, with fixed speed and synchronized
  start time. Non-uniform durations are represented by repeated frames.

## Verified gates

- Unity compile: exit code 0.
- Full EditMode: 48 total, 46 passed, 2 explicit skipped, 0 failed.
- Explicit local-ROM parser integration: 1/1, including fingerprint, dimensions,
  cells, tilesets, ranges and animations.
- Explicit double generation/reimport: 1/1; content hashes and Unity GUIDs are stable.
- PlayMode smoke `mvp2-playmode-4`: 1/1; three Tilemaps, player spawn and movement,
  blocked collision, valid sprites, advancing animated frames, and quantized camera.

These are implementation evidence for MVP 2. The roadmap checklist is complete for
MVP 2; later milestones remain gated and untouched.
