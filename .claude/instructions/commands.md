---
applyTo: '**'
---

# Available Commands

| Command | Purpose | When to use |
|---|---|---|
| `/plan` | Plan a feature with the planner agent | Any task touching 3+ files |
| `/tdd` | Test-driven workflow: failing test first | Adding handlers, tools, validators |
| `/code-review` | Review C# changes against project rules | After writing or refactoring code |
| `/build-fix` | Autonomously fix build and test failures | `dotnet build` or `dotnet test` fails |
| `/test-coverage` | Build → tests → anti-patterns → format | Before every PR |
