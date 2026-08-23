# RetroRPG IR specification

The IR is independent from Unity and from any specific game. Its schema is versioned and
serialized to deterministic debug JSON by Editor tooling.

The Pallet Town vertical slice uses map dimensions/cells, indexed 8x8 graphics, palette
banks, metatiles composed from eight subtile placements, render-layer roles, and tile
animation tracks. FireRed-specific numeric bitfields are decoded before entering the IR.

## Schema version 1

`PalletTown.ir.json` is UTF-8, newline-terminated, uses invariant numeric formatting,
and contains no timestamps, paths, GUIDs, or nondeterministic fields. Object fields and
arrays are emitted in this fixed order:

```text
root: schemaVersion=1, id, name, width, height, primaryTileset,
      secondaryTileset, tilesets[], cells[]
cell: metatile, collision, elevation
tileset: id, isSecondary, tiles[], palettes[], metatiles[], animations[]
tile: index, pixels[64]                         # indexed 4bpp, row-major 8x8
palette: index, colors[16][4]                   # RGBA bytes
metatile: index, attributes, route{first,second}, subtiles[8]
subtile: tile, palette, hFlip, vFlip
animation: id, destinationTile, durationTicks, frames[]
frame: tiles[]
diagnostic report: schemaVersion=1, stage, diagnostics[]
diagnostic: stage, category, severity, message, offset?, length?
```

`tilesets` is ordered primary then secondary, and cells are row-major from the ROM.
The report uses the same deterministic serializer and omits absent optional ranges.
This is diagnostic/interchange JSON, not a Unity serialization contract.
