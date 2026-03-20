---
alwaysApply: true
description: >
  Defines when Claude should auto-save memory to preserve progress
  and learning across sessions. Never lose a correction twice.
---

# Memory Flush Rules

## Always Save After

- **User correction** — any time the user says "no", "don't do that", or fixes your approach
- **Confirmed approach** — user explicitly validates a non-obvious choice ("yes, exactly", "perfect")
- **New project pattern** — discovering something project-specific not captured in the rules
- **Architecture decision** — a choice was made that will affect future work

## Session End Flush

At the end of every session, check and save:

1. Any feedback or corrections from this session not yet in memory
2. Project state changes — new modules added, bugs found, decisions made
3. Reference pointers — new external systems, APIs, or docs discovered

## What NOT to Save

- In-progress task state — use `TodoWrite` for that (session-scoped)
- Code patterns derivable from reading the codebase
- Git history — `git log` is authoritative
- Anything already documented in `CLAUDE.md`
- Ephemeral context only relevant to this conversation

## Memory Types

| Type | Save when |
|---|---|
| `feedback` | User corrects or confirms a non-obvious approach |
| `project` | New goals, decisions, deadlines, or blockers |
| `user` | New information about the user's role or preferences |
| `reference` | New pointer to an external system or resource |
