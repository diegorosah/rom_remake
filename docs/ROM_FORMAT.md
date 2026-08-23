# ROM format: Pokemon FireRed USA rev1

Supported fingerprint: SHA-1 `dd5945db9b930750cb39d00c84da8571feebf417`,
16 MiB, game code `BPRE`, maker code `01`, software version `1`.

## GBA header

The importer requires at least `0xC0` bytes. It reads the title at `0xA0`, game code at
`0xAC`, maker code at `0xB0`, fixed value at `0xB2`, software version at `0xBC`, and
complement check at `0xBD`. The complement is recalculated across `0xA0..0xBC`.

## Safety rules

- ROM pointers must resolve from the `0x08000000` address window into the file.
- Every fixed and pointer-derived read validates its range before access.
- LZ77 output is bounded by the declared size and the caller's expected maximum.
- Revision-specific addresses belong in `FireRedRomLayoutRev1` after evidence is recorded.

## Pallet Town

Verified from the matching decompilation: layout ID `LAYOUT_PALLET_TOWN`, dimensions
24x20, primary tileset `General`, secondary tileset `PalletTown`. Exact revision-1 ROM
addresses and animation sources are added only after binary cross-checking.
