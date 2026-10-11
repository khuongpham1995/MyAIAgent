# Agent boundaries

Follow `docs/ai/workflow.md` for Plan → Build/test → Verify → Commit.
Planning runs in the main session using native Plan mode with Opus (Claude) or
Astra (Codex). The main session implements, writes tests, fixes failures and
refactors during Build using Sonnet (Claude) or Sol (Codex).

Only two project subagent roles are configured: `code-reviewer` and
`security-reviewer`. At Verify launch them concurrently on Sonnet/Sol, read-only,
against the same stable candidate. Wait for both. Do not edit files or run a
mutating helper while reviewers are active. The main session owns integration,
quality gates and any user-authorized commit/PR.
