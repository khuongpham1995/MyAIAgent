---
alwaysApply: true
description: >
  Security rules for MyAIAgent: no hardcoded secrets, validated inputs,
  safe HTTP configuration, and secure logging practices.
---

# Security Rules

## Secrets & Configuration

- Never hardcode secrets, API keys, or connection strings — use `appsettings.json` + environment variables or Azure Key Vault
- Never log sensitive values — no PII, no auth tokens, no API keys
- Never commit `appsettings.Development.json` with real secrets — use `dotnet user-secrets` locally
- Always read required config values with `?? throw new InvalidOperationException("X is required")`

## Input Validation

- Validate all tool and command input at the Application boundary via FluentValidation
- Treat LLM-supplied strings the same as user input — never trust without validation
- Never pass raw input to shell commands — command injection applies in .NET too

## HTTP & External APIs

- Always use HTTPS for external API base URLs in production configuration
- Never disable SSL certificate validation (`ServerCertificateCustomValidationCallback`) outside test code
- Never expose internal error details from external APIs — log them, return a safe generic message

## Logging

- Always use structured logging parameters, not string interpolation: `LogInformation("Customer {Id}", id)`
- Never log request/response bodies for external APIs unless explicitly needed and PII-scrubbed

## Timeouts

- Always propagate `CancellationToken` — dropped tokens allow runaway requests
- Set a reasonable `HttpClient` timeout — the default 100s is too long for most service-to-service calls

## Checklist Before Commit

- [ ] No hardcoded secrets or API keys
- [ ] No disabled SSL validation
- [ ] All command/query inputs have FluentValidation rules
- [ ] External errors caught and sanitized before returning
- [ ] No PII in log messages
- [ ] No `Console.WriteLine` left in production code
