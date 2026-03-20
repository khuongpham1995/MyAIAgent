---
alwaysApply: true
description: >
  Development workflow rules: plan first, TDD, review every change, verify before done.
---

# Development Workflow Rules

## Plan First

- Enter plan mode for any non-trivial task (3+ steps or architecture decisions)
- Wait for user confirmation before implementing

## Test-Driven Development

- Write a failing test before production code
- RED → GREEN → REFACTOR — never skip the refactor phase

## Verify Before Done

- Run `dotnet build` and `dotnet test` after every change
- Fix build errors and test failures autonomously before surfacing to the user

## Stop and Re-plan

- If implementation goes sideways, STOP and re-plan
- Never push through a broken approach — the cost of a bad design compounds

## Learn From Corrections

- After any correction, save a memory entry so the same mistake doesn't recur
