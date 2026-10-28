# Repository guide

The optional `ai-audit` workflow lives in `.agents/skills/ai-audit/` and `.agents/docs/audit.md`. Its TypeScript control-plane helper is `scripts/security/audit.ts`. `.claude/skills/ai-audit` links to the canonical skill. `.agents/vendor/security-audit/` contains unchanged MIT-licensed upstream material; its `source.json` records the revision and checksums, and `report-schema.json` defines finding records. Vendor files retain upstream formatting and notices. Generated audit JSON and Markdown remain ignored under `artifacts/security-audit/`.

The executable validator ports and their tests are TypeScript: `scripts/security/validate-findings.ts`, `validate-coverage-ledger.ts`, and matching `.test.ts` files. `scripts/security/audit-validators.LICENSE` retains Cloudflare's MIT notice. The upstream JavaScript files remain an untouched reference; use the TypeScript commands in the [audit guide](../.agents/docs/audit.md).

Within ignored audit runs, `critic-input.json` retains the finalized evidence snapshot supplied to the coverage critic. Metadata binds its digest to the critic response and binds rendered reports to all input evidence. Critic reconciliation is applied by the helper; later findings do not alter the reviewed ledger.

Find code, configuration, and shared instructions. Source headers use `/** ... */` with a `MyAIAgent | <component>` signature and a short purpose. Other formats use native comments or the entries below.

## Suggested reading order

1. Start with the [README](../README.md) and [request diagrams](architecture.md).
2. Open `src/Aspire/MyAIAgent.All/AppHost.cs`, then Gateway `Program.cs` under `src/Services/Gateway/`.
3. Trace a Gateway handler/client into Orchestrator. Read `ChatSessionHistory` for context/persistence and `ChatOrchestratorService` for model/tools.
4. For documents, read KernelMemory `Program.cs` and `KernelMemoryProviderExtension`.
5. For web research, follow `WebSearchTool` → `WebSearchHandler` → `TavilySearchClient`, or `ReadWebPageTool` → `ReadWebPageHandler` → `TavilyWebPageClient`. Orchestrator's `AgentHelper`, `WebResearchFunction`, and `WebResearchSources` enforce selection, bound calls, and collect reply links.
6. Check the matching tests and [contributor guide](../CONTRIBUTING.md).

## Directory map

| Location | Purpose |
| --- | --- |
| `src/Aspire/MyAIAgent.All/` | Aspire process composition and dashboard launch configuration |
| `src/Services/Gateway/MyAIAgent.Gateway.Client/` | Reusable HTTP SDK; generates all operations and wire models from its OpenAPI JSON during build |
| `src/Clients/MyAIAgent.ConsoleClient/` | Interactive local Gateway client: menus, HTTP/SSE handling, session state, and safe terminal output |
| `tests/Clients/MyAIAgent.ConsoleClient.Tests/` | Scripted terminal journeys plus SDK consumer, streaming, and cancellation tests |
| `src/Services/<Service>/MyAIAgent.<Service>.Domain/` | Entities, constants, and domain state |
| `src/Services/<Service>/MyAIAgent.<Service>.Application/` | Requests, handlers, validation, response contracts, mappings, and dependency ports |
| `src/Services/<Service>/MyAIAgent.<Service>.Infrastructure/` | HTTP/provider adapters, persistence, and infrastructure registration |
| `src/Services/<Service>/MyAIAgent.<Service>.Presentation/` | Application entry point, configuration, HTTP endpoints, and MCP tool wrappers |
| `tests/Services/<Service>/MyAIAgent.<Service>.Tests/` | Unit and selected integration tests for that service |
| `src/Common/MyAIAgent.Common.*/` | Shared domain bases, result types, validation, registration helpers, Azure configuration, and errors |
| `tests/Common/MyAIAgent.Common.Tests/` | Shared test-only helpers; this project is not a runnable suite |
| `scripts/ai/` | TypeScript quality runner, candidate/evidence checks, and their tests |
| `scripts/deploy/` | TypeScript Container Apps deployment and authenticated smoke checks |
| `scripts/security/` | TypeScript secret-scanner integration and opt-in GitHub protection configuration |
| `infra/` | Bicep provisioning, app deployment, and scoped access modules |
| `.github/` | CI/CD workflows, composite actions, ownership, dependency updates, and PR template |
| `.agents/` | Canonical assistant workflow documentation, engineering rules, and skills |
| `.codex/`, `.claude/` | Assistant-specific adapters and links to shared instructions |
| `docs/` | Public setup, architecture, API, security, dependency, and deployment guides |

`Common.Tests` supplies helpers and is not runnable. The quality gate runs four backend suites plus the console-client suite.
The solution groups production projects from `src/` and test projects from the separate root `tests/` tree.
Test folders mirror `Services`, `Common`, and `Clients`; production projects never reference test projects.
Use `.csproj` files for dependencies, `Directory.Packages.props` for package versions, and
`Directory.Build.props` for shared compiler defaults. Project-specific settings remain local.

`Common.Domain` owns entity bases and lifetime markers; `Common.Application` owns `ResponseModel<T>`,
error contracts, validation and DI helpers; `Common.Infrastructure` owns `ServiceAuthorizationModel`,
HTTP controls, Azure configuration and persistence adapters. Add shared code only for a demonstrated
cross-service need. The generated Gateway SDK is grouped with the API it represents and remains independent
of these backend projects.

The console client is a single executable project, not another backend service. `Program.cs` registers a typed `GatewayClient`; `ConsoleApplication.cs` manages menus and chat state; `TerminalTextFilter.cs` removes terminal controls across streaming chunks. It references the reusable Gateway SDK. `GatewayClient` retains the SSE adapter and safe error handling; `GatewayResponseHandler` applies console-specific response policies. Run it separately from Aspire so terminal input remains interactive.

`MyAIAgent.Gateway.Client.csproj` generates the SDK before compilation from `OpenApi/openapi.json` using pinned NSwag.MSBuild and `nswag.json`. Output stays under `obj/` and is never manually edited. The SDK has no service or console project references. Gateway's `GatewayEndpoints.cs` maps runtime routes once; `GatewayOpenApiExtensions.cs` describes them. `GatewayOpenApiTests` compares the complete schema, including optional visitor routes, against the SDK input with mocked downstream dependencies.

## Common source-file roles

Minimal API modules use `IEndpointRouteBuilder Map[Module]Endpoints(this IEndpointRouteBuilder endpoints, ...)`
and return the incoming builder for chaining. Gateway composes chat, conversation, admin and health modules;
Orchestrator composes chat, conversation, agent and health modules under its service-key filter.
`MapMcpServiceEndpoints` and `MapKernelMemoryEndpoints` wrap their SDK route mappings.
MCP startup must call `UseMcpServiceAuthorization()` before mapping transport routes, including in test hosts.

| File/type | Role |
| --- | --- |
| `Program.cs` / Aspire `AppHost.cs` | Compose the service or local application |
| `IoCExtension.cs` | Register a layer's dependencies |
| Command/query, validator, handler | Carry input, validate it, execute a use case |
| Application interface / Infrastructure adapter | Define an external capability / implement it |
| Mapping | Transform boundary contracts |
| Repository / unit of work | Track / commit persistence changes |
| Test | Name the component, describe coverage in the header, retain AAA phases |

## JSON and generated files

Strict JSON cannot carry comments; lockfiles are generated. Keep tracked values safe to publish and filled-in configuration outside Git.

| File | Purpose |
| --- | --- |
| [src/Services/Gateway/MyAIAgent.Gateway.Client/OpenApi/openapi.json](../src/Services/Gateway/MyAIAgent.Gateway.Client/OpenApi/openapi.json) | Complete API contract input; replace its JSON and build to regenerate the consumer SDK |
| [src/Clients/MyAIAgent.ConsoleClient/appsettings.json](../src/Clients/MyAIAgent.ConsoleClient/appsettings.json) | Console Gateway address and agent-creation MCP endpoint; environment-specific JSON/environment variables override defaults, with `--gateway` taking priority for Gateway |
| [src/Services/Gateway/MyAIAgent.Gateway.Client/nswag.json](../src/Services/Gateway/MyAIAgent.Gateway.Client/nswag.json) | Generates every described operation and model with an injected HttpClient; output goes to obj/ |
| [.claude/settings.json](../.claude/settings.json) | Claude permissions and workflow settings |
| [package-lock.json](../package-lock.json) | Generated npm resolution/integrity data; update through npm |
| [package.json](../package.json) | Node requirements, development dependencies, npm commands |
| [src/Aspire/MyAIAgent.All/Properties/launchSettings.json](../src/Aspire/MyAIAgent.All/Properties/launchSettings.json) | AppHost launch profiles, ports, and Development environment |
| [src/Aspire/MyAIAgent.All/appsettings.Development.json](../src/Aspire/MyAIAgent.All/appsettings.Development.json) | Public AppHost Development overrides |
| [src/Aspire/MyAIAgent.All/appsettings.json](../src/Aspire/MyAIAgent.All/appsettings.json) | Public AppHost defaults |
| [src/Services/Gateway/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json](../src/Services/Gateway/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json) | Gateway launch profiles, ports, and Development environment |
| [src/Services/Gateway/MyAIAgent.Gateway.Presentation/appsettings.json](../src/Services/Gateway/MyAIAgent.Gateway.Presentation/appsettings.json) | Public Gateway defaults |
| [src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json](../src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json) | KernelMemory launch profile, ports, and Development environment |
| [src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json](../src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json) | Public KernelMemory Development overrides |
| [src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/appsettings.json](../src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation/appsettings.json) | Public KernelMemory defaults |
| [src/Services/McpServer/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json](../src/Services/McpServer/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json) | MCP launch profiles, ports, and Development environment |
| [src/Services/McpServer/MyAIAgent.McpServer.Presentation/appsettings.Development.json](../src/Services/McpServer/MyAIAgent.McpServer.Presentation/appsettings.Development.json) | Public MCP Development overrides |
| [src/Services/McpServer/MyAIAgent.McpServer.Presentation/appsettings.json](../src/Services/McpServer/MyAIAgent.McpServer.Presentation/appsettings.json) | Public MCP defaults |
| [src/Services/Orchestrator/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json](../src/Services/Orchestrator/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json) | Orchestrator launch profiles, ports, and Development environment |
| [src/Services/Orchestrator/MyAIAgent.Orchestrator.Presentation/appsettings.json](../src/Services/Orchestrator/MyAIAgent.Orchestrator.Presentation/appsettings.json) | Public Orchestrator defaults |
| [tsconfig.json](../tsconfig.json) | Strict TypeScript compiler settings and source inclusion |

## Shared instruction links

Claude reads shared instructions through these symlinks. Edit the canonical `.agents/` target and preserve the link.

| Link | Canonical target and purpose |
| --- | --- |
| `.claude/rules/shared` | `../../.agents/rules` — exposes the shared engineering rules. |
| `.claude/skills/ai-build` | `../../.agents/skills/ai-build` — exposes the ai-build skill. |
| `.claude/skills/ai-commit` | `../../.agents/skills/ai-commit` — exposes the ai-commit skill. |
| `.claude/skills/ai-plan` | `../../.agents/skills/ai-plan` — exposes the ai-plan skill. |
| `.claude/skills/ai-verify` | `../../.agents/skills/ai-verify` — exposes the ai-verify skill. |
| `.claude/skills/clean-architecture` | `../../.agents/skills/clean-architecture` — exposes the clean-architecture skill. |
| `.claude/skills/kernel-memory` | `../../.agents/skills/kernel-memory` — exposes the kernel-memory skill. |
| `.claude/skills/mcp-server` | `../../.agents/skills/mcp-server` — exposes the mcp-server skill. |
| `.claude/skills/mediatr-cqrs` | `../../.agents/skills/mediatr-cqrs` — exposes the mediatr-cqrs skill. |
| `.claude/skills/orchestrator` | `../../.agents/skills/orchestrator` — exposes the orchestrator skill. |
| `.claude/skills/resilience` | `../../.agents/skills/resilience` — exposes the resilience skill. |

## Documentation and assistant metadata

- Markdown opens with its purpose; preserve skill/reviewer frontmatter at the top. The PR template uses a hidden introduction.
- `AGENTS.md` owns working agreements; `CLAUDE.md` imports them. `.agents/docs/`, `rules/`, and `skills/` own shared policy. Codex TOML sets defaults/reviewer roles, not active-session mode.
- Exclude build output, `node_modules`, user-secrets, downloads, and `artifacts/` from documentation coverage. Never edit generated content or commit private settings for an example.
