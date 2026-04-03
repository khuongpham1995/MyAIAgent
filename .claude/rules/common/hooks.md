---
alwaysApply: false
description: >
  Documents the Claude Code hooks configured for MyAIAgent: format on edit,
  scaffold restore, and Bash command guard.
---

# Hooks

Hooks live in `.claude/hooks/` and are wired in `settings.json`.

## PreToolUse

### `pre-bash-guard.ts`

- **Trigger:** Every `Bash` tool call
- **Purpose:** Blocks destructive commands (`rm -rf`, `git reset --hard`, etc.)
- **On violation:** Exits non-zero — tool call is cancelled

## PostToolUse

### `post-edit-format.ts`

- **Trigger:** After every `Edit` or `Write` tool call
- **Purpose:** Runs `dotnet format` on the modified `.cs` file
- **Effect:** Keeps formatting clean without a manual format step

### `post-scaffold-restore.ts`

- **Trigger:** After every `Edit` or `Write` tool call
- **Purpose:** Runs `dotnet restore` if a `.csproj` file was added or modified
- **Effect:** Ensures new packages are restored immediately after scaffold
