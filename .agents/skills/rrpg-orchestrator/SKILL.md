---
name: rrpg-orchestrator
description: Orchestrate implementation of the earliest incomplete RetroRPG milestone with the project's specialized agents and validation gates.
---

Read `ROADMAP_CODEX_UPDATED.md` and applicable `AGENTS.md` files. Work only on the
earliest incomplete milestone unless the user explicitly selects another.

Route architecture to `rrpg_architect`, uncertain binary formats to `rom_analyst`,
documented parsers to `parser_worker`, Unity work to `unity_worker`, focused tests to
`test_worker`, and verified documentation to `docs_worker`. Parallelize independent
read-only tasks, but keep one writer per subsystem.

Before closing a milestone, run relevant tests and compile checks, obtain a
`milestone_reviewer` review, fix blocking findings, rerun validation, then synchronize
documentation and checklists. Report completed work, files changed, tests, remaining
risks, and the next eligible task.

