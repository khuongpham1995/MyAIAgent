---
alwaysApply: true
description: >
  General coding principles that apply across all code in MyAIAgent.
---

# Common Coding Style

## Naming

- Descriptive names over abbreviations — `cancellationToken` not `ct`, `customer` not `c`
- Async operations must end with `Async` — `GetCustomersAsync`, not `GetCustomers`
- Boolean names read as assertions — `isReady`, `hasErrors`, `canDelete`

## Comments

- Comment **why**, never **what** — the code shows what; comments explain motivation
- No commented-out code — delete it; git has history

## Functions & Methods

- Single responsibility — one function does one thing
- No side effects in query methods
- Early returns over deeply nested if-else
