# Project context

MyAIAgent is a public .NET 10 / C# backend repository with four services. Tracked configuration must contain safe defaults only; credentials belong in Key Vault or local developer secret storage. AAC and Cosmos use Azure identities, and deployed services have separate least-privilege runtime identities. The real directories are
grouped under `src/Services`, `src/Common`, `src/Clients`, and `src/Aspire`.
Test and test-helper projects live separately under root `tests/`, mirroring those groups;
`src/MyAIAgent.slnx` includes both trees.

| Area | Actual path prefix | Responsibility / review focus |
| --- | --- | --- |
| Gateway | `src/Services/Gateway/MyAIAgent.Gateway.*` | API gateway, chat forwarding, public streaming chat, admin agents/tools, document upload |
| Orchestrator | `src/Services/Orchestrator/MyAIAgent.Orchestrator.*` | IChatClient model calls, MCP dispatch, chat/session and agent persistence |
| KernelMemory | `src/Services/KernelMemory/MyAIAgent.KernelMemory.*` | RAG daemon, document ingestion, document records and retrieval |
| McpServer | `src/Services/McpServer/MyAIAgent.McpServer.*` | MCP tools, Tavily web search and webpage extraction |
| Common | `src/Common/MyAIAgent.Common.*` | Response models, validation, DI, auth filters, errors, resilience, storage |
| Aspire | `src/Aspire/MyAIAgent.All/` | Multi-service composition and service configuration |
| Consumer SDK | `src/Services/Gateway/MyAIAgent.Gateway.Client/` | Reusable generated Gateway HTTP client and transport models |
| Console client | `src/Clients/MyAIAgent.ConsoleClient/` | Interactive local testing through Gateway HTTP only; no service project references |
| Tests | `tests/{Services,Clients,Common}/` | Service and console suites plus shared test-only helpers |

Gateway.Client is a reusable HTTP SDK with no backend or console project references.
Its pinned NSwag.MSBuild target generates all methods and models from
`src/Services/Gateway/MyAIAgent.Gateway.Client/OpenApi/openapi.json` before compilation; output stays in ignored
`obj/`. Consumers replace the JSON and build normally. ConsoleClient references this SDK and
keeps UI/loopback/response-limit policies locally. Gateway schema tests verify the complete API
contract; consumer tests live alongside console tests. No custom generation command is needed.
ConsoleClient reads its Gateway address from appsettings in the executable directory, then optional
`DOTNET_ENVIRONMENT` JSON, environment variables, and the `--gateway` override. ClientOptions validates
the effective address; the SDK itself has no hardcoded host or console configuration dependency.

Each service has Domain, Application, Infrastructure and Presentation projects under `src/`,
with its Tests project under `tests/Services/<Service>/`. `MyAIAgent.Common.Tests` contains shared test helpers under `tests/Common/`; the four service
Tests projects run xUnit tests using Moq and FluentAssertions. Inspect `.csproj`
files for current versions; do not copy package versions from examples.

## Architecture and implementation

- `Directory.Build.props` owns shared .NET target/compiler defaults; `Directory.Packages.props`
  pins NuGet versions centrally. Keep project-specific properties and package references in each project.
- Common.Domain owns entity bases and lifetime markers. Common.Application owns `ResponseModel<T>`,
  application errors, validation and registration helpers. Common.Infrastructure owns service authorization
  settings, HTTP filters, Azure configuration and persistence adapters. Shared code must serve multiple services.
- Dependencies point inward: Domain ← Application ← Infrastructure ← Presentation.
  Common projects must not reference services; sibling services communicate over HTTP.
- Presentation dispatches through MediatR `ISender`; Application owns commands,
  queries, inbound DTOs, mappers and FluentValidation. Preserve response/error contracts.
- Shared exception handling and Gateway application-error metadata use `ResponseModel<ErrorResponseDto>`.
  The DTO lives in `Common.Application/Models`; errors keep `Message`/`Errors` on the envelope
  and put the HTTP status in `Data.StatusCode`. Gateway converts failed operation results at the HTTP boundary
  with `ToErrorResponse(statusCode)`.
- Map Minimal API modules with `IEndpointRouteBuilder Map[Module]Endpoints(this IEndpointRouteBuilder endpoints, ...)`.
  Return the incoming builder for chaining. Keep host startup/middleware separate and apply authorization
  to groups before composing feature modules; retain MCP middleware before raw transport dispatch.
- Keep each command/query, handler and validator in its own request folder under
  `Application/{Feature}/Commands/` or `Application/{Feature}/Queries/`.
- Keep Mapster `IRegister` mappings in `Application/Common/Mappings/`; scan and
  compile the service Application assembly once, then inject `IMapper` at call sites.
- Place feature DTOs and result contracts in `Application/{Feature}/`, ports in
  `Application/Common/Interfaces/`, and DI registration at each layer's root
  `IoCExtension.cs`. Keep the existing `Presentation` project names.
- Domain entities inherit `Common.Domain/Abstractions/BaseEntity<TKey>` for their shared key.
  Shared audit base types are available when the entity actually needs those fields.
- Infrastructure owns external clients and persistence; follow existing typed
  `HttpClient` registrations and interfaces rather than inventing abstractions.
  Place adapters in `Infrastructure/Services/` and repository implementations in
  `Infrastructure/Data/Repositories/`.
- Follow local C# style: records for messages, primary constructors for DI,
  file-scoped namespaces, sealed classes where appropriate, async/await and propagated
  cancellation. Use injected `TimeProvider` for behavior that depends on time.
- Keep secrets out of source, logs and agent reports. Use user-secrets or deployed
  configuration. Do not run live providers, upload documents or start Aspire merely
  to verify an unrelated change.

Gateway forwards chat/agent administration to Orchestrator, document uploads to
KernelMemory, and tool discovery to McpServer. Orchestrator dispatches tools through
the agent-configured MCP endpoint; direct KernelMemory retrieval during chat is not
yet implemented. McpServer does not call KernelMemory. MCP requires service-key authorization outside Development. Gateway and Orchestrator send its key only to the configured trusted MCP origin and protocol paths; never put the key on arbitrary agent-provided endpoints.

Aspire wires project references
in `src/Aspire/MyAIAgent.All/`; current HTTP clients still read explicit base-URL settings.

## Tests that matter

Use the owning `tests/Services/<Service>/MyAIAgent.<Service>.Tests` project. Add regression tests for
behavior changes, invalid/missing input, dependency failure and cancellation where
applicable. Test real handlers/validators/mappers; mock external I/O boundaries.
Integration tests may use a real MediatR pipeline. Repository tests run against the Cosmos
test database (`myaiagent-test`) through `[CosmosFact]` and are **skipped, not passed,** when
`COSMOS_TEST_ENDPOINT` is unset. Tests use Azure CLI sign-in against preprovisioned containers; developer identities must have only test-container grants. PR CI receives no Azure identity or endpoint. Never grant test identities production access.
Inspect existing fixtures before adding dependencies.

```bash
npm ci --ignore-scripts
npm run quality -- --project Orchestrator
npm run quality
```

| Service argument | Test project |
| --- | --- |
| `Gateway` | `tests/Services/Gateway/MyAIAgent.Gateway.Tests/MyAIAgent.Gateway.Tests.csproj` |
| `Orchestrator` | `tests/Services/Orchestrator/MyAIAgent.Orchestrator.Tests/MyAIAgent.Orchestrator.Tests.csproj` |
| `KernelMemory` | `tests/Services/KernelMemory/MyAIAgent.KernelMemory.Tests/MyAIAgent.KernelMemory.Tests.csproj` |
| `McpServer` | `tests/Services/McpServer/MyAIAgent.McpServer.Tests/MyAIAgent.McpServer.Tests.csproj` |
| `ConsoleClient` | `tests/Clients/MyAIAgent.ConsoleClient.Tests/MyAIAgent.ConsoleClient.Tests.csproj` |

Common changes affect the four backend suites; the standalone console client also has a runnable suite.
`Common.Tests` is a helper, not a runnable suite. The full quality command restores/builds
`src/MyAIAgent.slnx`, runs all five test projects with separate TRX directories, and verifies formatting of
added/modified candidate C# files without rewriting source. No C# changes means
format is NOT_APPLICABLE, not a claim that the whole solution is formatted.
`--project` restores/builds/tests only that service's test project and dependencies;
formatting is deferred to the full gate. It is iteration evidence and cannot
satisfy the full commit gate. See
[evidence.md](evidence.md) for artifacts, candidate binding and branch scope.

## Load only relevant references

| Work | Existing reference |
| --- | --- |
| Layer changes | `.agents/skills/clean-architecture/SKILL.md`, `.agents/rules/dotnet/architecture.md` |
| Handlers and validation | `.agents/skills/mediatr-cqrs/SKILL.md` |
| MCP tools | `.agents/skills/mcp-server/SKILL.md` |
| Chat/agent orchestration | `.agents/skills/orchestrator/SKILL.md` |
| RAG/ingestion | `.agents/skills/kernel-memory/SKILL.md` |
| HTTP/retry | `.agents/skills/resilience/SKILL.md` |

These canonical files are shared by both assistants; Claude's discovery paths are
symlinks to them. Actual source and `src/MyAIAgent.slnx` take precedence over outdated
sample paths in a reference.
