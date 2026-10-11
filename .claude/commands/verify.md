---
description: Run read-only Sonnet code and security reviewers concurrently on the tested candidate.
model: sonnet
---

Follow Verify in `docs/ai/workflow.md`. Confirm Build evidence first. Launch
`code-reviewer` and `security-reviewer` together in parallel using Agent calls;
pass both the same quality report, candidate digest, quality SHA-256, scope and plan.
Use `docs/ai/evidence.md` to check freshness and preserve their returned JSON records.
Wait for both. Never run mutating helpers alongside them. Consolidate findings
and return Summary / Changes / Risks; missing review or failed gates are BLOCKED.
Scope: $ARGUMENTS
