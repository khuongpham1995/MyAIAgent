---
name: ai-verify
description: Run the read-only code-reviewer and security-reviewer concurrently, then the tech-lead result review, on the tested MyAIAgent candidate. Use for the Verify stage of the delivery workflow.
model: sonnet
---

First follow **Native mode/model preflight** in `.agents/docs/setup.md`.

Follow **Verify** in `.agents/docs/workflow.md` and `.agents/docs/evidence.md`.
Spawn the `code-reviewer` and `security-reviewer` project agents concurrently on
the same candidate and wait for both. After both PASS, run the `tech-lead` result
review alone. Return Summary / Changes / Risks.
