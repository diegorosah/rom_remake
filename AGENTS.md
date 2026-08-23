# RetroRPG Codex Instructions

## Source of truth

- `ROADMAP_CODEX_UPDATED.md` defines scope and implementation order.
- Work on the earliest incomplete milestone unless the user explicitly selects another milestone.
- Do not silently expand scope or mark checklist items complete without evidence.

## Architecture

- `RetroRPG.Core` and `RetroRPG.IR` remain game-agnostic and do not depend on Unity.
- FireRed-specific formats and rules belong in `Assets/RetroRPG/Importers/GBA/PokemonFireRed`.
- Importers emit IR and never create Unity objects.
- Unity/Editor code converts IR into generated assets.
- Runtime consumes imported assets and never reads a ROM.

## ROM safety

- Validate bounds before every binary read and decompression write.
- Keep offsets, signatures, pointer rules, and safety limits in named format definitions.
- Distinguish verified ROM facts from hypotheses and document verified discoveries in `docs/ROM_FORMAT.md`.
- Never commit ROMs, extracted graphics, generated scenes, or other proprietary content.
- Never print or return raw proprietary ROM bytes.

## Workflow

- Use `rrpg_architect` for architecture and milestone planning.
- Use `rom_analyst` when a binary structure is uncertain.
- Use `parser_worker` only after the relevant structure is documented.
- Use `unity_worker` for Unity/C# implementation, `test_worker` for focused tests, and `docs_worker` for verified documentation updates.
- Use `milestone_reviewer` before closing a milestone.
- Parallelize independent read-only work; allow only one writer per subsystem.

## Validation

- Run the relevant Unity EditMode or PlayMode tests after changes.
- Keep generated test results outside `Assets`.
- Update documentation whenever a public contract or verified ROM fact changes.

