# RetroRPG IR specification

The IR is independent from Unity and from any specific game. Its schema is versioned and
serialized to deterministic debug JSON by Editor tooling.

The Pallet Town vertical slice uses map dimensions/cells, indexed 8x8 graphics, palette
banks, metatiles composed from eight subtile placements, render-layer roles, and tile
animation tracks. FireRed-specific numeric bitfields are decoded before entering the IR.

