---
description: >
  Create a layered implementation plan for a MyAIAgent feature before writing
  any code. Delegates to the planner agent. Waits for user confirmation.
  Trigger: "plan", "how would you implement", "what's the approach", "design".
---

# /plan — Implementation Planning

## What

Invokes the `planner` agent to analyse the codebase and produce a step-by-step plan across Clean Architecture layers. **No code is written until the user confirms.**

## When

- Any feature touching 3+ files
- New handler, tool, or feature
- Adding a new external integration
- Refactoring across layers
- Unsure of the right approach

## How

### Step 1 — Invoke Planner

Load `.claude/agents/planner.md` and follow its workflow:
1. Restate the requirement
2. Ask follow-up questions until >= 97% confidence
3. Map affected layers (Domain → Application → Infrastructure → Presentation → Tests)
4. Produce the plan

### Step 2 — Present Plan

Output the plan in this format:

```markdown
# Implementation Plan: [Feature Name]

## Summary
[2-3 sentences]

## Layers Affected
| Layer | File | Change |
|---|---|---|
| Domain | Constants/XConstant.cs | Add constants |
| Application | Handlers/X/XHandler.cs | New handler |
| Presentation | [endpoint or tool class] | New endpoint/method |
| Tests | Test/.../XHandlerTests.cs | New tests |

## Steps
1. ...
2. ...

## Risks
- ...
```

### Step 3 — Wait for Confirmation

**Do not write code.** Ask:

> "Does this plan look right? Reply 'yes' to proceed, or tell me what to change."

User responses:
- `yes` / `proceed` → start implementation
- `modify: [changes]` → revise plan and re-present
- `different approach: [alt]` → re-plan from scratch
