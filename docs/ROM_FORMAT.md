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

## MVP 3 — Pallet Town warp bundle (audited rev1)

This section records the exact data needed for the first interior transition bundle.
All offsets below are file offsets in the validated FireRed USA v1.1/BPRE snapshot;
each pointer is accepted only when it is a canonical GBA pointer (`0x08000000`-
`0x08FFFFFF`) whose converted range is inside the 16 MiB ROM. The map-group table
is at `0x352718`; group 3 points to `TownsAndRoutes` at `0x352364` and group 4
points to `IndoorPallet` at `0x35246C`.

### Selected maps and pointer chain

`MapHeader` is `0x1C` bytes. Its first four fields are pointers to `MapLayout`,
`MapEvents`, scripts and connections. `MapLayout` is `0x1C` bytes with signed
32-bit width/height, border/map pointers, primary/secondary tileset pointers and
border dimensions. The following values are verified in the ROM and cross-checked
against the named map/layout records in `pret/pokefirered`:

| Map (group:number) | Header | Layout | Dimensions | Events | Layout ID | Primary / secondary |
|---|---:|---:|---:|---:|---:|---|
| Pallet Town (3:0) | `0x350688` | `0x2DD530` | 24×20 | `0x3B4EC0` | 78 | General / PalletTown |
| PlayersHouse1F (4:0) | `0x350DC0` | `0x2D5270` | 13×10 | `0x3B97BC` | 1 | Building / GenericBuilding1 |
| PlayersHouse2F (4:1) | `0x350DDC` | `0x2D536C` | 12×9 | `0x3B97FC` | 2 | Building / GenericBuilding1 |
| RivalsHouse (4:2) | `0x350DF8` | `0x2D5494` | 13×10 | `0x3B987C` | 3 | Building / GenericBuilding2 |

The corresponding map-cell pointers are `0x2DD170`, `0x2D516C`, `0x2D5294`
and `0x2D5390`; border pointers are `0x2DD168`, `0x2D5164`, `0x2D528C`
and `0x2D5388`. Cell count is width×height, so the selected bundle contains
848 map cells. Oak's Lab (group 4, map 3; header `0x350E14`, layout `0x2D56D8`)
is intentionally outside this bundle even though Pallet's third door references it.

### MapEvents and WarpEvent

`MapEvents` is 20 bytes: counts at offsets `0..3`, followed by pointers at
`+4/+8/+C/+10` for object, warp, coordinate and background event arrays. A
`WarpEvent` is 8 bytes: signed `s16 x,y`, `u8 elevation`, `u8 warpId`,
`u8 mapNum`, `u8 mapGroup`. The exact event counts are:

| Map | Objects | Warps | Coordinates | Background | Warp array |
|---|---:|---:|---:|---:|---:|
| Pallet Town | 3 | 3 | 3 | 5 | `0x3B4E3C` |
| PlayersHouse1F | 1 | 4 | 0 | 1 | `0x3B9790` |
| PlayersHouse2F | 0 | 1 | 0 | 3 | `0x3B97D0` |
| RivalsHouse | 2 | 3 | 0 | 3 | `0x3B9840` |
| **Total** | **6** | **11** | **3** | **12** | |

The eleven records resolve as follows (destination is `mapNum:mapGroup`):

| Source | `(x,y,elevation,warpId)` | Destination |
|---|---|---|
| Pallet Town #0 (`0x3B4E3C`) | `(6,7,0,1)` | PlayersHouse1F (`0:4`) |
| Pallet Town #1 (`0x3B4E44`) | `(15,7,0,0)` | RivalsHouse (`2:4`) |
| Pallet Town #2 (`0x3B4E4C`) | `(16,13,0,0)` | Oak Lab (`3:4`, excluded) |
| PlayersHouse1F #0 (`0x3B9790`) | `(5,8,3,0)` | Pallet Town (`0:3`) |
| PlayersHouse1F #1 (`0x3B9798`) | `(4,8,3,0)` | Pallet Town (`0:3`) |
| PlayersHouse1F #2 (`0x3B97A0`) | `(10,2,3,0)` | PlayersHouse2F (`1:4`) |
| PlayersHouse1F #3 (`0x3B97A8`) | `(3,9,0,0)` | Pallet Town (`0:3`) |
| PlayersHouse2F #0 (`0x3B97D0`) | `(10,2,3,2)` | PlayersHouse1F (`0:4`) |
| RivalsHouse #0 (`0x3B9840`) | `(4,8,3,1)` | Pallet Town (`0:3`) |
| RivalsHouse #1 (`0x3B9848`) | `(5,8,3,1)` | Pallet Town (`0:3`) |
| RivalsHouse #2 (`0x3B9850`) | `(3,8,3,1)` | Pallet Town (`0:3`) |

The records have no activation field; activation is derived declaratively from the
verified metatile behavior and direction. Pallet warps 0/1 are collision-1
`WARP_DOOR` (`0x69`) targets activated by a north attempt from the cell below.
House1F warp 1 and Rival warp 0 use `SOUTH_ARROW` (`0x65`) and activate south;
House1F warp 2 uses `UP_RIGHT_STAIR` (`0x6C`) and activates east; House2F warp 0
uses `DOWN_LEFT_STAIR` (`0x6F`) and activates west. House1F warps 0/3 and Rival
warps 1/2 lie on behavior `0x00` and remain inactive records. Arrival facing is
south for a door, north for `SOUTH_ARROW`, west for `UP_RIGHT_STAIR` and east for
`DOWN_LEFT_STAIR`. The engine places the player on the destination record but does
not evaluate a warp merely because a scene loaded; the normalized runtime also
suppresses that arrival record until the first completed move away.

### Interior tilesets

The tileset structures are verified at the following offsets. `tiles`, `palettes`,
`metatiles` and `metatileAttributes` are the pointer fields at structure offsets
`+4`, `+8`, `+C` and `+14`. Graphics use LZ77 type `0x10`; decoded sizes and counts
are checked before allocation. Metatiles are eight `u16` entries (16 bytes each)
and attributes are one `u32` per metatile.

| Tileset | Struct | Graphics | Decoded tiles | Palettes | Metatiles | Attributes | Metatile count |
|---|---:|---:|---:|---:|---:|---:|---:|
| Building | `0x2D4C24` | `0x275304` | 20,480 B / 640 | `0x277704` | `0x2AD824` | `0x2B0024` | 640 |
| GenericBuilding1 | `0x2D4CE4` | `0xEA99F4` | 2,016 B / 63 | `0xEA97F4` | `0x2B4EBC` | `0x2B503C` | 24 |
| GenericBuilding2 | `0x2D4EF4` | `0x28E614` | 4,864 B / 152 | `0x28ECE0` | `0x2BEF84` | `0x2BFB04` | 184 |

The aggregate acceptance gate is four exact map header/layout chains; dimensions
13×10, 12×9 and 13×10 for the interiors; 848 cells; 11 warp records with the
flow above; 6 object events, 12 background events, 3 coordinate events; and a
bounds-safe decode of the three tilesets (855 graphics tiles and 848 metatiles).
No script or callback is executed. Oak Lab's layout, events and tileset are not
parsed by this selected-bundle gate and remain explicit follow-up scope.

Verified cell aggregates provide a structural integration check: House1F has 130
cells with collision 0/1 counts 76/54 and elevation 0/3 counts 54/76; House2F has
108 cells with collision 0/1 counts 70/38 and elevation 0/3/4 counts 43/63/2;
Rival House has 130 cells with collision 0/1 counts 85/45 and elevation 0/3 counts
35/95.

The parser validates the exact header and SHA-1 before using any named offset. It
checks both `gMapGroups` and `gMapLayouts` pointer routes, positive dimensions,
`width * height * 2`, every `count * stride`, and all base/index additions with
checked arithmetic. `MapEvents` must be in range; a positive count requires a
canonical pointer and complete array range, while only count zero permits null.
Warp source coordinates must be inside the source map. All selected maps are parsed
before destinations are resolved; an internal destination must name a selected map
and a valid zero-based warp index. Oak's Lab is retained as an explicit unresolved
destination that produces a warning and remains blocked at runtime.

### Evidence and implementation boundary

Primary evidence is commit `c75f352304d529f6ba92d4f74b9cf8b5c3810788` of
`pret/pokefirered`, especially `include/global.fieldmap.h` (structure sizes,
pointer fields and `WarpEvent`), `data/maps/map_groups.json` (group ordering and
names), `data/layouts/layouts.json` (dimensions and tileset names), and the named
map/event and tileset data generated by that revision. This project copies neither
decompilation code nor proprietary assets. The parser emits neutral map/warp IR;
Unity transitions, fades, facing, suppression and gameplay ownership remain
runtime decisions.

## MVP 4 — selected object events and NPC graphics (audited rev1)

The selected four-map bundle contains six `ObjectEventTemplate` records: five
humanoid NPCs and one inanimate Town Map prop. `ObjectEventTemplate` is `0x18`
bytes: local/graphics/kind bytes at `+0x00..+0x02`, signed coordinates at
`+0x04/+0x06`, elevation/movement at `+0x08/+0x09`, packed movement ranges at
`+0x0A`, trainer fields at `+0x0C/+0x0E`, script pointer at `+0x10` and visibility
flag at `+0x14`. All selected records are normal kind 0 with zero reserved and
trainer fields. Script pointers are retained only as identities and never executed.

| Map | Object array | Count | Selected records |
|---|---:|---:|---|
| Pallet Town | `0x3B4DF4` | 3 | Woman1 `(3,10)`, FatMan `(13,17)`, ProfOak `(10,8)` |
| PlayersHouse1F | `0x3B9778` | 1 | Mom `(8,4)` |
| PlayersHouse2F | null | 0 | none |
| RivalsHouse | `0x3B9810` | 2 | Daisy `(10,6)`, TownMap prop `(6,4)` |

Local IDs are one-based and unique per map. Woman, Fat Man and Daisy use movement
type `0x02` (`WANDER_AROUND`) and begin facing south. Oak uses `0x07` (north),
TownMap uses `0x08` (south) and Mom uses `0x09` (west). Wander ranges are Woman
X 2..4/Y 6..14, Fat Man X 7..19/Y 15..19, and Daisy X 9..11/Y 3..9. The five
humanoid start cells are collision 0/elevation 3. TownMap is intentionally a
collision-1 wall prop and must not receive a mobile NPC controller.

The graphics-info pointer table is `0x39FE20`, stride 4, 152 static entries:

| Graphics ID | Info | Image table / logical frames | Raw graphics | Palette tag |
|---:|---:|---:|---:|---:|
| 23 Woman1 | `0x3A3DD0` | `0x3A06F8` / 10 | `0x370418` (`0xA00`) | `0x1105` |
| 27 FatMan | `0x3A3E84` | `0x3A0830` / 9 | `0x373418` (`0x900`) | `0x1106` |
| 71 ProfOak | `0x3A43DC` | `0x3A1408` / 9 | `0x389B98` (`0x900`) | `0x1106` |
| 76 Daisy | `0x3A4838` | `0x3A1A90` / 9 | `0x36B198` (`0x900`) | `0x1105` |
| 88 Mom | `0x3A515C` | `0x3A2978` / 9 logical | `0x391B98` (`0x300` unique) | `0x1103` |
| 93 TownMap | `0x3A49A0` | `0x3A1C70` / 1 | `0x369E98` (`0x100`) | `0x1103` |

Humanoids are 16×32, raw 4bpp, `0x100` bytes per logical frame and use the
already-audited standard eight cardinal idle/walk scripts at `0x3A33D8`. Woman's
tenth raise-hand frame is retained but unused by basic movement. Mom's logical
pointers repeat three physical frames and must preserve logical indices. TownMap is
32×16, inanimate, uses the inanimate table at `0x3A3384`, and emits a static state.

The selected raw BGR555 palettes are tag `0x1103` at entry `0x3A51C8` →
`0x36D898`, tag `0x1105` at `0x3A51D8` → `0x36D8D8`, and tag `0x1106` at
`0x3A51E0` → `0x36D8F8`; each contains exactly 16 colors (`0x20` bytes), with
index zero transparent.

Bounds rules: exact fingerprint first; validate full `MapEvents` and checked
`count * 0x18`; pointer null only for zero count; validate each complete template,
signed coordinates, elevation, local-ID uniqueness, supported movement type and
reserved fields. Graphics IDs must be below 152; every graphics-info record, frame
table entry, raw frame and palette range is checked before decoding. Only the
verified standard/inanimate animation tables and frame counts are accepted; commands
are decoded declaratively with bounded counts and a final jump-to-zero. No arbitrary
script, callback or ROM control flow is followed.

Flag 0 objects are visible. The explicit new-game preview profile hides Oak through
flag `0x002C`, shows TownMap until flag `0x0039` is set, and shows all other selected
objects. The parser preserves flag identity; this initial visibility profile is an
adapter/runtime policy, not an inference made by generic IR.

Primary references are `include/global.fieldmap.h`, `include/sprite.h`, constants
for event objects/movement/flags, the four selected `data/maps/*` records,
`src/event_object_movement.c`, `src/event_object_lock.c` and
`src/data/object_events/object_event_{graphics_info,graphics,pic_tables,anims}.h`
at `pret/pokefirered` commit `c75f352304d529f6ba92d4f74b9cf8b5c3810788`.

## MVP 5 — bounded dialogue circuits (audited rev1)

Dialogue import remains declarative: the adapter recognizes a small whitelist of
verified script shapes, resolves their text pointers, and emits neutral dialogue
IR. It never executes ROM scripts, calls specials, mutates flags/variables, or
interprets an arbitrary opcode stream.

The six selected object events carry these script identities (file offsets):

| Target | Script | MVP 5 policy |
|---|---:|---|
| Pallet Woman | `0x1657D4` | Stateful branches; blocked until a declared state profile is selected |
| Pallet Fat Man | `0x1658A7` | Supported static message circuit |
| Professor Oak | null | No interaction in the new-game preview |
| Mom | `0x168C81` | Stateful branch; blocked by default |
| Daisy | `0x168DCE` | Specials/variables/flags; explicitly unsupported by the first dialogue slice |
| Rival House Town Map | `0x168FDB` | Supported static message circuit |

The two first-gate circuits are closed forms equivalent to loading one verified
text pointer, invoking the standard NPC message box, and ending. Fat Man resolves
to text `0x17D885` (89 bytes including the bounded terminator scan); Town Map
resolves to `0x18D7DB` (62 bytes). Mom has a verified message branch at `0x18D449`
(39 bytes), but her entry checks flag `0x0258` and is not normalized without an
explicit state profile. Woman checks variable `0x4070`, variable `0x4002`, and
flag `0x0002`; one verified branch references `0x1B1D03` (20 bytes). Daisy depends
on multiple variables, flags and specials and therefore remains outside this
bounded parser. These offsets and lengths are structural metadata only; extracted
proprietary dialogue text is never versioned or logged.

The supported text codec requires `0xFF` end-of-string within a configured maximum
(at most 512 bytes for this slice). `0xFE` is newline; `0xFA`/`0xFB` are prompt
scroll/clear; `0xFC` begins an extended control with its command-specific bounded
operands; and `0xFD` begins a placeholder token. Unknown, truncated, or
non-whitelisted controls fail the whole dialogue snapshot. Normal glyph bytes are
mapped through the verified FireRed character table into semantic character
tokens; raw source bytes are not retained in IR.

The exact script gate validates the supported opcode sequence and operands,
canonical GBA pointers, full instruction ranges, the expected standard message-box
kind, and a terminal end command. An unexpected opcode, branch, destination,
message kind, missing terminator, invalid text token, or modified script pointer is
an import error. Interaction is runtime policy: the player presses the interaction
button while cardinally adjacent and facing the target. Only a declared NPC
dialogue may request `faceplayer`; static props never receive NPC movement logic.

Primary references are `asm/macros/event.inc`, `include/constants/characters.h`
and the selected map scripts/text declarations in `pret/pokefirered` at commit
`c75f352304d529f6ba92d4f74b9cf8b5c3810788`. The offsets above were independently
checked against the supported FireRed USA rev1 snapshot.

## MVP 6 — Route 1 land encounters (audited rev1)

Route 1 is map group 3, map number 19. The verified pointer chain is
`gMapGroups` `0x352718` → TownsAndRoutes group `0x352364` → `MapHeader`
`0x35089C` → layout `0x2E563C` (layout ID 89). The layout is 24×40 (960
cells), with border `0x2E4EB4`, cells `0x2E4EBC`, primary General tileset
`0x2D4B04`, secondary PalletTown tileset `0x2D4B1C`, and a 2×2 border.
Events are at `0x3B66B8` (two objects, no warps or coordinate events, one
background event); scripts are at `0x167F75` and connections at `0x352A64`.

For this slice, encounter terrain is normalized from bits 24..26 of the
already-bounds-checked metatile attribute word: 0 is none, 1 is land and 2 is
water. Route 1 contains exactly 178 land cells and 782 non-encounter cells, with
no water cells. The verified land-cell runs in top-down map coordinates are:

- y 6..10: x 10..21;
- y 13..17: x 16..21;
- y 24..28: x 12..17;
- y 32..33: x 4..10 and 17..21;
- y 34: x 2..8 and 15..19;
- y 35: x 2..8, 12..13 and 15..19;
- y 36..39: x 12..13.

The exact rev1 wild header is at `0x3CA3F4`. It is 20 bytes: map group and
number at `+0/+1`, padding at `+2/+3`, then land/water/rock-smash/fishing
pointers at `+4/+8/+C/+10`. Route 1 has land info `0x3C8F00`, encounter rate
21, and twelve four-byte land slots at `0x3C8ED0`; the other three method
pointers are null.

| Slot | Weight | Level | Species ID |
|---:|---:|---:|---|
| 0 | 20 | 3 | Pidgey (16) |
| 1 | 20 | 3 | Rattata (19) |
| 2 | 10 | 3 | Pidgey (16) |
| 3 | 10 | 3 | Rattata (19) |
| 4 | 10 | 2 | Pidgey (16) |
| 5 | 10 | 2 | Rattata (19) |
| 6 | 5 | 3 | Pidgey (16) |
| 7 | 5 | 3 | Rattata (19) |
| 8 | 4 | 4 | Pidgey (16) |
| 9 | 4 | 4 | Rattata (19) |
| 10 | 1 | 5 | Pidgey (16) |
| 11 | 1 | 4 | Rattata (19) |

Weights total 100; aggregated species weight is 50/50. Each native slot stores
minimum level, maximum level and a little-endian species ID; these selected slots
have equal minimum/maximum levels. Runtime encounter rolls use an injected random
source. The native base check is rate×16 against a 1600-value range (21% here,
before modifiers), and native behavior changes can add a 60% gate. These are
declarative rules for the runtime and are never executed by the parser.

Bounds rules: exact fingerprint first; validate the group/header/layout chain,
24×40 cell product, all map and event ranges, both tileset identities, every
referenced metatile attribute before classifying terrain, exact wild header map
identity, null unsupported-method pointers, rate, complete twelve-slot array,
known species IDs and weights totaling 100. This milestone parses only the
whitelisted Route 1 header and does not scan arbitrary wild headers.

Primary references are `include/wild_encounter.h`, the Route 1 map/layout data,
`src/wild_encounter.c`, and the generated wild encounter declarations in
`pret/pokefirered` at commit `c75f352304d529f6ba92d4f74b9cf8b5c3810788`.
