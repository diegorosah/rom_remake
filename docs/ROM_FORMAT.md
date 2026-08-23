# ROM format: Pokemon FireRed USA rev1

This document records facts verified against the exact supported ROM snapshot and
the primary `pret/pokefirered` decompilation. All addresses below are **file offsets**
unless explicitly identified as GBA pointers. These facts apply only to the supported
fingerprint; other BPRE revisions are recognized but rejected before map parsing.

## Supported fingerprint and header

- Size: 16 MiB (`0x01000000` bytes).
- Game code: `BPRE`; maker code: `01`; software version: `1`.
- SHA-1: `dd5945db9b930750cb39d00c84da8571feebf417`.
- SHA-256: `729041b940afe031302d630fdbe57c0c145f3f7b6d9b8eca5e98678d0ca4d059`.

The header parser requires at least `0xC0` bytes. It reads title at `0xA0`, game code
at `0xAC`, maker code at `0xB0`, fixed byte at `0xB2`, software version at `0xBC`,
and complement check at `0xBD`. The complement is recalculated over `0xA0..0xBC`.
Import is authorized only when the valid header and SHA-1 both match this baseline.

## Pointer and bounds rules

- A ROM pointer is canonical only in the GBA window beginning at `0x08000000`.
- File offset is `pointer - 0x08000000`, checked against the loaded snapshot.
- Thumb callback addresses are normalized by clearing bit 0 only for comparison.
- Every fixed or pointer-derived read validates its complete range before access.
- Multiplication/addition for counts and byte sizes is checked for overflow.
- Parser limits are named in `FireRedRomLayoutRev1`; no scan or guessed offset is
  permitted for the supported revision.

The parser never executes ARM/Thumb code. A callback is accepted only as a declarative
animation identifier after the exact ROM fingerprint and callback value both match.

## Pallet Town discovery

`gMapGroups` is at `0x352718`. Group 3 points to `0x352364`; map 0 in that group points
to the Pallet Town `MapHeader` at `0x350688`.

`gMapLayouts` is at `0x34EBFC`. Layout ID 78 uses zero-based table index 77 and points
to the Pallet Town `MapLayout` at `0x2DD530`.

### MapHeader (`0x1C` bytes at `0x350688`)

| Field | Verified value |
|---|---:|
| map layout | `0x2DD530` |
| events | `0x3B4EC0` |
| scripts | `0x1654D2` |
| connections | `0x3527DC` |
| music | `0x012C` |
| layout ID | `78` |
| region section | `0x58` |
| weather | `2` |
| map type | `1` |
| biking allowed | `1` |
| flags | `0x06` |
| floor type | `0` |
| battle scene | `0` |

### MapLayout (`0x1C` bytes at `0x2DD530`)

| Field | Verified value |
|---|---:|
| width | `24` |
| height | `20` |
| border cells | `0x2DD168` |
| map cells | `0x2DD170` |
| primary tileset | `0x2D4B04` (`General`) |
| secondary tileset | `0x2D4B1C` (`PalletTown`) |
| border width/height | `2 x 2` |

The map contains exactly 480 little-endian `u16` cells. A cell is decoded as:

- metatile ID: bits `0..9` (`raw & 0x03FF`);
- collision: bits `10..11` (`(raw >> 10) & 0x03`);
- elevation: bits `12..15` (`(raw >> 12) & 0x0F`).

The verified map uses 98 distinct metatiles in the range 2..728: 24 primary IDs and
74 secondary IDs.

## Tilesets

A tileset header is `0x18` bytes. Bytes 0 and 1 contain compression/secondary flags;
the pointer fields are graphics at `+0x04`, palettes at `+0x08`, metatiles at `+0x0C`,
animation callback at `+0x10`, and attributes at `+0x14`.

### Primary: General (`0x2D4B04`)

- compressed primary graphics: LZ10 at `0xEA1D68`, output `0x5000` bytes = 640 tiles;
- palettes: `0xEA1B68`; 16 banks physically present, banks 0..6 routed as primary;
- metatiles: `0x29F738`; 640 records x 8 little-endian `u16` subtiles;
- attributes: `0x2A1F38`; 640 little-endian `u32` records;
- raw animation callback pointer: `0x08070169`.

### Secondary: PalletTown (`0x2D4B1C`)

- compressed secondary graphics: LZ10 at `0x26D3EC`, output `0x0980` bytes = 76 tiles;
- palettes: `0x26D830`; banks 7..12 are routed as secondary;
- metatiles: `0x2A2938`; 89 records;
- attributes: `0x2A2EC8`; 89 records;
- animation callback: null.

Primary tile/metatile capacity is 640. Secondary tile slots therefore become global
IDs 640..715, and secondary metatiles become global IDs 640..728. The engine-wide
capacity is 1024 metatiles, with 13 routed palette banks (7 primary + 6 secondary).

## LZ10

The stream begins with byte `0x10` and a 24-bit little-endian declared output size.
Each flag byte is consumed from bit 7 to bit 0:

- flag 0: copy one literal byte;
- flag 1: read two bytes, length = `(first >> 4) + 3`, distance =
  `(((first & 0x0F) << 8) | second) + 1`.

Back-references may overlap already-produced output. The decoder rejects missing
headers/flags/payload, distance before output start, declared output above its named
limit, or any copy that crosses the declared output length.

## 4bpp tiles and palettes

Each tile is 8x8 indexed pixels stored in 32 bytes. For every byte, the low nibble is
the left pixel and the high nibble is the right pixel.

Palette colors are little-endian BGR555. Each 5-bit channel expands to 8-bit with
`(value << 3) | (value >> 2)`. Palette index zero is transparent on tile layers;
the camera supplies the opaque black backdrop color.

## Metatiles, flips and attributes

Each metatile has eight `u16` subtile entries. Entries 0..3 form first-plane
top-left, top-right, bottom-left, bottom-right; entries 4..7 form the second plane in
the same order. A subtile word contains:

- tile index: bits `0..9`;
- horizontal flip: bit `10`;
- vertical flip: bit `11`;
- palette bank: bits `12..15`.

The `u32` attribute record uses these verified masks:

| Meaning | Bits |
|---|---:|
| behavior | `0..8` |
| terrain | `9..13` |
| attribute 2 | `14..17` |
| attribute 3 | `18..23` |
| encounter type | `24..26` |
| attribute 5 | `27..28` |
| layer type | `29..30` |
| attribute 7 | `31` |

Layer routing is declarative:

| Layer type | First plane | Second plane |
|---:|---|---|
| 0 (normal) | Middle | Top |
| 1 (covered) | Bottom | Middle |
| 2 (split) | Bottom | Top |
| 3 | invalid; parser error | invalid |

Pallet Town uses 64 layer-0 and 34 layer-1 metatiles; no layer-2 metatile is referenced
by this map.

## Declarative animations

Only the verified `General` callback maps to the following specifications. Frame data
is 4bpp tile data and is parsed as data, never instructions.

| ID | Destination tiles | Tiles/frame | Frames | Ticks/frame | Frame offsets |
|---|---:|---:|---:|---:|---|
| flower | 508..511 | 4 | 5 | 16 | `0x3A7450`, `0x3A74D0`, `0x3A7550`, `0x3A75D0`, `0x3A7650` |
| water | 416..463 | 48 | 8 | 16 | `0x3A76E4`, `0x3A7CE4`, `0x3A82E4`, `0x3A88E4`, `0x3A8EE4`, `0x3A94E4`, `0x3A9AE4`, `0x3AA0E4` |
| sand | 464..481 | 18 | 8 | 8 | `0x3AA6E4`, `0x3AA924`, `0x3AAB64`, `0x3AADA4`, `0x3AAFE4`, `0x3AB224`, `0x3AB464`, `0x3AB6A4` |

Flower loops every 80 ticks, water every 128 ticks, and sand every 64 ticks. Pallet
Town references water with palette banks 4 and 6 and flowers with bank 0. Sand is not
used by the Pallet Town scene but remains in the imported General tileset IR. A Unity
animated asset key must include destination tile, palette bank, and both flip flags.

## Rendering interpretation

Metatiles are 16x16 pixels and occupy one world unit. Subtiles use PPU 16 and Unity
cells of 0.5 x 0.5. Map row order is top-to-bottom; Unity cell Y is inverted
deterministically. Palette and flips are applied before sprite creation.

## Primary references

- `include/global.fieldmap.h` in `pret/pokefirered` for field-map structures and masks.
- `data/layouts/layouts.json` for named map layouts and Pallet Town dimensions/tilesets.
- `src/tileset_anims.c` and related data tables for animation callback semantics.
- GBA/Nintendo compression and graphics conventions as represented by the decomp's
  LZ77 and tile data contracts.

No source code or proprietary asset from the decompilation is copied into this project.
