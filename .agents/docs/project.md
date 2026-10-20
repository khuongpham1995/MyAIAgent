# Project context

MyAIAgent is a public .NET 10 / C# backend repository with four services. Tracked configuration must contain safe defaults only; credentials belong in Key Vault or local developer secret storage. AAC and Cosmos use Azure identities, and deployed services have separate least-privilege runtime identities. The real directories are
flat under `src/`; folders in `src/MyAIAgent.slnx` are solution groupings only.

| Area | Actual path prefix | Responsibility / review focus |
| --- | --- | --- |
| Gateway | `src/MyAIAgent.Gateway.*` | API gateway, chat forwarding, public streaming chat, admin agents/tools, document upload |
| Orchestrator | `src/MyAIAgent.Orchestrator.*` | IChatClient model calls, MCP dispatch, chat/session and agent persistence |
| KernelMemory | `src/MyAIAgent.KernelMemory.*` | RAG daemon, document ingestion, document records and retrieval |
| McpServer | `src/MyAIAgent.McpServer.*` | MCP tools, Google search and page fetching |
| Common | `src/MyAIAgent.Common.*` | Response models, validation, DI, auth filters, errors, resilience, storage |
| Aspire | `src/MyAIAgent.All/` | Multi-service composition and service configuration |

Each service has Domain, Application, Infrastructure, Presentation and Tests
projects. `MyAIAgent.Common.Tests` contains shared test helpers; the four service
Tests projects run xUnit tests using Moq and FluentAssertions. Inspect `.csproj`
files for current versions; do not copy package versions from examples.

## Architecture and implementation

- Dependencies point inward: Domain ← Application ← Infrastructure ← Presentation.
  Common projects must not reference services; sibling services communicate over HTTP.
- Presentation dispatches through MediatR `ISender`; Application owns commands,
  queries, inbound DTOs, mappers and FluentValidation. Preserve response/error contracts.
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
yet implemented. McpServer does not call KernelMemory. Aspire wires project references
in `src/MyAIAgent.All/`; current HTTP clients still read explicit base-URL settings.

## Tests that matter

Use the owning `src/MyAIAgent.<Service>.Tests` project. Add regression tests for
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
| `Gateway` | `src/MyAIAgent.Gateway.Tests/MyAIAgent.Gateway.Tests.csproj` |
| `Orchestrator` | `src/MyAIAgent.Orchestrator.Tests/MyAIAgent.Orchestrator.Tests.csproj` |
| `KernelMemory` | `src/MyAIAgent.KernelMemory.Tests/MyAIAgent.KernelMemory.Tests.csproj` |
| `McpServer` | `src/MyAIAgent.McpServer.Tests/MyAIAgent.McpServer.Tests.csproj` |

Common changes affect all four suites; `Common.Tests` is a helper, not a runnable
suite. The full quality command restores/builds `src/MyAIAgent.slnx`, runs all four
service test projects with separate TRX directories, and verifies formatting of
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
