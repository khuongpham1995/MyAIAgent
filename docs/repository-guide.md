# Repository guide

Use this guide to find a request path, understand project boundaries, and identify files whose formats cannot carry a purpose comment. Maintained source files explain their specific role in a short opening comment.

C#, TypeScript, and Bicep files use `/** ... */` blocks: a `MyAIAgent | <component>` signature identifies the owning area, followed by the file's description. Other formats retain their native comment syntax or the guide entries below.

## Suggested reading order

1. Read the [README](../README.md) for capabilities and limitations.
2. Follow the chat and ingestion diagrams in [architecture](architecture.md).
3. Open `src/MyAIAgent.All/AppHost.cs` to see local composition, then Gateway `Program.cs` for incoming routes.
4. Follow a Gateway handler and client into the matching Orchestrator handler. `ChatSessionHistory` and `ChatOrchestratorService` show persistence/context and model/tool responsibilities.
5. Read KernelMemory's `Program.cs` and `KernelMemoryProviderExtension` for the separate RAG daemon.
6. Read MCP `WebSearchTool`, `WebSearchHandler`, `GoogleSearchClient`, and `PageFetcher` to trace one tool end to end.
7. Inspect the matching tests and [contributor guide](../CONTRIBUTING.md) before extending a feature.

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

`Common.Tests` supplies helpers; it is not a fifth runnable test suite. The `.slnx` file groups the real projects rather than creating nested filesystem directories. Project files define references and dependencies; they are the source of truth for package versions.

## Common source-file roles

- `Program.cs` is a service composition root. Aspire's composition root is `AppHost.cs`.
- `IoCExtension.cs` registers the owning layer's dependencies.
- Commands/queries carry input, validators reject invalid requests, and handlers implement a use case.
- Application interfaces define external capabilities required by handlers; Infrastructure provides adapters.
- Mapping classes define request/response transformations at application boundaries.
- Repositories track persistence changes; Orchestrator's unit of work commits them.
- Test filenames identify the component; their header explains the behavior covered. AAA comments mark Arrange, Act, and Assert phases.

## JSON and generated files

These files intentionally have no inserted comments: they must remain valid JSON, and the dependency lockfile is generated. Values in tracked configuration must be safe to publish; filled-in local configuration belongs outside Git.

| File | Purpose |
| --- | --- |
| [.claude/settings.json](../.claude/settings.json) | Claude project permissions and model/workflow settings. |
| [package-lock.json](../package-lock.json) | Generated dependency resolution and integrity metadata used by npm ci; update through npm when dependencies change. |
| [package.json](../package.json) | Node engine requirements, locked development-tool declarations, and executable npm commands. |
| [src/MyAIAgent.All/Properties/launchSettings.json](../src/MyAIAgent.All/Properties/launchSettings.json) | Aspire AppHost local launch profiles, addresses, and Development environment settings. |
| [src/MyAIAgent.All/appsettings.Development.json](../src/MyAIAgent.All/appsettings.Development.json) | Aspire AppHost checked-in Development configuration overrides; not a local secret store. |
| [src/MyAIAgent.All/appsettings.json](../src/MyAIAgent.All/appsettings.json) | Aspire AppHost public configuration defaults and placeholders; overridden by local or deployed configuration. |
| [src/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json](../src/MyAIAgent.Gateway.Presentation/Properties/launchSettings.json) | Gateway local launch profiles, addresses, and Development environment settings. |
| [src/MyAIAgent.Gateway.Presentation/appsettings.json](../src/MyAIAgent.Gateway.Presentation/appsettings.json) | Gateway public configuration defaults and placeholders; overridden by local or deployed configuration. |
| [src/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json](../src/MyAIAgent.KernelMemory.Presentation/Properties/launchSettings.json) | KernelMemory local launch profiles, addresses, and Development environment settings. |
| [src/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json](../src/MyAIAgent.KernelMemory.Presentation/appsettings.Development.json) | KernelMemory checked-in Development configuration overrides; not a local secret store. |
| [src/MyAIAgent.KernelMemory.Presentation/appsettings.json](../src/MyAIAgent.KernelMemory.Presentation/appsettings.json) | KernelMemory public configuration defaults and placeholders; overridden by local or deployed configuration. |
| [src/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json](../src/MyAIAgent.McpServer.Presentation/Properties/launchSettings.json) | McpServer local launch profiles, addresses, and Development environment settings. |
| [src/MyAIAgent.McpServer.Presentation/appsettings.Development.json](../src/MyAIAgent.McpServer.Presentation/appsettings.Development.json) | McpServer checked-in Development configuration overrides; not a local secret store. |
| [src/MyAIAgent.McpServer.Presentation/appsettings.json](../src/MyAIAgent.McpServer.Presentation/appsettings.json) | McpServer public configuration defaults and placeholders; overridden by local or deployed configuration. |
| [src/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json](../src/MyAIAgent.Orchestrator.Presentation/Properties/launchSettings.json) | Orchestrator local launch profiles, addresses, and Development environment settings. |
| [src/MyAIAgent.Orchestrator.Presentation/appsettings.json](../src/MyAIAgent.Orchestrator.Presentation/appsettings.json) | Orchestrator public configuration defaults and placeholders; overridden by local or deployed configuration. |
| [tsconfig.json](../tsconfig.json) | Strict TypeScript compiler settings and automation source inclusion. |

## Shared instruction links

These tracked symlinks expose canonical instructions to Claude. Their purpose is reuse, not a second editable copy. Edit the `.agents/` target; preserve the symlink itself.

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

Markdown files identify their purpose in an opening paragraph, existing introductory instructions, or frontmatter `description`. Skill/reviewer frontmatter must stay first so assistant tools can discover it. The PR template uses a hidden opening description so it remains useful when copied into a pull request.

`AGENTS.md` defines project working agreements; `CLAUDE.md` imports them for Claude. Shared policy lives under `.agents/docs/`, subject-specific rules under `.agents/rules/`, and skills under `.agents/skills/`. Codex TOML files select project defaults and reviewer roles; they do not switch an active session's native mode automatically.

Build outputs, `node_modules`, local user-secrets, tool downloads, and `artifacts/` evidence are not maintained source. Do not add purpose headers to generated output, edit lockfile contents for documentation, or commit private configuration to make a setup example work.
