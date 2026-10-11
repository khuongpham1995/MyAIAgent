---
name: commit
description: Commit a verified MyAIAgent task after asking the user how to deliver it. Run only when the user asks to commit.
model: sonnet
disable-model-invocation: true
---

Follow **Commit and optional PR** in `.agents/docs/workflow.md` and the commit
check in `.agents/docs/evidence.md`. Only the user's own request authorizes Git
changes; without it, report readiness only. Return Summary / Changes / Risks.
