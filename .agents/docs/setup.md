# AI workflow setup

## Configuration map

| Location | Owns |
| --- | --- |
| `AGENTS.md` | Shared working agreements and workflow entrypoint |
| `.agents/docs/project.md` | Real service layout, architecture and test strategy |
| `.agents/docs/workflow.md` | Stage gates, review concurrency, commit/PR policy |
| `.agents/docs/review.md` | Shared code/security review contracts |
| `.agents/docs/evidence.md` | Candidate, quality and review evidence commands |
| `.agents/skills/` | Four stage skills (`ai-plan`, `ai-build`, `ai-verify`, `ai-commit`) and domain skills |
| `.agents/rules/` | Shared engineering, testing and security rules |
| `CLAUDE.md`, `.claude/settings.json` | Shared instruction import, Opus Plan / Sonnet execution, permissions |
| `.claude/agents/` | Two read-only Sonnet reviewers |
| `.claude/skills`, `.claude/rules/shared` | Per-skill links to `.agents/skills/*` and one shared rules link |
| `.codex/config.toml`, `.codex/agents/` | Codex Sol default and two Sol reviewers; select Astra for main-session Plan |
| `scripts/ai/quality.ts`, `candidate.ts` | Test/format evidence and candidate/review/staging checks |
| `.github/pull_request_template.md` | Shared PR report format |

Everything shared has one canonical source under `.agents/`; `.claude/` and
`.codex/` contain only provider settings, reviewer definitions and discovery links.
Codex discovers `.agents/skills/` natively. Claude discovers one directory symlink
per skill under `.claude/skills/`, using its documented symlink layout. When adding
or renaming a shared skill, update its Claude link; the configuration test checks
that both inventories match. Edit the canonical skill, never a separate copy.
File explorers may expand these links as if they were duplicate folders. They
resolve to the same files; do not delete them merely to hide that visual repetition.
Reviewer adapters retain each client's required format and model/tool settings;
their instructions point to the single contract in [review.md](review.md).

The four commands are `ai-plan`, `ai-build`, `ai-verify`, and `ai-commit`.
The prefix avoids shadowing native `/plan` and bundled `/verify`. Old unprefixed
stage skills were removed. Stage policy stays in [workflow.md](workflow.md).
Claude reads the skill's `model` and `disable-model-invocation` frontmatter;
Codex reads `agents/openai.yaml` for picker labels and explicit-only commit policy.
Neither metadata file can switch Codex's parent-session model or native mode.

## Native mode/model preflight

Before each stage, use the host's actual mode/model state when exposed. Do not
infer it from skill text, the requested model, or a read-only sandbox. If state
is unavailable or mismatched, tell the user the exact native control needed and
wait for confirmation rather than claiming the requested routing ran.

| Stage | Claude | Codex |
| --- | --- | --- |
| Plan | Native Plan mode, Opus (`opusplan`) | Native Plan mode, `gpt-6-astra` |
| Build / Verify / Commit | Outside Plan mode, Sonnet | Default mode, `gpt-6-sol` |

Mode changes must use the host's controls. Claude's `EnterPlanMode` tool can enter
Plan mode; call it before planning when available and Plan mode is not already active. Accepting/exiting
Plan uses the native Plan completion control (`ExitPlanMode` when available).
A skill's model override selects Claude's model during that skill; it does not
prove a permanent change to the session's default model.
Codex has no documented repository setting equivalent to `opusplan`: switching
Astra to Sol in this setup still needs the client model control. A custom App Server
integration can set `collaborationMode.settings.model` per turn, but is not installed
here and has not been tested across app/IDE surfaces. Do not add an invented
`plan_mode_model` key, a prompt pretending to be a mode change, or a wrapper
that merely changes the sandbox. No custom runtime or UI automation is installed.

## Claude Code

Start from the real checkout directory (on this machine,
`/Volumes/KHUONG/Repositories/MyAIAgent`):

```bash
npm ci --ignore-scripts
claude --model opusplan --permission-mode plan
```

Project settings select the same defaults. User, environment, organization and
CLI overrides can change the result. In an existing terminal session use
`/model opusplan`; `/plan` or Shift+Tab enters native Plan mode. In the Claude
IDE extension use its model and permission-mode controls.

1. `/ai-plan <task>` selects `opusplan` and requests native Plan mode if needed.
2. Accept/exit native Plan mode, then `/ai-build` selects Sonnet and runs Build.
3. `/ai-verify` selects Sonnet and dispatches both project reviewers concurrently.
4. `/ai-commit` selects Sonnet and follows the single delivery question and gates
   in [workflow.md](workflow.md).

Type `/ai-` to find the four commands. Skills are Claude's supported custom command
mechanism; a separate commands directory would duplicate discovery. Terminal and
IDE sessions must open this repository and trust it. A remote/cloud session must
have the repository's configuration in its checkout. A plain Claude chat does not
load these local files.

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

Use a current client supporting repository configuration, skills and custom agents.
The installed CLI checked on 2026-10-17 is **0.162.1**; Claude Code is **2.1.287**.
The bundled Codex model catalog includes both requested IDs. Account availability
still needs confirmation in the model picker. No global settings are modified.
Trust/open the real checkout to load `.codex/` and `.agents/skills/`.

In a terminal:

```bash
codex --model gpt-6-astra
```

Then use the native controls and repository skills in the composer:

```text
/plan
$ai-plan <task>
```

At the native Plan completion control, leave Plan mode. Use `/model` to select
`gpt-6-sol`, then invoke `$ai-build`, `$ai-verify`, or `$ai-commit` for the requested
stage. `/status` shows the current model; `/skills` lists the repository skills.
There are no repository-defined `/build`, `/verify` or `/commit` native commands.
The four `$ai-*` entries are the supported command interface for this project.

In Codex app/IDE, open this project, choose Astra in the model selector and enable
Plan mode in the composer, then select **AI Plan** in the skill picker (or invoke
`$ai-plan` where supported). After accepting the plan, choose Sol and Default mode
before **AI Build**, **AI Verify**, or **AI Commit**. CLI slash controls are not
available on every app surface. The shared skills and reviewer contracts stay the
same; UI model/mode controls belong to the host. Existing sessions may require a
reload/new chat after skill renames. A web chat without this checkout cannot load
repository-local skills.

`ai-commit` is explicit-only, so it may be absent from the automatic skill catalog;
invoke `$ai-commit` or select its skill file explicitly. It never auto-authorizes
Git writes. Fresh sessions need the plan and quality/review evidence from the
previous session. Project config defaults to Sol; that default does not change an
already-running session. Model/mode changes only count after client confirmation.

## Discovery troubleshooting

- Open the repository root, trust it, then restart/reload the client after renames.
- Check the four `ai-*` directories under `.agents/skills/` and their Claude links.
  Run `node --test --import tsx scripts/ai/shared-config.test.ts` for filesystem consistency;
  this test alone does not prove a client loaded a skill.
- Claude: type `/ai-`; inspect higher-priority personal/enterprise skills if a name
  resolves elsewhere. Do not start with skills disabled or safe mode enabled.
- Codex: use `/skills` or the skill picker; do not look for Claude-style `/ai-*`.
- On Windows enable Git symlink support/Developer Mode before cloning.
- This machine's old `/Users/kylepham184/Repositories/MyAIAgent` path is a symlink.
  Open `/Volumes/KHUONG/Repositories/MyAIAgent` directly: a sandbox writable root
  containing a symlink can fail before any command executes.

## Quality and troubleshooting

Requires .NET 10 SDK, Node.js 20.6+ and access to the solution's NuGet dependencies.

```bash
node --test --import tsx scripts/ai/*.test.ts
npm run quality
npm run quality -- --project Orchestrator
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

Checked 2026-10-17; installed-client capabilities and account model access may differ.

- [Codex configuration and profile migration](https://learn.chatgpt.com/docs/config-file/config-advanced)
- [Codex custom agents](https://learn.chatgpt.com/docs/agent-configuration/subagents)
- [Codex App Server mode/model controls](https://learn.chatgpt.com/docs/app-server#turns)
- [Codex native Plan command](https://learn.chatgpt.com/docs/developer-commands?surface=cli)
- [Codex local skill discovery](https://learn.chatgpt.com/docs/build-skills)
- [GPT-6 Sol model](https://developers.openai.com/api/docs/models/gpt-6-sol)
- [Claude opusplan model routing](https://code.claude.com/docs/en/model-config#opusplan-model-setting)
- [Claude subagents](https://code.claude.com/docs/en/sub-agents)
- [Claude skill symlinks](https://code.claude.com/docs/en/skills#where-skills-live)
- [Claude shared rules](https://code.claude.com/docs/en/memory#share-rules-across-projects-with-symlinks)
- [Claude permission rules and their limits](https://code.claude.com/docs/en/permissions)
