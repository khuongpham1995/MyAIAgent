---
name: ai-workflow
description: Run MyAIAgent delivery stages (plan, build, verify, commit, optional PR) using the repository's shared quality gates and parallel code/security review. Use when working on a MyAIAgent change or explicitly invoking a stage.
---

# MyAIAgent delivery

Read `AGENTS.md`, `docs/ai/project.md`, then the requested stage in
`docs/ai/workflow.md`. All paths are relative to the repository root.

- `plan <task>`: native Plan mode with Opus (Claude) or Astra (Codex); read-only scope, files, acceptance
  criteria, test cases and risks. Use the client controls in `docs/ai/setup.md`.
- `build`: Sonnet (Claude) or Sol (Codex) implements the available plan and runs meaningful tests plus
  `node scripts/ai/quality.mjs`. Use `--project <Service>` for targeted iteration only.
  A fresh session needs the concrete plan first. See `docs/ai/evidence.md` for artifacts.
- `verify`: on Sonnet/Sol, explicitly spawn `code-reviewer` and `security-reviewer`
  concurrently, using their project custom agent roles and the same candidate,
  scope and evidence. Pass both the candidate digest and quality SHA-256; preserve
  their returned records using `docs/ai/evidence.md`. Wait for both; no edits during review. Use `docs/ai/review.md`.
- `commit [--pr]`: Sonnet/Sol follows the shared Commit gate and stages only task-owned
  changes. Only an explicit user commit request authorizes Git mutation.
  After readiness checks, use the shared delivery question: **Just commit only** or
  **Commit + PR**. Wait for the choice unless the user already selected an option;
  `--pr` selects **Commit + PR**. Only that option or another explicit publication
  request authorizes push and draft PR.
  Automatic skill selection grants no authorization.
  Run the evidence checker with both reviewer records and `--staged` before commit.

Do not claim this skill switches native mode/model. If the client has not selected
Opus/Astra for Plan or Sonnet/Sol for execution, explain the needed client selection. Never
silently substitute a model or pretend unavailable parallel review succeeded.
Use Summary / Changes / Risks for every handoff, including actual validation results.
