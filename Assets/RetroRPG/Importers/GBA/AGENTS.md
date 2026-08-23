# GBA importer instructions

- Bounds-check every read and decompression output.
- Document address conversion and verified layouts in `docs/ROM_FORMAT.md`.
- Importers emit IR and never create Unity objects.
- Never guess unknown bytes silently or expose proprietary ROM content.

