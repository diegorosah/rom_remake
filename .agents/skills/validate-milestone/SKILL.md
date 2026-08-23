---
name: validate-milestone
description: Validate a RetroRPG milestone against its roadmap acceptance criteria before documentation or checklist completion.
---

Run the milestone's EditMode and PlayMode tests, compile/static checks, and any explicit
local-ROM integration tests without committing generated content. Compare observable
results with the roadmap criteria, then request a read-only `milestone_reviewer` pass.
Fix blocking findings and rerun the affected checks. Only after a clean gate may
`docs_worker` synchronize documentation and mark verified checklist items. Report exact
commands, results, blockers, and the next eligible milestone.

