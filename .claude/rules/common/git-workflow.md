---
alwaysApply: true
description: >
  Enforces conventional commits, branch naming, and atomic commit practices
  for the MyAIAgent repository.
---

# Git Workflow Rules

## Commit Messages

Use conventional commit prefixes:

| Prefix | Use for |
|---|---|
| `feat:` | New MCP tool, handler, or capability |
| `fix:` | Bug fix |
| `refactor:` | Code restructure without behavior change |
| `test:` | Adding or updating tests |
| `docs:` | CLAUDE.md, README, skill files |
| `chore:` | Build config, package updates, CI |

- **Write the "why", not the "what"** — the diff shows what changed; the message explains motivation.
- **No vague messages** — "fix bug", "update code", "WIP" are not acceptable.

```
# DO
feat: add delete_customer MCP tool with validation
fix: prevent double-upload of KernelMemory documents on retry
refactor: extract retry logic into RetryExtension to remove duplication

# DON'T
update stuff
fix
WIP
```

## Branch Naming

```
feature/add-rag-module
feature/document-upload-tool
fix/kernel-memory-polling-timeout
refactor/extract-api-client-base
```

## Atomic Commits

- One logical change per commit — feature + its tests belong in the same commit.
- **Don't** bundle unrelated changes (e.g., a bug fix + a new feature).
- **Don't** force-push to `main`.
- **Don't** skip pre-commit hooks with `--no-verify`.

## Pre-PR Checklist

Run `/verify` before creating a PR:
1. `dotnet build` passes
2. `dotnet test` — all green
3. `dotnet format --verify-no-changes` — no drift
4. No anti-patterns in staged files
