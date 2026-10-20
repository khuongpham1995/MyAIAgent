# Local development with Aspire

This guide takes a new checkout from an offline build to four locally running services. Azure resources remain external to Aspire; starting AppHost does not provision them.

## 1. Build before connecting providers

Install Git, .NET 10 SDK, Node.js 20.6+, and Azure CLI. HTTPS profiles also require a trusted .NET development certificate. Run from the repository root:

```bash
npm ci --ignore-scripts
npm run quality
dotnet dev-certs https --trust
```

The default gate does not need Azure credentials. Cosmos integration tests are skipped when `COSMOS_TEST_ENDPOINT` is unset. Docker is used by the workflow-validation command; the AppHost currently starts .NET project processes rather than local database containers.

## 2. Prepare Azure dependencies

Use a development subscription/resource group and review service tiers and usage costs before provisioning. Budget alerts are notifications, not a hard spending cap. The [deployment guide](deployment.md) describes infrastructure templates and access grants; a local setup does not require deploying Container Apps, the registry, or production identities.

| Dependency | Local requirement |
| --- | --- |
| Cosmos DB for NoSQL | `myaiagent-test` with `ChatSessions` (`/UserId`), `AgentDefinitions` (`/Id`), and `DocumentRecords` (`/IndexName`). Runtime does not create containers. |
| Azure OpenAI | Available chat/text and embedding deployments, endpoint, and provider key. KernelMemory requires these even if Orchestrator uses OpenAI. |
| Azure AI Search | Service endpoint and admin key for KernelMemory index operations. |
| Azure Blob Storage | Account connection string and a development container for persistent documents in the configuration below. |
| Azure App Configuration | Optional locally. Endpoint plus App Configuration Data Reader access when enabled. |
| Google Custom Search | API key and search engine ID to exercise `web_search`; date/time and service startup do not require Google calls. |

Provision Cosmos/Search through their Bicep templates or equivalent Azure configuration; those templates do not provision OpenAI deployments or Blob Storage. Give the signed-in developer Cosmos data access scoped to the required **test containers**, not production. Azure subscription management rights alone do not grant Cosmos/AAC data access.

```bash
az login --tenant '<tenant-id>'
az account set --subscription '<development-subscription-id>'
```

Cosmos and AAC use `AzureCliCredential` in Development. Do not supply Cosmos account keys or AAC connection strings.

## 3. Configure each application

Gateway, Orchestrator, KernelMemory, and MCP have separate user-secrets stores. Configure the owning Presentation project; AppHost secrets are not copied into child applications. Keep public `appsettings*.json` files unchanged.

The JSON blocks below use `dotnet user-secrets set` through standard input. Replace placeholders privately; never commit a filled-in configuration file. The user-secrets store is outside Git but is not encrypted.

Create distinct active and rotation service keys, using 64 hexadecimal characters for each key. Use the same active key in the corresponding Gateway setting; also set the MCP active key in Orchestrator. For example, generate one value locally with `node -e "console.log(require('node:crypto').randomBytes(32).toString('hex'))"`; generate a fresh value for each slot. Do not use the example placeholder text as a key.

### Orchestrator

```bash
dotnet user-secrets set --project src/MyAIAgent.Orchestrator.Presentation <<'JSON'
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

To use OpenAI for Orchestrator instead, set `AiProvider:Type=OpenAI`, `OpenAI:ApiKey`, and `OpenAI:ModelId`. This affects Orchestrator only. Do not set a `Urls` user-secret: the launch profiles and Aspire assign listening addresses.

### KernelMemory

```bash
dotnet user-secrets set --project src/MyAIAgent.KernelMemory.Presentation <<'JSON'
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

Use actual Azure deployment names, which may differ from model names. Set `KernelMemory:Services:AzureOpenAIText:MaxTokenTotal` and `:Tokenizer`, and the corresponding `AzureOpenAIEmbedding:MaxTokenTotal`, `:Tokenizer`, and `:EmbeddingDimensions`, to match the deployed models. For example, `text-embedding-3-small` at its default dimension uses 1536 dimensions, `cl100k`, and an 8191-token input limit; `gpt-4.1-nano` uses `o200k` and a 128000-token budget is a conservative configured context limit. These are compatibility examples from the previously exercised setup, not a guarantee of model availability in your region. Check your deployment before copying them.

Do not use the deprecated `ContentStorageType` setting. The tracked defaults choose Azure OpenAI and Azure AI Search with in-process orchestration. Distributed ingestion and other vector providers are not wired. Avoid verbose KernelMemory logging when working with private documents or prompts.

### Gateway

```bash
dotnet user-secrets set --project src/MyAIAgent.Gateway.Presentation <<'JSON'
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

These addresses match the checked-in service launch profiles. Gateway's tracked KernelMemory URL is `http://localhost:9001`, so the `5227` override is required for this Aspire setup. Existing typed clients read these settings; `.WithReference(...)` alone does not override them.

### MCP server

MCP authorization is enabled by default. Configure its own user-secrets store:

```bash
dotnet user-secrets set --project src/MyAIAgent.McpServer.Presentation <<'JSON'
{
  "ServiceAuthorization:Enabled": "true",
  "ServiceAuthorization:AccessKey1": "<mcp-active-key>",
  "ServiceAuthorization:AccessKey2": "<mcp-rotation-key>",
  "GoogleSearch:ApiKey": "<google-search-key>",
  "GoogleSearch:SearchEngineId": "<search-engine-id>"
}
JSON
```

Google settings are needed only for web search, not tool discovery or the clock tool. Keep the default service header name `Authorization`. For an isolated local experiment, MCP can explicitly set `ServiceAuthorization:Enabled=false` in Development; disabling it outside Development is rejected at startup.

Both callers scope the MCP key to the exact origin in `McpServer:BaseUrl` and the paths `/`, `/mcp`, or `/mcp/`, without a query or fragment. Set matching origins in Gateway and Orchestrator. Other agent endpoints never receive this key. Outside Development the trusted origin must use HTTPS; locally HTTP is permitted only for loopback. Redirects are not followed. Restart all affected processes after changing keys or origins.

External MCP origins are blocked by default. To approve one, set an array in Orchestrator's user-secrets, for example `"McpServer:AllowedExternalOrigins": ["https://mcp.example.com:8443"]`. Entries must be HTTPS origins without credentials, paths, queries, fragments, or wildcards. External endpoints may use paths and protocol queries, but must resolve only to public addresses; local/private external servers are not allowed even in Development. The configured first-party MCP origin is the explicit exception for local Aspire and internal deployment. Existing agent endpoints must match either that service's protocol routes or an approved external origin. Restart Orchestrator after changing the list. See [security](security.md#outbound-destination-policy) for connection and DNS enforcement.

### Optional AAC

To use AAC, set `AppConfiguration:Endpoint` and `AppConfiguration:Label=Development` in each consuming project's user-secrets. Leave the endpoint empty to use only local configuration. MCP also loads its per-project user-secrets; avoid setting service-specific secrets on AppHost environment variables that reach every child process.

Each project already declares its key prefix: `Gateway:`, `Orchestrator:`, `KernelMemory:`, or `McpServer:`. Prefixes are removed once when importing settings. For example:

- `Orchestrator:AzureOpenAI:DeploymentName` becomes `AzureOpenAI:DeploymentName`.
- `KernelMemory:CosmosDb:Endpoint` becomes `CosmosDb:Endpoint`.
- `KernelMemory:KernelMemory:Services:AzureOpenAIText:Deployment` becomes `KernelMemory:Services:AzureOpenAIText:Deployment`.

Use the `Development` label and an appropriate per-service `Sentinel` key for refresh. Store nonsecret settings in AAC; keys and connection strings stay in local user-secrets. Local secrets/environment/command-line values override AAC. Existing clients may capture configuration at startup, so restart AppHost after changes.

## 4. Start Aspire and verify routing

Select `MyAIAgent.All` as the IDE startup project, or run:

```bash
dotnet run --project src/MyAIAgent.All --launch-profile https
```

The `http` AppHost profile is also available and explicitly permits local unencrypted dashboard transport; dashboard authentication stays enabled. KernelMemory currently has only an `https` service profile, so it still exposes HTTPS as well as HTTP when AppHost uses its `http` profile. Retain the development certificate setup.

Gateway, Orchestrator, and MCP have matching `http`/`https` profiles that set both environment variables to Development. Stop an older AppHost before starting another instance to avoid port conflicts. Restart AppHost after editing profiles or secrets.

| Service | HTTP | HTTPS |
| --- | --- | --- |
| Gateway | `http://localhost:5200` | `https://localhost:7200` |
| Orchestrator | `http://localhost:5100` | `https://localhost:7100` |
| KernelMemory | `http://localhost:5227` | `https://localhost:7074` |
| MCP | `http://localhost:5255` | `https://localhost:7117` |

Use the dashboard's displayed endpoints if local settings differ. Check `/health/live` on each service, then `GET http://localhost:5200/admin/agents`; the latter exercises Gateway forwarding, the Orchestrator key, and Cosmos. Also check `GET http://localhost:5200/admin/tools` to verify Gateway-to-MCP authentication and discovery without invoking a model or Google search. A successful envelope has `isSuccess: true`. Local Gateway routes use Development identity; do not expose them publicly.

Gateway's interactive API documentation is available at `/scalar/` on its HTTP or HTTPS endpoint. It selects the `kyle-ai-agent` OpenAPI document by default.

To try chat, send `POST /chat` to Gateway with `{"sessionId":null,"userMessage":"Hello","agentId":null}`. This consumes model usage. With no default agent, chat runs without tools. To use tools, create an agent through `/admin/agents` with `name`, `instructions`, `mcpServerEndpoint` (for example `http://localhost:5255/mcp`), `isDefault`, and optional `enabledTools`. A missing or empty tool allowlist currently exposes all discovered tools; explicitly choose names when restricting tools.

Document upload/search/ask examples and current routes are in [architecture](architecture.md). Public visitor authentication requires the additional setup in [public-chat-api.md](public-chat-api.md).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| AAC endpoint validation at startup | The service must run in Development to load user-secrets. An enabled endpoint must be an HTTPS service root, without credentials, query, or fragment. |
| Cosmos startup failure | Endpoint syntax, Azure CLI tenant, `myaiagent-test`, and test-container data-plane permissions. |
| AAC 403 | The signed-in identity needs App Configuration data access; wait for new grants to propagate. |
| Downstream 401/403 | Each caller's active service key must match a target active/rotation key. For MCP check both Gateway and Orchestrator user-secrets and the exact trusted origin. |
| KernelMemory configuration error | Both provider endpoints/keys, deployment compatibility, distinct service keys, and `DocumentStorageType`. |
| Port already in use | Stop the previous AppHost/debug session; remove stale `Urls` overrides. |
| Healthy processes but requests fail | Liveness does not check provider credentials, model quota, Cosmos access, or Google configuration. |

### Playwright HTTPS certificate trust

The browser and Playwright's Node HTTP client can use different certificate stores. If `.NET` reports a trusted development certificate but Playwright API requests fail with a self-signed certificate error, export the public certificate and supply it to the Node process that starts Playwright:

```bash
dotnet dev-certs https --check --trust
mkdir -p artifacts/certs
dotnet dev-certs https --export-path "$PWD/artifacts/certs/localhost.pem" --format Pem
export NODE_EXTRA_CA_CERTS="$PWD/artifacts/certs/localhost.pem"
```

Run these commands from the repository root. Omitting `--password` and `--no-password` exports only the public certificate. The output stays in the ignored `artifacts/` directory. Start or restart Playwright from this shell so Node reads `NODE_EXTRA_CA_CERTS` at process startup; an already running IDE or MCP server does not inherit a later shell export. Re-export after replacing the development certificate. Keep certificate validation enabled; do not use `ignoreHTTPSErrors` or `NODE_TLS_REJECT_UNAUTHORIZED=0` as a substitute for trust.

## Optional live Cosmos tests

After granting the test-scoped identity and signing in:

```bash
COSMOS_TEST_ENDPOINT='https://<cosmos-account>.documents.azure.com/' \
  dotnet test src/MyAIAgent.Orchestrator.Tests/MyAIAgent.Orchestrator.Tests.csproj \
  --filter FullyQualifiedName~Integration.Infrastructure.Repositories
```

These tests write temporary records in the test database. Treat failures as evidence to investigate; do not describe skipped tests or process health as successful live repository verification.
