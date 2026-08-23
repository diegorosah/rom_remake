# Decisions

## Baseline

- Unity: `6000.5.9f1`, Universal 2D, Windows Editor.
- ROM: Pokemon FireRed USA rev1 (`BPRE`, software version 1), exact SHA-1
  `dd5945db9b930750cb39d00c84da8571feebf417`.
- Other revisions may be recognized but are blocked from import.

## Rendering

- A metatile is one world unit; 8x8 subtiles use PPU 16 and Tilemap cell size 0.5.
- Bottom, middle, and top Tilemaps preserve FireRed layer routing.
- The first vertical slice includes the animations used by General and PalletTown
  tilesets but no player, NPC, collision runtime, or battle system.

