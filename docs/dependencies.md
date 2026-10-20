# Dependency maintenance

Maintain the .NET 10 package baseline without changing the [runtime architecture](architecture.md). Versions are pinned in `.csproj` files, including the Aspire SDK in `MyAIAgent.All.csproj`.

## Upgrade baseline

Baseline checked on 2026-10-19. Selected direct packages use MIT, Apache-2.0, or BSD-3-Clause licenses. Keep upgrades within the project's free, permissive license policy.

| Area | Selected versions | Decision |
| --- | --- | --- |
| Aspire | 13.6.1 | Update AppHost SDK; pin MessagePack 2.5.303 to patch its inherited dependency. |
| .NET / ASP.NET Core / EF Cosmos | 10.0.13 | Align with the .NET 10 package line. Cosmos containers and partition keys remain unchanged. |
| Azure App Configuration | 8.6.0 | Keep both provider packages aligned. |
| Microsoft Agents / Extensions.AI | 1.25.0 / 10.11.0 | Use stable SDKs; preserve the existing `IChatClient` orchestration. |
| MCP | 2.2.0 | Align client/server. Clients detect Streamable HTTP or legacy SSE; local serving uses Streamable HTTP at `/mcp` and `/`, with legacy SSE disabled. |
| FluentValidation | 12.2.0 | The .NET 10 target meets its .NET 8 minimum. |
| MediatR | 12.5.0 | Retain the Apache-2.0 release rather than adopting newer licensing terms. |
| FluentAssertions | 6.12.2 | Apply the patch within the Apache-2.0 line; do not move to commercial-license releases. |
| OpenAPI | Microsoft.OpenApi 2.12.2 | ASP.NET Core OpenAPI 10.0.13 requires `>= 2.12.2, < 3.0.0`; do not independently upgrade to 3.x. |
| Test tooling | Test SDK 18.11.0, xUnit 2.9.3, VS runner 4.0.1, coverlet 10.1.0, Moq 4.21.0 | Retain xUnit v2 and VSTest/TRX evidence. Runner 4 supports v2 tests. |
| KernelMemory | 0.98.250508.3 | Retain the latest published service package as requested; NuGet marks it unmaintained. |

Azure.Identity, Azure.AI.OpenAI, Mapster, and Scrutor remain on the selected stable versions. KernelMemory remains in its Infrastructure project.

## KernelMemory security overrides

Keep these explicit overrides compatible with KernelMemory's older dependency graph:

| Package | Pin | Reason |
| --- | --- | --- |
| Microsoft.SemanticKernel.Core | 1.71.0 | Patch [GHSA-2ww3-72rp-wpp4](https://github.com/advisories/GHSA-2ww3-72rp-wpp4) while retaining connector compatibility |
| MongoDB.Driver | 3.12.0 | Bring patched SharpCompress/Snappier through the driver |
| Microsoft.Data.SqlClient / System.Security.Cryptography.Pkcs | 6.1.7 / 10.0.13 | Replace inherited vulnerable cryptography dependency |
| MessagePack (Aspire) | 2.5.303 | Patch [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) within the 2.5 API line |

These pins do not add MongoDB or SQL resources; the application uses Azure AI Search and Cosmos.

## Validation and future updates

After restoring updates:

```bash
npm run quality
dotnet list src/MyAIAgent.slnx package --vulnerable --include-transitive --no-restore
```

- Review licenses and API compatibility; keep advisory warnings visible.
- Dependabot excludes MediatR and FluentAssertions major upgrades to preserve license choices.
- Tests exercise MCP HTTP/authentication/lifetime and a fake KernelMemory AI response. Validate live ingestion, retrieval, and Cosmos against test resources before deploying.

## Package references

- [KernelMemory service package](https://www.nuget.org/packages/Microsoft.KernelMemory.Service.AspNetCore/0.98.250508.3)
- [MediatR 12.5.0](https://www.nuget.org/packages/MediatR/12.5.0)
- [FluentAssertions 6.12.2](https://www.nuget.org/packages/FluentAssertions/6.12.2)
- [ASP.NET Core OpenAPI 10.0.13](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.13)
