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

## Player overworld sprite: Red on foot

The MVP 2 player facts were cross-checked against the supported rev1 snapshot and
`pret/pokefirered` commit `c75f352304d529f6ba92d4f74b9cf8b5c3810788`. They
apply only after the exact header and fingerprint gate described above succeeds.

### Verified pointer chain

| Structure | File offset/range | Verified contract |
|---|---:|---|
| graphics-info pointer entry 0 | `0x39FE20..0x39FE24` | points to `0x3A3C20` |
| Red normal graphics info | `0x3A3C20..0x3A3C44` | one `0x24`-byte record |
| image table | `0x3A0110..0x3A0158` | first nine `SpriteFrameImage` entries, 8 bytes each |
| animation pointer table | `0x3A34E0..0x3A3500` | idle/walk for four directions |
| player palette entry | `0x3A5208..0x3A5210` | tag `0x1100`, points to `0x35B9D8` |
| palette data | `0x35B9D8..0x35B9F8` | 16 raw BGR555 colours |
| normal graphics | `0x35BBD8..0x35C4D8` | nine raw 4bpp frames |

The graphics-info record must contain tile tag `0xFFFF`, palette tag `0x1100`,
reflection tag `0x1102`, allocation size `0x0200`, width 16, height 32 and foot
tracks. Its pointer fields must match the verified OAM (`0x083A3780`), subsprite
(`0x083A380C`), animation (`0x083A34E0`), image (`0x083A0110`) and affine-animation
(`0x08231D6C`) tables. These callbacks and scripts are validated as identifiers and
data; they are never executed.

All pointers are canonical little-endian GBA ROM pointers. The parser validates the
complete table or structure range before reading fields and uses checked arithmetic
for every count, stride and offset calculation.

### Frame format

- Each indexed frame is 16x32 pixels, arranged as 2x4 sequential 8x8 tiles.
- A frame is 256 bytes; all nine frames occupy exactly `0x900` raw bytes.
- Frame `n` begins at `0x35BBD8 + n * 0x100` for `n` in `0..8`.
- Every image-table entry must point to its corresponding frame and declare size
  `0x100`.
- The data is uncompressed 4bpp. Low/high nibble and BGR555 conversion follow the
  already-audited tile and palette rules; palette index zero becomes transparent.
- Any non-canonical pointer, unexpected size, non-contiguous frame or out-of-range
  index blocks import. These data must not be passed through LZ10.

### Declarative direction animations

| Direction | Idle | Walking sequence |
|---|---|---|
| south | frame `0`, 16 ticks | `3, 0, 4, 0`, 8 ticks each |
| north | frame `1`, 16 ticks | `5, 1, 6, 1`, 8 ticks each |
| west | frame `2`, 16 ticks | `7, 2, 8, 2`, 8 ticks each |
| east | frame `2`, H-flipped, 16 ticks | `7, 2, 8, 2`, all H-flipped, 8 ticks each |

The eight animation pointer slots at `0x3A34E0..0x3A3500` point respectively to
idle south/north/west/east at `0x3A2B34`, `0x3A2B3C`, `0x3A2B44`, `0x3A2B4C`, then
walking south/north/west/east at `0x3A2B54`, `0x3A2B68`, `0x3A2B7C`, `0x3A2B90`.
Idle scripts occupy 8 bytes and walking scripts 20 bytes. The parser accepts only
the exact audited frame commands followed by a jump to zero, positive durations,
frame indices `0..8`, and the declared flip flags. The IR retains frame indices,
flips and ticks, never ROM control flow.

The generic IR mapping is an `OverworldSpriteDefinition` named
`player_red_normal`, with a 16-colour RGBA palette, nine immutable indexed frames
and idle/walking animations for the four cardinal directions. Unity types and
FireRed offsets do not enter this contract or the Runtime assembly.

## Pallet Town movement collision

For the supported map, the 480 cells have the following verified distribution:

| Property | Count |
|---|---:|
| collision `0` | 282 |
| collision `1` | 198 |
| elevation `0` | 198 |
| elevation `1` | 12 |
| elevation `3` | 270 |
| normal behavior `0x00` | 460 |
| ocean-water behavior `0x15` | 12 |
| warp-door behavior `0x69` | 3 |
| signpost behavior `0x84` | 5 |
| directional behavior `0x30..0x37` | 0 |

The exact combinations are 270 passable/elevation-3 normal cells, 12
passable/elevation-1 water cells, 190 collision-1 normal cells, three collision-1
warp doors and five collision-1 signposts.

For a terrestrial player, a cardinal step is blocked when the destination is out
of bounds, destination collision is nonzero, a directional edge forbids the step,
or elevation is incompatible. Elevation mismatch follows the verified engine rule:

- current elevation zero accepts any target;
- target elevation zero or 15 accepts the current elevation;
- otherwise target and current elevation must match.

When directional behaviors are present, a south step checks south on the current
cell and north on the target; north checks north/south, west checks west/east, and
east checks east/west. Pallet Town references none of those behaviors, but the rule
is retained in the normalized collision contract. With preview spawn on elevation
3, all 198 collision-1 cells and all 12 elevation-1 water cells are unreachable.
Object occupancy belongs to the NPC milestone and is not part of MVP 2.

The standalone preview spawn is a product/runtime choice. MVP 2 uses a validated
passable elevation-3 cell; canonical warp-based placement belongs to MVP 3.

## Primary references

- `include/global.fieldmap.h` in `pret/pokefirered` for field-map structures and masks.
- `data/layouts/layouts.json` for named map layouts and Pallet Town dimensions/tilesets.
- `src/tileset_anims.c` and related data tables for animation callback semantics.
- `include/sprite.h`, `include/constants/event_objects.h` and
  `src/data/object_events/object_event_graphics*.h` for player graphics structures.
- `src/event_object_movement.c`, `src/fieldmap.c` and `src/metatile_behavior.c` for
  collision, elevation and directional-edge semantics.
- `spritesheet_rules.mk` for the 2x4-tile player-frame layout.
- GBA/Nintendo compression and graphics conventions as represented by the decomp's
  LZ77 and tile data contracts.

No source code or proprietary asset from the decompilation is copied into this project.
