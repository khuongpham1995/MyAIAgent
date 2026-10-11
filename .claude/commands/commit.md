---
description: Choose commit only or commit plus PR for a verified task with Sonnet; --pr preselects PR.
argument-hint: "[--pr]"
model: sonnet
---

Follow Commit and optional PR in `docs/ai/workflow.md`. First check the user's
actual request: only the user invoking this command or explicitly requesting a
commit authorizes it. Automatic loading or an agent invoking this command does
not authorize Git changes. Without that user request, report readiness only.
After checking readiness, show the shared workflow's delivery question with
**Just commit only** and **Commit + PR** when the user has not already chosen.
Use the available interactive question UI; otherwise ask in plain text and wait.
An explicit commit-only request or `--pr` already answers it; do not ask twice.
Only the user's **Commit + PR** selection, `--pr`, or explicit publication request
authorizes push/PR.
Require current quality results and both reviewers; inspect the exact staged diff
and preserve unrelated work. Run the `docs/ai/evidence.md` candidate check with both
review records before staging and again with `--staged` immediately before commit. Use Summary / Changes / Risks in the commit and PR.
Return the commit hash and, when created, PR URL. Report blockers truthfully.
Options: $ARGUMENTS
