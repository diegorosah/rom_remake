# RetroRPG AI Workflow

`ROADMAP_CODEX_UPDATED.md` is the project source of truth. Codex works on the earliest
incomplete milestone unless the user explicitly chooses another.

## Roles

| Work | Agent | Default model | Writes |
|---|---|---|---|
| Architecture and milestone planning | `rrpg_architect` | `gpt-5.6-sol` / xhigh | No |
| ROM investigation | `rom_analyst` | `gpt-5.6-sol` / xhigh | No |
| Documented parser implementation | `parser_worker` | `gpt-5.6-terra` / high | Yes |
| Unity and Editor implementation | `unity_worker` | `gpt-5.6-terra` / high | Yes |
| Focused tests and fixtures | `test_worker` | `gpt-5.6-luna` / medium | Yes |
| Documentation synchronization | `docs_worker` | `gpt-5.6-luna` / medium | Yes |
| Milestone gate | `milestone_reviewer` | `gpt-5.6-sol` / high | No |

Subagents normally inherit the parent turn's live permission mode. A custom agent's
`sandbox_mode` narrows its intended role, but live parent overrides can still take
precedence. Select the parent permission mode before delegation.

## Delivery gate

1. Plan the current milestone.
2. Investigate uncertain formats before implementation.
3. Assign one writer per subsystem; parallelize only independent work.
4. Run relevant Unity tests and compile checks.
5. Obtain a read-only milestone review.
6. Fix blockers and rerun affected validation.
7. Update docs and roadmap status from evidence.

If custom agents are unavailable, run the same roles sequentially and retain the same
gates. Never treat missing background-agent UI as permission to skip validation.

## Discovery checks

Start a fresh Codex session at the repository root and ask it to list active instruction
sources, repo skills, and custom agents. Invoke `$plan-current-mvp`, then delegate one
read-only architecture task and one independent read-only investigation. Confirm that
the main thread receives both summaries. Project-scoped `.codex/config.toml` is loaded
only after the project is trusted.

### Validation evidence (2026-08-23)

- The repository instructions and all five repo-scoped skills were discovered in a
  fresh session rooted at `D:\rom_remake`; each skill passed its structural validator.
- The configured read-only `rom_analyst` returned the independently verified FireRed
  rev1 Pallet Town layout/tileset/animation specification to the main thread.
- The configured read-only `rrpg_architect` returned an architecture gate review to
  the main thread, including actionable package, immutability, diagnostics, and test
  findings.
- Writer concurrency was kept to one parser writer and one Unity/Editor writer, with
  non-overlapping ownership. Sequential fallback and all delivery gates remain in
  force when custom agents are unavailable.

## Proprietary data

ROMs remain user-owned local inputs. Paths are stored only in machine-local Editor
preferences. Logs and manifests may contain filenames, sizes, hashes, and verified
offsets, but never ROM bytes or extracted assets. `POKEMON_FIRERED_ROM` and
`Assets/Imported` are ignored by Git.
