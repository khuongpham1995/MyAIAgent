# AI workflow setup

## Configuration map

| Location | Owns |
| --- | --- |
| `AGENTS.md` | Shared working agreements and workflow entrypoint |
| `.agents/docs/project.md` | Real service layout, architecture and test strategy |
| `.agents/docs/workflow.md` | Stage gates, review concurrency, commit/PR policy |
| `.agents/docs/review.md` | Shared code/security review contracts |
| `.agents/docs/evidence.md` | Candidate, quality and review evidence commands |
| `.agents/skills/` | Four stage skills (`plan`, `build`, `verify`, `commit`) and domain skills |
| `.agents/rules/` | Shared engineering, testing and security rules |
| `CLAUDE.md`, `.claude/settings.json` | Shared instruction import, Opus Plan / Sonnet execution, permissions |
| `.claude/agents/` | Two read-only Sonnet reviewers |
| `.claude/skills`, `.claude/rules/shared` | Symlinks to `.agents/skills` and `.agents/rules` |
| `.codex/config.toml`, `.codex/agents/` | Codex Sol default and two Sol reviewers; select Astra for main-session Plan |
| `scripts/ai/quality.mjs`, `candidate.mjs` | Test/format evidence and candidate/review/staging checks |
| `.github/pull_request_template.md` | Shared PR report format |

Everything shared has one canonical source under `.agents/`; `.claude/` and
`.codex/` hold only tool-specific settings, reviewer definitions and pointers.
Codex discovers `.agents/skills/` natively and reads shared Markdown rules as
directed by `AGENTS.md`. Claude does not read `.agents/`, so `.claude/skills` is a
symlink to `.agents/skills` and `.claude/rules/shared` a symlink to `.agents/rules`.
These are references, not copies; edit only the canonical files. Claude documents
symlinks for individual skill folders, not for the whole `skills` directory; the
directory link was checked on Claude Code 2.1.236 by loading a skill through it.
If a client stops following it, link each skill folder instead.

A skill or rule added under `.agents/` needs no extra link. Stage skills only point
to `.agents/docs/workflow.md`; change stage policy there, not in the skill. Their
`model` and `disable-model-invocation` frontmatter is read by Claude; Codex uses
`commit/agents/openai.yaml` to keep `commit` from running by description match.
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

The stage commands are the shared skills, invoked by name:

1. `/plan <task>` — enters native Plan mode and produces a concrete plan.
2. Use Claude's native Plan exit/acceptance control, then `/build` — Sonnet implements
   the available plan and runs tests/quality gates.
3. `/verify` — two read-only Sonnet reviewers run concurrently; owner consolidates.
4. `/commit` — after readiness checks, asks the delivery question defined in
   [workflow.md](workflow.md) and waits for the user's choice. Publishing requires
   GitHub CLI authentication and an unambiguous target.

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
$plan <task>
```

At the native Plan-to-Build transition, leave Plan mode using the client control,
select `gpt-6-sol` with `/model`, and run:

```text
$build
$verify
$commit
```

`$commit` uses the same delivery question as Claude. Use the host's
question UI when available and permitted, or a plain-text question otherwise;
wait for the answer.
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
