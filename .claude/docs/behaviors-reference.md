# Behaviors Reference

Extended reference for common development scenarios in MyAIAgent.

## Adding a New MCP Tool — Full Checklist

1. Add name + description constants to `McpServerConstant.cs` (Domain)
2. Define `IRequest<ResponseModel<T>>` record in `Application/`
3. Implement `IRequestHandler<,>` in `Application/Handlers/`
4. Add `AbstractValidator<T>` if the command accepts user input
5. Implement infrastructure in `Infrastructure/` if new external dependency
6. Add `[McpServerTool]` method in `Presentation/McpTools/`
7. Write unit tests in `MyAIAgent.McpServer.Test/` mirroring source layout

## Handling Build Failures

1. Run `dotnet build` and capture the full error output
2. Categorize: missing `using`, ambiguous type, null safety, interface mismatch, missing project ref
3. Read the failing file — never guess from the error message alone
4. Apply the minimal fix — do not refactor surrounding code
5. Rebuild to verify — max 3 attempts before escalating to user

## Handling Test Failures

1. Run `dotnet test --verbosity normal` to get full failure output
2. Read the failing test — understand what it asserts
3. Determine: is the test wrong, or is the production code wrong?
4. Fix the right thing — never modify a test to make it pass unless the test itself is the bug
5. Re-run to confirm green

## HTTP Client Setup Pattern

```
1. Define interface method in IApiClient<T> if new operation needed
2. Register in Infrastructure/Extensions/IoCExtension.cs with AddHttpClient + AddRetryPolicy
3. Inject IApiClient<T> via primary constructor — never new HttpClient()
4. Load `resilience` skill for full retry configuration details
```

## When to Use Result vs Exception

| Situation | Use |
|---|---|
| Entity not found | `Result.Failure<T>("not found")` |
| Validation failed | FluentValidation via pipeline (automatic) |
| External API down | Catch `HttpRequestException`, return `Result.Failure` |
| Programming error (null ref, index OOB) | Let propagate — fix the bug |
| Truly unexpected crash | Let propagate to global handler |
