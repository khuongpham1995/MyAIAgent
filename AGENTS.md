# MyAIAgent working agreements

- Proceed autonomously on well-scoped requests; verify before handing work back.
- Ask only when ambiguity materially affects implementation, safety, cost, or behavior.
- Preserve user changes and unrelated worktree state. Make focused, maintainable changes.
- Write source, comments, documentation, commit messages, and product copy in English.
  Conversation may be in Vietnamese. Lead with outcomes and keep updates concise.
- Read `docs/ai/project.md` before implementation and `docs/ai/workflow.md` for delivery.
- Use Plan → Build/test → Verify → Commit, with PR creation only when requested.
  A plan-only request ends after planning; a build request does not authorize publication.
- At Verify, explicitly spawn the code-reviewer and security-reviewer concurrently.
  Both are read-only. Wait for both; never run a build fixer alongside reviewers.
- Report Summary / Changes / Risks with actual test evidence and unresolved limitations.

Codex setup and model/mode selection: `docs/ai/setup.md`.
Shared skills live in `.agents/skills/`; shared rules live in `.agents/rules/`.
Read `.agents/rules/common/*.md` and `.agents/rules/skill-triggers.md` before coding.
For C# or project-reference changes, also read the relevant `.agents/rules/dotnet/*.md`.
Apply `.agents/rules/memory-flush.md` when preserving durable decisions.
Codex discovers shared skills natively; it loads these Markdown rules through this
instruction, not a `.codex/rules` policy file. Claude discovers the same files via
symlinks under `.claude/skills/` and `.claude/rules/shared/`. Edit canonical `.agents/`
files only. Source code and project files resolve stale examples in references.
