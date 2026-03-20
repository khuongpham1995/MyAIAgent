---
alwaysApply: true
description: >
  Security rules for MyAIAgent: no hardcoded secrets, validated inputs,
  safe HTTP configuration, and secure logging practices.
---

# Security Rules

## Secrets & Configuration

- **Never hardcode secrets, API keys, or connection strings** — use configuration (`appsettings.json` + environment variables or Azure Key Vault).
- **Never log sensitive values** — no customer PII, no auth tokens, no API keys in log output.
- **Never commit `appsettings.Development.json` with real secrets** — use user secrets (`dotnet user-secrets`) locally.

```csharp
// DO — read from configuration
var baseUrl = configuration["MockApi:BaseUrl"]
    ?? throw new InvalidOperationException("MockApi:BaseUrl is required");

// DON'T — hardcode
var baseUrl = "https://api.example.com/secret-key-here";
```

## Input Validation

- **Validate all tool input at the Application boundary** via FluentValidation.
- **Do not trust LLM-supplied strings** without validation — treat them the same as user input.
- **Never pass raw input to shell commands** — command injection applies even in .NET.

## HTTP & External APIs

- **Always use HTTPS** for external API base URLs in production configuration.
- **Do not disable SSL certificate validation** (`ServerCertificateCustomValidationCallback = (_, _, _, _) => true` is forbidden outside test code).
- **Do not expose internal error details** from external APIs — log them, return a safe message.

```csharp
// DO — safe error surfacing
catch (HttpRequestException ex)
{
    logger.LogError(ex, "External API error for {Entity}", typeof(T).Name);
    return Result.Failure<T>("Service temporarily unavailable");
}

// DON'T — leak internal details
return Result.Failure<T>(ex.Message);  // may contain URLs, IPs, stack traces
```

## Logging

- **Use structured logging parameters**, not string interpolation.
- **Do not log request/response bodies** for external APIs unless explicitly needed and PII-scrubbed.

```csharp
// DO
logger.LogInformation("Processing request for customer {CustomerId}", customerId);

// DON'T
logger.LogInformation($"Processing request for customer {customer.Email} at {DateTime.Now}");
```

## CancellationToken & Timeouts

- Always propagate `CancellationToken` — dropped tokens allow runaway requests.
- Ensure `HttpClient` has a reasonable timeout (default 100s is too long for most MCP tool calls).

## Security Checklist Before Commit

- [ ] No hardcoded secrets or API keys
- [ ] No disabled SSL validation
- [ ] All command/query inputs have FluentValidation rules
- [ ] External errors caught and sanitized before returning
- [ ] No PII in log messages
- [ ] No `Console.WriteLine` left in production code
