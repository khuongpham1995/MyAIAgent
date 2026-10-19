# Dependency maintenance

The solution targets .NET 10 and retains the KernelMemory service. Package versions are pinned in the owning `.csproj` files; the Aspire SDK is pinned in `MyAIAgent.All.csproj`.

## Upgrade baseline

Checked against NuGet on 2026-10-19. The selected direct packages use MIT, Apache-2.0, or BSD-3-Clause licenses. The project avoids upgrades that introduce commercial licensing requirements or reciprocal licensing terms.

| Area | Selected versions | Decision |
| --- | --- | --- |
| Aspire | 13.6.1 | Update AppHost SDK; pin MessagePack 2.5.303 to patch its inherited dependency. |
| .NET / ASP.NET Core / EF Cosmos | 10.0.13 | Align with the .NET 10 package line. Cosmos containers and partition keys remain unchanged. |
| Azure App Configuration | 8.6.0 | Keep both provider packages aligned. |
| Microsoft Agents / Extensions.AI | 1.25.0 / 10.11.0 | Use stable SDKs; preserve the existing `IChatClient` orchestration. |
| MCP | 2.2.0 | Align client and server. Use `McpClient` and `HttpClientTransport`; clients auto-detect Streamable HTTP or legacy SSE. The local server uses Streamable HTTP at `/mcp` and retains its root endpoint for existing agents. Legacy SSE serving remains disabled by the SDK default. |
| FluentValidation | 12.2.0 | The .NET 10 target meets its .NET 8 minimum. |
| MediatR | 12.5.0 | Retain the Apache-2.0 release rather than adopting newer licensing terms. |
| FluentAssertions | 6.12.2 | Apply the patch within the Apache-2.0 line; do not move to commercial-license releases. |
| OpenAPI | Microsoft.OpenApi 2.12.2 | ASP.NET Core OpenAPI 10.0.13 requires `>= 2.12.2, < 3.0.0`; do not independently upgrade to 3.x. |
| Test tooling | Test SDK 18.11.0, xUnit 2.9.3, VS runner 4.0.1, coverlet 10.1.0, Moq 4.21.0 | Retain xUnit v2 and VSTest/TRX evidence. Runner 4 supports v2 tests. |
| KernelMemory | 0.98.250508.3 | Retain the latest published service package as requested; NuGet marks it unmaintained. |

Azure.Identity, Azure.AI.OpenAI, Mapster, and Scrutor already match the selected stable releases. The unused MCP references to DotMarkdown, UglyToad.PdfPig, and the old KernelMemory test package were removed. The KernelMemory service reference remains in its Infrastructure project.

## KernelMemory security overrides

KernelMemory 0.98 retains old transitive dependencies. Its Infrastructure project explicitly pins:

- `Microsoft.SemanticKernel.Core` 1.71.0, the first patched version for [GHSA-2ww3-72rp-wpp4](https://github.com/advisories/GHSA-2ww3-72rp-wpp4). Keep the override compatible with KernelMemory's existing connectors.
- `MongoDB.Driver` 3.12.0, which brings patched SharpCompress and Snappier dependencies instead of independently replacing their APIs underneath an old driver.
- `Microsoft.Data.SqlClient` 6.1.7 and `System.Security.Cryptography.Pkcs` 10.0.13, replacing the inherited vulnerable cryptography dependency.

These drivers are dependencies of KernelMemory's package graph; the configured application still uses Azure AI Search and Cosmos. The overrides do not add MongoDB or SQL resources.

The Aspire project separately pins MessagePack 2.5.303 for [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h), retaining the existing 2.5 API line.

## Validation and future updates

Run `npm run quality` and `dotnet list src/MyAIAgent.slnx package --vulnerable --include-transitive --no-restore` after restoring updates. Keep advisory warnings visible. Dependabot ignores major upgrades for MediatR and FluentAssertions to preserve the selected license lines. Review licenses as well as API compatibility before changing those constraints.

Regression tests exercise the real MCP SDK against an in-memory HTTP server, unauthorized service requests, MCP tool lifetime/cancellation, and KernelMemory's Azure OpenAI connector against a fake streaming HTTP response. They do not prove live Azure ingestion, retrieval, or Cosmos persistence behavior. Run those integration checks against test resources before deploying.

## Package references

- [KernelMemory service package](https://www.nuget.org/packages/Microsoft.KernelMemory.Service.AspNetCore/0.98.250508.3)
- [MediatR 12.5.0](https://www.nuget.org/packages/MediatR/12.5.0)
- [FluentAssertions 6.12.2](https://www.nuget.org/packages/FluentAssertions/6.12.2)
- [ASP.NET Core OpenAPI 10.0.13](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.13)
