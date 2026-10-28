# Local development with Aspire

Build the solution, configure providers, and run four services with Aspire. AppHost starts processes; it does not provision Azure resources.

Service projects live in `src/Services/<Service>/`, AppHost in `src/Aspire/`, and the console in
`src/Clients/`. Test projects live under root `tests/`, alongside `src/`.
Reopen `src/MyAIAgent.slnx` and update IDE run configurations that still use
the former flat paths. Existing user-secrets remain available because `UserSecretsId` values are unchanged.

## 1. Build before connecting providers

Install Git, .NET 10 SDK, Node.js 20.6+, and Azure CLI. From the repository root:

```bash
npm ci --ignore-scripts
npm run quality
dotnet dev-certs https --trust
```

The gate needs no Azure credentials; live Cosmos tests skip without `COSMOS_TEST_ENDPOINT`. Docker is needed only for workflow validation, not AppHost. HTTPS requires the trusted development certificate above.

## 2. Prepare Azure dependencies

Use a development subscription/resource group. Review costs first: budget alerts do not cap spending. Local setup needs no Container Apps, registry, or production identities; see [deployment](deployment.md) for provisioning and grants.

| Dependency | Local requirement |
| --- | --- |
| Cosmos DB for NoSQL | `myaiagent-test` with `ChatSessions` (`/UserId`), `AgentDefinitions` (`/Id`), and `DocumentRecords` (`/IndexName`). Runtime does not create containers. |
| Azure OpenAI | Available chat/text and embedding deployments, endpoint, and provider key. KernelMemory requires these even if Orchestrator uses OpenAI. |
| Azure AI Search | Service endpoint and admin key for KernelMemory index operations. |
| Azure Blob Storage | Account connection string and a development container for persistent documents in the configuration below. |
| Azure App Configuration | Optional locally. Endpoint plus App Configuration Data Reader access when enabled. |
| Tavily | API key for `web_search` and `read_webpage`; startup and discovery work without it. Keep paid billing disabled if you want a free-only account. |

Provision Cosmos/Search with Bicep or equivalent settings; OpenAI deployments and Blob Storage are separate. Grant developer data access to **test containers only**. Subscription management rights do not grant Cosmos/AAC data access.

```bash
az login --tenant '<tenant-id>'
az account set --subscription '<development-subscription-id>'
```

Development uses `AzureCliCredential` for Cosmos/AAC, with no account-key or connection-string fallback.

## 3. Configure each application

- Configure each Presentation project's own user-secrets; AppHost secrets are not copied to children.
- Replace placeholders privately. User-secrets are outside Git but unencrypted; never commit filled-in examples.
- Generate distinct active/rotation keys, each 64 hexadecimal characters. Match the active key in its callers; MCP has two callers, Gateway and Orchestrator.

Generate a fresh value for each slot:

```bash
node -e "console.log(require('node:crypto').randomBytes(32).toString('hex'))"
```

### Orchestrator

```bash
dotnet user-secrets set --project src/Services/Orchestrator/MyAIAgent.Orchestrator.Presentation <<'JSON'
{
  "CosmosDb:Endpoint": "https://<cosmos-account>.documents.azure.com/",
  "CosmosDb:DatabaseName": "myaiagent-test",
  "AiProvider:Type": "AzureOpenAI",
  "AzureOpenAI:Endpoint": "https://<openai-resource>.openai.azure.com/",
  "AzureOpenAI:DeploymentName": "<chat-deployment-name>",
  "AzureOpenAI:ApiKey": "<provider-key>",
  "ServiceAuthorization:Enabled": "true",
  "ServiceAuthorization:AccessKey1": "<orchestrator-active-key>",
  "ServiceAuthorization:AccessKey2": "<orchestrator-rotation-key>",
  "McpServer:BaseUrl": "http://localhost:5255",
  "McpServer:AccessKey": "<mcp-active-key>"
}
JSON
```

For OpenAI, set `AiProvider:Type=OpenAI`, `OpenAI:ApiKey`, and `OpenAI:ModelId` in Orchestrator only. Leave `Urls` unset so Aspire/launch profiles control listening addresses.

### KernelMemory

```bash
dotnet user-secrets set --project src/Services/KernelMemory/MyAIAgent.KernelMemory.Presentation <<'JSON'
{
  "CosmosDb:Endpoint": "https://<cosmos-account>.documents.azure.com/",
  "CosmosDb:DatabaseName": "myaiagent-test",
  "KernelMemory:DefaultIndexName": "myaiagent-test",
  "KernelMemory:DocumentStorageType": "AzureBlobs",
  "KernelMemory:ServiceAuthorization:AccessKey1": "<kernel-memory-active-key>",
  "KernelMemory:ServiceAuthorization:AccessKey2": "<kernel-memory-rotation-key>",
  "KernelMemory:Services:AzureOpenAIText:Endpoint": "https://<openai-resource>.openai.azure.com/",
  "KernelMemory:Services:AzureOpenAIText:Deployment": "<text-deployment-name>",
  "KernelMemory:Services:AzureOpenAIText:APIKey": "<provider-key>",
  "KernelMemory:Services:AzureOpenAIEmbedding:Endpoint": "https://<openai-resource>.openai.azure.com/",
  "KernelMemory:Services:AzureOpenAIEmbedding:Deployment": "<embedding-deployment-name>",
  "KernelMemory:Services:AzureOpenAIEmbedding:APIKey": "<provider-key>",
  "KernelMemory:Services:AzureAISearch:Endpoint": "https://<search-service>.search.windows.net",
  "KernelMemory:Services:AzureAISearch:APIKey": "<search-admin-key>",
  "KernelMemory:Services:AzureBlobs:Auth": "ConnectionString",
  "KernelMemory:Services:AzureBlobs:ConnectionString": "<blob-connection-string>",
  "KernelMemory:Services:AzureBlobs:Container": "myaiagent-test",
  "Logging:LogLevel:Microsoft.KernelMemory": "Warning"
}
JSON
```

Use Azure **deployment names**, not model names. Under `KernelMemory:Services:`, match these settings to your models:

| Provider | Settings | Previously exercised example |
| --- | --- | --- |
| `AzureOpenAIText` | `MaxTokenTotal`, `Tokenizer` | `gpt-4.1-nano`: 128000 configured token budget, `o200k` |
| `AzureOpenAIEmbedding` | `MaxTokenTotal`, `Tokenizer`, `EmbeddingDimensions` | `text-embedding-3-small`: 8191, `cl100k`, 1536 |

Check compatibility and regional availability before copying these examples. Use `DocumentStorageType`, not deprecated `ContentStorageType`. Distributed ingestion and other vector providers are not wired. Keep private document/prompt logging at `Warning`.

### Gateway

```bash
dotnet user-secrets set --project src/Services/Gateway/MyAIAgent.Gateway.Presentation <<'JSON'
{
  "Orchestrator:BaseUrl": "http://localhost:5100",
  "Orchestrator:AccessKey": "<orchestrator-active-key>",
  "KernelMemory:BaseUrl": "http://localhost:5227",
  "KernelMemory:AccessKey": "<kernel-memory-active-key>",
  "McpServer:BaseUrl": "http://localhost:5255",
  "McpServer:AccessKey": "<mcp-active-key>"
}
JSON
```

These URLs match the launch profiles. The KernelMemory `5227` override replaces Gateway's tracked `9001` default. Clients read these settings; Aspire references do not replace them.

### MCP server

Authorization is enabled by default. Configure MCP's own store:

```bash
dotnet user-secrets set --project src/Services/McpServer/MyAIAgent.McpServer.Presentation <<'JSON'
{
  "ServiceAuthorization:Enabled": "true",
  "ServiceAuthorization:AccessKey1": "<mcp-active-key>",
  "ServiceAuthorization:AccessKey2": "<mcp-rotation-key>",
  "Tavily:ApiKey": "<tavily-api-key>"
}
JSON
```

- The same Tavily credentials serve search and webpage reading; discovery needs no provider key.
- Keep header `Authorization`. Disabling MCP authorization is allowed only in Development.
- Match `McpServer:BaseUrl` in both callers. Keys are sent only to that origin's `/`, `/mcp`, or `/mcp/`, with no query/fragment or redirects. Production requires HTTPS; Development permits loopback HTTP.

External MCP is blocked by default. To approve an origin, set Orchestrator user-secrets:

```json
{ "McpServer:AllowedExternalOrigins": ["https://mcp.example.com:8443"] }
```

Entries must be HTTPS origins without credentials, paths, queries, fragments, or wildcards. External endpoints may have paths/protocol queries, but must resolve to public addresses even in Development. Only the configured first-party service allows local/internal addresses. Existing agents must satisfy the policy too. Restart after key/origin changes; see [security](security.md#outbound-destination-policy).

### Optional AAC

Set `AppConfiguration:Endpoint` and `AppConfiguration:Label=Development` per consuming project; an empty endpoint keeps AAC off locally. Avoid AppHost environment secrets shared with all children.

Prefixes (`Gateway:`, `Orchestrator:`, `KernelMemory:`, `McpServer:`) are removed once during import:

- `Orchestrator:AzureOpenAI:DeploymentName` becomes `AzureOpenAI:DeploymentName`.
- `KernelMemory:CosmosDb:Endpoint` becomes `CosmosDb:Endpoint`.
- `KernelMemory:KernelMemory:Services:AzureOpenAIText:Deployment` becomes `KernelMemory:Services:AzureOpenAIText:Deployment`.

Use a per-service `Sentinel` for refresh. AAC holds nonsecret settings; user-secrets/environment/command-line overrides win. Restart AppHost after changes because clients may capture startup values.

### Central Tavily key through AAC

| Location | Setting |
| --- | --- |
| Key Vault | Secret `tavily-api-key` |
| AAC | Key `McpServer:Tavily:ApiKey`, label `Development`, type **Key Vault reference** |
| Reference | `https://<development-vault>.vault.azure.net/secrets/tavily-api-key` (versionless) |

The app resolves the reference with the same Azure CLI credential used for AAC. Grant that developer identity **Key Vault Secrets User** on this development secret. Production uses its explicitly assigned managed identity and needs its own grant; never reuse developer credentials there.

Configure the reference only after running the updated AAC loader. Resolution failure stops startup, even if a higher-priority local secret is present. Once verified, remove the McpServer `Tavily:ApiKey` user-secret to use AAC as the source. Restart AppHost after key rotation or configuration changes; immediate secret refresh is not promised. Keep plaintext keys out of AAC and Git.

## 4. Start Aspire and verify routing

Select `MyAIAgent.All` as the IDE startup project, or run:

```bash
dotnet run --project src/Aspire/MyAIAgent.All --launch-profile https
```

- `https` is the default example; `http` permits an unencrypted local dashboard with authentication still enabled.
- Gateway, Orchestrator, and MCP have matching `http`/`https` Development profiles. KernelMemory has only `https`, exposing both protocols; keep the development certificate trusted.
- Stop old AppHost instances before restarting. Restart after profile or secret changes.

| Service | HTTP | HTTPS |
| --- | --- | --- |
| Gateway | `http://localhost:5200` | `https://localhost:7200` |
| Orchestrator | `http://localhost:5100` | `https://localhost:7100` |
| KernelMemory | `http://localhost:5227` | `https://localhost:7074` |
| MCP | `http://localhost:5255` | `https://localhost:7117` |

Use dashboard endpoints when local ports differ. Verify without model or search calls:

| Request | Checks |
| --- | --- |
| `/health/live` on each service | Process availability |
| Gateway `GET /admin/agents` | Orchestrator forwarding/key and Cosmos; expect `isSuccess: true` |
| Gateway `GET /admin/tools` | MCP authorization/discovery; expect `isSuccess: true` |
| Gateway `/scalar/` | Interactive API reference; default document `kyle-ai-agent` |

**Keep Development routes private.** To try a paid model call, send Gateway `POST /chat` with `{"sessionId":null,"userMessage":"Hello","agentId":null}`.

With no default agent, chat has no tools. Create one through `/admin/agents` with `name`, `instructions`, `mcpServerEndpoint` (e.g. `http://localhost:5255/mcp`), `isDefault`, and `enabledTools`. Only explicitly named, enabled tools are exposed. Missing/empty lists, fully disabled entries, and unknown names grant no tools.

**Compatibility change:** empty lists previously enabled every discovered tool. Existing agents with those lists must be reconfigured explicitly; stored agents are not updated automatically. The clock tool was removed; Orchestrator supplies current UTC time in each chat turn. Agent administration currently supports create/list/delete, so replace an affected agent deliberately and use its new ID for new conversations. Existing conversations can select a replacement through their chat request's `agentId`.

### Enable automatic web search

1. Store `Tavily:ApiKey` in McpServer user-secrets (above), or use the [Key Vault reference](#central-tavily-key-through-aac) below.
2. Create or update an agent through Gateway `/admin/agents` with these fields, retaining the agent ID returned by the API:

   ```json
   {
     "name": "Web assistant",
     "instructions": "Answer clearly, check current facts when needed, and cite sources.",
     "mcpServerEndpoint": "http://localhost:5255/mcp",
     "isDefault": true,
     "enabledTools": ["web_search", "read_webpage"]
   }
   ```

3. Restart AppHost after configuration changes, then send a chat question needing current information. The model can call search and use the evidence automatically. Model and search usage are separate; no lookup is guaranteed on a particular prompt.

Try “What changed in the latest .NET release?”, “Summarize this article: https://example.com/article”, or “Compare these two documentation pages: URL1 and URL2”. General questions can be answered without a tool call.

`read_webpage` accepts one public HTTPS URL without credentials, query strings, IP literals, or a nonstandard port; fragments are removed. Local names and hosts with nonpublic DNS answers are rejected. Tavily retrieves the page; authenticated pages, OCR, and site crawling are unsupported. Extracted Markdown is limited to 8,000 characters and includes a truncation flag.

The first-party tools share a three-invocation limit per chat turn, including failed calls. Clients never retry automatically. Completed replies append up to ten safe, unique links under **Retrieved sources**, even if the model omits citations. Streaming delivers this as a final text delta; the same reply is persisted. The list identifies retrieved evidence and does not certify claims or remove unsupported links written by the model.

Basic search uses one Tavily credit per request; basic extraction uses one credit per five successful URLs. Check your [Tavily plan and usage](https://docs.tavily.com/documentation/api-credits); quotas can change. Calls and model tokens may incur charges; per-turn limits do not enforce a monthly budget. Do not include private information in queries or submitted URLs. Missing credentials, unreadable pages, or exhausted quotas produce safe tool errors.

Next: [document routes](architecture.md#document-ingestion-and-rag) or [public visitor API](public-chat-api.md).

## 5. Test with the console client

Keep Aspire running, then use a **separate interactive terminal**:

```bash
dotnet run --project src/Clients/MyAIAgent.ConsoleClient
```

The console reads `Gateway:BaseUrl` from its own `appsettings.json` (default `https://localhost:7200`).
Settings load from the executable directory, independently of the terminal's working directory.
Later sources override earlier ones:

1. `appsettings.json`
2. Optional `appsettings.<DOTNET_ENVIRONMENT>.json`, such as `appsettings.Development.json`
3. Environment variables, including `Gateway__BaseUrl`
4. `--gateway`

Edit `src/Clients/MyAIAgent.ConsoleClient/appsettings.json` and rebuild, or set `Gateway__BaseUrl` before starting.
The same file sets `McpServer:Endpoint`, the default offered when creating a tool-enabled agent.
Override it with `McpServer__Endpoint`; if unset, enter an MCP URL during agent creation.
This client setting does not change the backend's trusted MCP origin or service authorization.
Environment-specific JSON files are copied to build/publish output too; keep their values safe to publish.
Settings are captured at startup, so restart the console after changes. For a one-off HTTP profile override:

```bash
dotnet run --project src/Clients/MyAIAgent.ConsoleClient -- --gateway http://localhost:5200
```

Only HTTP(S) loopback service roots are accepted. TLS validation stays enabled. The client has no provider credentials, does not change Azure CLI context, and does not start services itself. If configuration access fails, select the subscription/tenant containing your development resources before restarting Aspire.

| Menu | What to test |
| --- | --- |
| **Connection check** | Gateway liveness; this does not check provider credentials or quota |
| **Select/create agent** | Select an existing agent, or explicitly create one with instructions and tool names. Creation does not change the default agent. |
| **New chat / Continue chat** | Type messages and see streamed replies; follow-up messages retain the session |
| **Conversations** | Page through history, read messages, open a saved chat, rename, or confirm deletion |
| **Tools** | Discover available tools without invoking Tavily |
| **Mode** | Switch between streaming `/chat/stream` and JSON `/chat` replies |

For a research agent, enable `web_search,read_webpage` and accept the local MCP endpoint when it matches your configuration. Then try an ordinary question, a current-facts search, and a public article summary. Chat can incur model usage; research can also consume Tavily credits.

During chat, `/help` explains commands, `/new` resets the session, `/back` returns to the menu, and `/exit` quits. Ctrl+C cancels an active request and returns control; when idle it exits. The client retains a received session ID after interruption. Inspect history before resending: cancellation or a missing `done` event does not prove the turn was unsaved.

Opening saved history leaves agent resolution to the server. Selecting another agent starts a new conversation. After opening saved history, `/new` uses the server default unless you explicitly select an agent.

**Local scope:** Development routes share `development:anonymous`; this client does not test multi-user isolation, public visitor JWTs, browser CORS, or production sign-in. Document uploads and standalone KernelMemory queries remain in Scalar/direct HTTP. Untrusted output is printed as plain text with terminal controls removed; Markdown links remain visible text. Nothing is exported or logged to disk by the client.

Automated client tests use mocked HTTP and no cloud access:

```bash
npm run quality -- --project ConsoleClient
```

The full `npm run quality` includes this suite alongside the four backend suites. Ordinary requests have a 30-second deadline; chat has a three-minute total deadline, including delayed JSON response headers. Streaming response headers have a separate 30-second deadline. There are no automatic retries. JSON responses are limited to 2 MiB and SSE responses to 8 MiB.

### Update the API client from JSON

Paste the complete OpenAPI document into `src/Services/Gateway/MyAIAgent.Gateway.Client/OpenApi/openapi.json`, save, and build ConsoleClient normally. Its project reference builds the reusable SDK and regenerates methods/models automatically. Generated C# stays under the SDK's ignored `obj/` directory.

No generation script, running API, Azure configuration, or operation whitelist is involved. See [API consumers and SDK generation](api-clients.md) for the copy/paste workflow, authentication, streaming, and maintainer contract checks.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| AAC endpoint validation at startup | The service must run in Development to load user-secrets. An enabled endpoint must be an HTTPS service root, without credentials, query, or fragment. |
| Cosmos startup failure | Endpoint syntax, Azure CLI tenant, `myaiagent-test`, and test-container data-plane permissions. |
| AAC 403 | The signed-in identity needs App Configuration data access; wait for new grants to propagate. |
| Downstream 401/403 | Each caller's active service key must match a target active/rotation key. For MCP check both Gateway and Orchestrator user-secrets and the exact trusted origin. |
| KernelMemory configuration error | Both provider endpoints/keys, deployment compatibility, distinct service keys, and `DocumentStorageType`. |
| Port already in use | Stop the previous AppHost/debug session; remove stale `Urls` overrides. |
| Healthy processes but requests fail | Liveness does not check provider credentials, model quota, Cosmos access, or Tavily configuration. |

### Playwright HTTPS certificate trust

For Playwright self-signed certificate errors, trust the public certificate in its Node process too:

```bash
dotnet dev-certs https --check --trust
mkdir -p artifacts/certs
dotnet dev-certs https --export-path "$PWD/artifacts/certs/localhost.pem" --format Pem
export NODE_EXTRA_CA_CERTS="$PWD/artifacts/certs/localhost.pem"
```

Run from the repository root. Omitting `--password` and `--no-password` exports only the public certificate to ignored `artifacts/`. Restart Playwright/its host from this shell; existing processes do not inherit the export. Re-export after certificate replacement. Keep TLS validation enabled; do not use `ignoreHTTPSErrors` or `NODE_TLS_REJECT_UNAUTHORIZED=0`.

## Optional live Cosmos tests

After granting the test-scoped identity and signing in:

```bash
COSMOS_TEST_ENDPOINT='https://<cosmos-account>.documents.azure.com/' \
  dotnet test tests/Services/Orchestrator/MyAIAgent.Orchestrator.Tests/MyAIAgent.Orchestrator.Tests.csproj \
  --filter FullyQualifiedName~Integration.Infrastructure.Repositories
```

Tests write temporary test-database records. Report failures and skips honestly; process health is not persistence verification.
