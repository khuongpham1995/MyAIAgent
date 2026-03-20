# .claude — Claude Code Configuration

This directory configures Claude Code for the MyAIAgent .NET solution.

## Structure

| Directory | Purpose |
|---|---|
| `agents/` | Specialized sub-agents invoked for specific tasks |
| `commands/` | Slash commands (`/plan`, `/tdd`, `/code-review`, etc.) |
| `hooks/` | Shell scripts triggered on tool use events |
| `instructions/` | Always-loaded instruction and standards files |
| `rules/common/` | Cross-cutting rules (git, testing, security, workflow) |
| `rules/dotnet/` | .NET / C#-specific coding standards and architecture |
| `skills/` | Domain context loaded on demand for specific areas |

## Quick Reference

| When | Invoke |
|---|---|
| New feature (3+ files) | `/plan` |
| New handler or tool | `/tdd` |
| After writing code | `/code-review` |
| Build or test failure | `/build-fix` |
| Before PR | `/test-coverage` |
