# Claude adapter

Shared policy: `AGENTS.md` and `docs/ai/`. Start with `docs/ai/setup.md`.

- `settings.json`: native Plan mode, `opusplan` model routing and native allow/ask/deny rules.
- `commands/`: only `plan`, `build`, `verify`, and `commit` stage entrypoints.
- `agents/`: only the read-only Sonnet `code-reviewer` and `security-reviewer`.
- `skills/<name>`: symlinks to the shared `.agents/skills/<name>` directories.
- `rules/shared`: symlink to `.agents/rules/`; no provider-specific rule copies.

Edit skills and rules at their canonical `.agents/` paths. Claude commands and
agent definitions remain here because they use Claude-specific tools and metadata.
Planning, implementation, test writing and build fixes run in the main session;
they do not require separate project agents or extra commands.

Verification is explicit through `node scripts/ai/quality.mjs`. Per-edit format and
restore hooks were removed: they used nonexistent solution paths and unsupported
input environment variables, hid failures, and depended on unpinned `npx tsx`.
Native permissions prompt for publication and deny common unsafe Git forms.
See `docs/ai/setup.md` for their limits and `docs/ai/evidence.md` for candidate checks.
