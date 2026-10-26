# Repository guide

The optional `ai-audit` workflow lives in `.agents/skills/ai-audit/` and `.agents/docs/audit.md`. Its TypeScript control-plane helper is `scripts/security/audit.ts`. `.claude/skills/ai-audit` links to the canonical skill. `.agents/vendor/security-audit/` contains unchanged MIT-licensed upstream material; its `source.json` records the revision and checksums, and `report-schema.json` defines finding records. Vendor files retain upstream formatting and notices. Generated audit JSON and Markdown remain ignored under `artifacts/security-audit/`.

The executable validator ports and their tests are TypeScript: `scripts/security/validate-findings.ts`, `validate-coverage-ledger.ts`, and matching `.test.ts` files. `scripts/security/audit-validators.LICENSE` retains Cloudflare's MIT notice. The upstream JavaScript files remain an untouched reference; use the TypeScript commands in the [audit guide](../.agents/docs/audit.md).

Within ignored audit runs, `critic-input.json` retains the finalized evidence snapshot supplied to the coverage critic. Metadata binds its digest to the critic response and binds rendered reports to all input evidence. Critic reconciliation is applied by the helper; later findings do not alter the reviewed ledger.

Find code, configuration, and shared instructions. Source headers use `/** ... */` with a `MyAIAgent | <component>` signature and a short purpose. Other formats use native comments or the entries below.

## Suggested reading order

1. Start with the [README](../README.md) and [request diagrams](architecture.md).
2. Open `MyAIAgent.All/AppHost.cs`, then Gateway `Program.cs` under `src/`.
3. Trace a Gateway handler/client into Orchestrator. Read `ChatSessionHistory` for context/persistence and `ChatOrchestratorService` for model/tools.
4. For documents, read KernelMemory `Program.cs` and `KernelMemoryProviderExtension`.
5. For web research, follow `WebSearchTool` → `WebSearchHandler` → `TavilySearchClient`, or `ReadWebPageTool` → `ReadWebPageHandler` → `TavilyWebPageClient`. Orchestrator's `AgentHelper`, `WebResearchFunction`, and `WebResearchSources` enforce selection, bound calls, and collect reply links.
6. Check the matching tests and [contributor guide](../CONTRIBUTING.md).

## Directory map

| Location | Purpose |
| --- | --- |
| `src/MyAIAgent.All/` | Aspire process composition and dashboard launch configuration |
| `src/MyAIAgent.<Service>.Domain/` | Entities, constants, and domain state |
| `src/MyAIAgent.<Service>.Application/` | Requests, handlers, validation, response contracts, mappings, and dependency ports |
| `src/MyAIAgent.<Service>.Infrastructure/` | HTTP/provider adapters, persistence, and infrastructure registration |
| `src/MyAIAgent.<Service>.Presentation/` | Application entry point, configuration, HTTP endpoints, and MCP tool wrappers |
| `src/MyAIAgent.<Service>.Tests/` | Unit and selected integration tests for that service |
| `src/MyAIAgent.Common.*/` | Shared result types, validation, registration helpers, Azure configuration, errors, and test helpers |
| `scripts/ai/` | TypeScript quality runner, candidate/evidence checks, and their tests |
| `scripts/deploy/` | TypeScript Container Apps deployment and authenticated smoke checks |
| `scripts/security/` | TypeScript secret-scanner integration and opt-in GitHub protection configuration |
| `infra/` | Bicep provisioning, app deployment, and scoped access modules |
| `.github/` | CI/CD workflows, composite actions, ownership, dependency updates, and PR template |
| `.agents/` | Canonical assistant workflow documentation, engineering rules, and skills |
| `.codex/`, `.claude/` | Assistant-specific adapters and links to shared instructions |
| `docs/` | Public setup, architecture, API, security, dependency, and deployment guides |

`Common.Tests` supplies helpers, not a fifth test suite. Directories are flat under `src/`; `.slnx` groups them visually. Use `.csproj` files for current references and versions.

## Common source-file roles

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
| [.claude/settings.json](../.claude/settings.json) | Claude permissions and workflow settings |
| [package-lock.json](../package-lock.json) | Generated npm resolution/integrity data; update through npm |
| [package.json](../package.json) | Node requirements, development dependencies, npm commands |
| [src/MyAIAgent.All/Properties/launchSettings.json](../src/MyAIAgent.All/Properties/launchSettings.json) | AppHost launch profiles, ports, and Development environment |
| [src/MyAIAgent.All/appsettings.Development.json](../src/MyAIAgent.All/appsettings.Development.json) | Public AppHost Development overrides |
| [src/MyAIAgent.All/appsettings.json](../src/MyAIAgent.All/appsettings.json) | Public AppHost defaults |
| [src/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json](../src/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json) | Gateway launch profiles, ports, and Development environment |
| [src/MyAIAgent.Gateway.Presentation/appsettings.json](../src/MyAIAgent.Gateway.Presentation/appsettings.json) | Public Gateway defaults |
| [src/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json](../src/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json) | KernelMemory launch profile, ports, and Development environment |
| [src/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json](../src/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json) | Public KernelMemory Development overrides |
| [src/MyAIAgent.KernelMemory.Presentation/appsettings.json](../src/MyAIAgent.KernelMemory.Presentation/appsettings.json) | Public KernelMemory defaults |
| [src/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json](../src/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json) | MCP launch profiles, ports, and Development environment |
| [src/MyAIAgent.McpServer.Presentation/appsettings.Development.json](../src/MyAIAgent.McpServer.Presentation/appsettings.Development.json) | Public MCP Development overrides |
| [src/MyAIAgent.McpServer.Presentation/appsettings.json](../src/MyAIAgent.McpServer.Presentation/appsettings.json) | Public MCP defaults |
| [src/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json](../src/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json) | Orchestrator launch profiles, ports, and Development environment |
| [src/MyAIAgent.Orchestrator.Presentation/appsettings.json](../src/MyAIAgent.Orchestrator.Presentation/appsettings.json) | Public Orchestrator defaults |
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
