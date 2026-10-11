# AI workflow setup

## Configuration map

| Location | Owns |
| --- | --- |
| `AGENTS.md` | Shared working agreements and workflow entrypoint |
| `docs/ai/project.md` | Real service layout, architecture and test strategy |
| `docs/ai/workflow.md` | Stage gates, review concurrency, commit/PR policy |
| `docs/ai/review.md` | Shared code/security review contracts |
| `.codex/config.toml`, `.codex/agents/` | Codex Sol default and two Sol reviewers; select Astra for main-session Plan |
| `.agents/skills/` | Shared workflow and domain skills for both providers |
| `.agents/rules/` | Shared engineering, testing, security and delivery rules |
| `CLAUDE.md`, `.claude/settings.json` | Shared instruction import, Opus Plan / Sonnet execution |
| `.claude/commands/`, `.claude/agents/` | Four Claude stage commands and two Sonnet reviewers |
| `scripts/ai/quality.mjs`, `candidate.mjs` | Test/format evidence and candidate/review/staging checks |
| `.github/pull_request_template.md` | Shared PR report format |

Skills and rules have one canonical source under `.agents/`. Codex discovers
`.agents/skills/` natively and reads shared Markdown rules as directed by `AGENTS.md`.
Claude discovers each skill through `.claude/skills/<name>` symlinks and loads rules
through `.claude/rules/shared`, pointing to `.agents/rules/`. These are references,
not copies; edit only the canonical files. Model/tool metadata remains provider-specific.

When adding a shared skill, also add a relative `.claude/skills/<name>` symlink to
`../../.agents/skills/<name>`. Rules added under `.agents/rules/` need no extra link.
Run `node --test scripts/ai/*.test.mjs` to check discovery links and references.
On Windows, enable Developer Mode and Git symlink support before checkout; a clone
that materializes symlinks as text files cannot load these adapters correctly.

## Claude Code

From the repository root:

```bash
claude --model opusplan --permission-mode plan
```

Project settings also select these defaults. `opusplan` uses Opus in native Plan
mode and Sonnet after leaving it. User, environment, organization or CLI overrides
may change model selection; check the active session model instead of assuming.

1. `/plan <task>` — enters native Plan mode and produces a concrete plan.
2. Use Claude's native Plan exit/acceptance control, then `/build` — Sonnet implements
   the available plan and runs tests/quality gates.
3. `/verify` — two read-only Sonnet reviewers run concurrently; owner consolidates.
4. `/commit` — after readiness checks, show **Just commit only** / **Commit + PR**
   and wait for the user's choice. `/commit --pr` already selects the latter:
   push a task branch and open a draft PR (requires GitHub CLI authentication and
   an unambiguous target). An explicit choice already made is not asked again.

The only project subagents are `code-reviewer` and `security-reviewer`. They
explicitly declare `model`; they do not inherit Opus by accident. Plan
permission mode and restricted tool lists reduce accidental edits. Shell-capable
reviewers still rely on native permissions and the read-only contract; this is not
a hardened arbitrary-command sandbox. No experimental agent-team flag is needed.

Project permissions ask before ordinary `git commit`, `git push` and `gh pr create`.
Deny rules reject common force-push, bulk staging, amend, auto-stage commit and
hook-bypass forms. The quality/candidate runners and exact owning-project test
commands are allowed. Additional dotnet arguments still use the native permission
flow. These scripts run repository code, so allow them only in a trusted checkout.
Deny takes precedence over ask, then allow. Bash patterns match command text, not
all equivalent invocations (aliases, absolute paths, `git -C`, combined flags or
scripts can differ). These rules reduce accidental operations; they do not prove
test/review completion or replace the sandbox. Use the ordinary documented commands,
never alternative syntax to evade a permission rule. See the official permission
reference below. Native prompts are separate from user task authorization.

## Codex

Use a current Codex client supporting repository config, local skills, native
`/plan` and custom subagents. The CLI found during setup was **0.44.0**; it is too
old to validate this configuration end to end. Upgrade through your normal Codex
installation channel before using this workflow. No global settings are modified.
Trust the repository through the client's normal trust flow to load `.codex/`.

Start planning with the requested Astra family:

```bash
codex --model gpt-6-astra
```

In that session:

```text
/plan
$ai-workflow plan <task>
```

At the native Plan-to-Build transition, leave Plan mode using the client control,
select `gpt-6-sol` with `/model`, and run:

```text
$ai-workflow build
$ai-workflow verify
$ai-workflow commit
```

`$ai-workflow commit` uses the same delivery question as Claude. Use the host's
question UI when available and permitted, or a plain-text question otherwise;
wait for the answer. `$ai-workflow commit --pr` preselects **Commit + PR**.
In an app, select Astra/Sol and
Plan mode using the model/mode UI when CLI slash commands are unavailable. A prompt
or skill cannot change the active model or native collaboration mode itself. Do not
claim those switched unless the client confirms it. If the model is unavailable,
report that and choose a replacement only with the user's direction.

The project defaults to `gpt-6-sol`; it deliberately preserves the requested Sol
model rather than silently selecting a newer variant. Planning runs in the main
session with Astra and native Plan mode; no separate planner agent is configured.
Fresh sessions must receive the concrete plan and test/review evidence explicitly.

No legacy `[profiles.*]` blocks are used. Current Codex (0.134.0+) resolves named
profiles from user-level `~/.codex/<name>.config.toml`; project config cannot define
them. Explicit CLI/UI model selection keeps this setup repository-local.

## Quality and troubleshooting

Requires .NET 10 SDK, Node.js 20+ and access to the solution's NuGet dependencies.

```bash
node --test scripts/ai/*.test.mjs
node scripts/ai/quality.mjs
node scripts/ai/quality.mjs --project Orchestrator
```

The quality runner executes sequentially, stops on the first failure, never formats
source, and stores a fresh JSON report plus per-service TRX files in `artifacts/ai/<run>/`.
Console output contains compiler/test/format diagnostics. An interrupted report
remaining RUNNING is incomplete. Tests of the runner use fake tool results and do
not replace running the .NET suite. Counts come from fresh TRX counters; missing,
empty or inconsistent output fails the gate. Candidate edits during checks fail it.

Use [evidence.md](evidence.md) for the exact review and pre-commit check commands.
Format is limited to candidate C# files; use `dotnet format src/MyAIAgent.slnx
--no-restore --verify-no-changes` separately for a whole-solution baseline when needed.
If restore needs network/credentials, report the actual blocker. Existing formatting
drift or dependency vulnerabilities remain visible; don't fix unrelated code or
suppress diagnostics to make the workflow green. A quality PASS does not authorize
commit. Scripts verify local evidence but do not install Git hooks, authenticate
reviewers, prove concurrency, or enforce server-side branch protections.

## Official references

Checked 2026-10-11; installed-client capabilities and account model access may differ.

- [Codex configuration and profile migration](https://learn.chatgpt.com/docs/config-file/config-advanced)
- [Codex custom agents](https://learn.chatgpt.com/docs/agent-configuration/subagents)
- [Codex native Plan command](https://learn.chatgpt.com/docs/developer-commands?surface=cli)
- [Codex local skill discovery](https://learn.chatgpt.com/docs/build-skills)
- [GPT-6 Sol model](https://developers.openai.com/api/docs/models/gpt-6-sol)
- [Claude opusplan model routing](https://code.claude.com/docs/en/model-config#opusplan-model-setting)
- [Claude subagents](https://code.claude.com/docs/en/sub-agents)
- [Claude skill symlinks](https://code.claude.com/docs/en/skills#where-skills-live)
- [Claude shared rules](https://code.claude.com/docs/en/memory#share-rules-across-projects-with-symlinks)
- [Claude permission rules and their limits](https://code.claude.com/docs/en/permissions)
