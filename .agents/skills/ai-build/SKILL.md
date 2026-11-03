---
name: ai-build
description: Implement the approved MyAIAgent plan with tests and run the project quality gate. Use for the Build stage of the delivery workflow.
model: sonnet
---

First follow **Native mode/model preflight** in `.agents/docs/setup.md`.

Follow **Build** in `.agents/docs/workflow.md`, using the plan in this conversation
or the handoff the user names. Do not commit or publish in this stage.
Delegate only independent, file-owned work under `.agents/docs/build-workers.md`.
The main session integrates worker changes and runs the full quality gate.
Return Summary / Changes / Risks.
