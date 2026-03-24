Run `code-reviewer`, `security-reviewer`, and `build-error-resolver` in parallel against the full solution. Collect all results and present a unified report.

## Steps

Launch all three agents simultaneously using `run_in_background: true` in a single response:

1. **code-reviewer** — reviews Clean Architecture compliance, MCP patterns, naming conventions, DI, and anti-patterns across all C# source files in `src/`
2. **security-reviewer** — scans for secrets, disabled SSL, missing input validation, information leakage, unsafe HTTP config, and missing CancellationToken propagation across all C# source files in `src/`
3. **build-error-resolver** — runs `dotnet build src/MyAIAgent.sln` and `dotnet test src/MyAIAgent.sln`; fixes any failures autonomously before reporting

Wait for all three to complete, then present a single unified report with sections for Build, Security, and Code Quality — each showing severity-ranked findings with file and line references.
