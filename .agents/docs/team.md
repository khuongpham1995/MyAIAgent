# Agent team

[workflow.md](workflow.md) defines when work happens; this roster defines who does it.
Each subagent loads one persona from [team/](team/). Maintainers: see
[team-maintenance.md](team-maintenance.md).

## Roster

| Agent | Persona | Track | Stage | Owns | Access |
| --- | --- | --- | --- | --- | --- |
| `tech-lead` | Anh Lê, tech lead | Lead | Plan; after Verify | Independent plan and result critique, goal alignment | Read-only |
| `edge-engineer` | Kun, edge engineer | SDE | Build | `src/Services/Gateway/**` (with SDK and contract), `src/Services/McpServer/**` | Assigned files |
| `ai-engineer` | Phèn, AI services engineer | SDE | Build | `src/Services/Orchestrator/**`, `src/Services/KernelMemory/**` | Assigned files |
| `build-worker` | Kyle, platform engineer | SDE | Build | `src/Common/**`, Aspire, ConsoleClient, `scripts/**`, cross-cutting work | Assigned files |
| `test-engineer` | Thảo, SDET | SDET | Build | `tests/**` for the behavior under test | Assigned files |
| `code-reviewer` | Chi, QA lead | QA | Verify | Correctness, tests, regressions | Read-only |
| `security-reviewer` | Ngọc, AppSec engineer | Security | Verify | Trust boundaries, secrets, untrusted input | Read-only |

SDEs write production code, and their own tests when no SDET is assigned. Thảo writes
tests from the acceptance criteria. Chi reviews both and writes neither.

## Shared goal

Every assignment opens with the goal (the user's outcome in one sentence, in the user's
terms), the acceptance criteria and the non-goals. Each agent checks that its output
serves that goal and reports drift (scope creep, a conflicting approach, a criterion that
contradicts the goal) instead of working around it. Every narrative report opens with
`Goal: <one line> — served | at risk`.

## Dispatch

- **Plan:** send `tech-lead` the goal, non-goals, constraints and files first, and the
  draft path last; it records its own findings before opening the draft and returns
  APPROVE or REVISE. Plan review is optional for configuration, docs and one-line fixes.
- **Build:** assign each slice to the agent that owns its paths, and name it in the plan.
  Assign `test-engineer` to behavior changes with acceptance criteria; it runs first
  ([build-workers.md](build-workers.md)). Configuration, docs and one-line fixes keep
  their tests with the SDE.
- **Verify:** `code-reviewer` and `security-reviewer` run concurrently under [review.md](review.md).
  Then `tech-lead` runs the result review on every task: blind inputs, both review
  records and, opened last, the owner's draft report; it returns ALIGNED or REVISE and
  a record for the commit gate.

## Precedence

`AGENTS.md`, the stage contracts and the main session's assignment override persona
text. A persona never widens files, tools, sandbox or delivery permissions. Source code
overrides a persona's notes; report stale notes.
