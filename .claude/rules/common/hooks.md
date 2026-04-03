---
description: >
  Documents the Claude Code hooks configured for MyAIAgent: format on edit,
  scaffold restore, Bash command guard, and anti-pattern detection.
---

# Hooks

Hooks live in `.claude/hooks/` and are wired in `settings.json`.

## PreToolUse

### `pre-bash-guard.ts`

- **Trigger:** Every `Bash` tool call
- **Purpose:** Blocks destructive commands (`rm -rf`, `git reset --hard`, etc.)
- **On violation:** Exits non-zero — tool call is cancelled

### `pre-commit-antipattern.ts`

- **Trigger:** Every `Bash` tool call
- **Purpose:** Detects anti-patterns in staged `.cs` files (`DateTime.UtcNow`, `new HttpClient()`, `async void`, `.Result`, interpolated logging, `IMediator`)
- **On violation:** Exits non-zero — tool call is cancelled. No-op when no `.cs` files are staged.

## PostToolUse

### `post-edit-code-review.ts`

- **Trigger:** After every `Edit` or `Write` tool call
- **Purpose:** Reminds Claude to invoke the `code-reviewer` agent after modifying a production `.cs` file
- **Skips:** Test projects (`.Test/`), `obj/`, `bin/` — no noise during TDD cycles
- **Effect:** Outputs a message Claude Code surfaces as hook feedback; Claude defers the review until editing is complete

### `post-edit-format.ts`

- **Trigger:** After every `Edit` or `Write` tool call
- **Purpose:** Runs `dotnet format` on the modified `.cs` file
- **Effect:** Keeps formatting clean without a manual format step

### `post-scaffold-restore.ts`

- **Trigger:** After every `Edit` or `Write` tool call
- **Purpose:** Runs `dotnet restore` if a `.csproj` file was added or modified
- **Effect:** Ensures new packages are restored immediately after scaffold
