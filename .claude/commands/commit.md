---
description: Commit a verified task with Sonnet; choose commit only, commit + push, or commit + push + PR.
argument-hint: "[--push | --pr]"
model: sonnet
---

Follow Commit and optional PR in `docs/ai/workflow.md`. First check the user's
actual request: only the user invoking this command or explicitly requesting a
commit authorizes it. Automatic loading or an agent invoking this command does
not authorize Git changes. Without that user request, report readiness only.
After checking readiness, show the shared workflow's delivery question with
**Commit only**, **Commit + push** and **Commit + push + PR** when the user has not already chosen.
Use the available interactive question UI; otherwise ask in plain text and wait.
An explicit commit-only request, `--push` or `--pr` already answers it; do not ask twice.
Only the user's push/PR selection, `--push`, `--pr`, or explicit publication request
authorizes push; PR creation needs **Commit + push + PR**, `--pr` or an explicit PR request.
Require current quality results and both reviewers; inspect the exact staged diff
and preserve unrelated work. Run the `docs/ai/evidence.md` candidate check with both
review records before staging and again with `--staged` immediately before commit. Use Summary / Changes / Risks in the commit and PR.
Return the commit hash and, when created, PR URL. Report blockers truthfully.
Options: $ARGUMENTS
