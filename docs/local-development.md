# Local development with Aspire

Build the solution, configure providers, and run four services with Aspire. AppHost starts processes; it does not provision Azure resources.

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
| Tavily | Free-tier API key for `web_search`; startup, discovery, and the clock work without it. Keep paid billing disabled if you want a free-only account. |

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

For OpenAI, set `AiProvider:Type=OpenAI`, `OpenAI:ApiKey`, and `OpenAI:ModelId` in Orchestrator only. Leave `Urls` unset so Aspire/launch profiles control listening addresses.

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

Use Azure **deployment names**, not model names. Under `KernelMemory:Services:`, match these settings to your models:

| Provider | Settings | Previously exercised example |
| --- | --- | --- |
| `AzureOpenAIText` | `MaxTokenTotal`, `Tokenizer` | `gpt-4.1-nano`: 128000 configured token budget, `o200k` |
| `AzureOpenAIEmbedding` | `MaxTokenTotal`, `Tokenizer`, `EmbeddingDimensions` | `text-embedding-3-small`: 8191, `cl100k`, 1536 |

Check compatibility and regional availability before copying these examples. Use `DocumentStorageType`, not deprecated `ContentStorageType`. Distributed ingestion and other vector providers are not wired. Keep private document/prompt logging at `Warning`.

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

These URLs match the launch profiles. The KernelMemory `5227` override replaces Gateway's tracked `9001` default. Clients read these settings; Aspire references do not replace them.

### MCP server

Authorization is enabled by default. Configure MCP's own store:

```bash
dotnet user-secrets set --project src/MyAIAgent.McpServer.Presentation <<'JSON'
{
  "ServiceAuthorization:Enabled": "true",
  "ServiceAuthorization:AccessKey1": "<mcp-active-key>",
  "ServiceAuthorization:AccessKey2": "<mcp-rotation-key>",
  "Tavily:ApiKey": "<tavily-api-key>"
}
JSON
```

- Tavily credentials are needed for web search only, not discovery or the clock.
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
dotnet run --project src/MyAIAgent.All --launch-profile https
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

With no default agent, chat has no tools. Create one through `/admin/agents` with `name`, `instructions`, `mcpServerEndpoint` (e.g. `http://localhost:5255/mcp`), `isDefault`, and optional `enabledTools`. A missing/empty tool list exposes all discovered tools; name tools explicitly to restrict them.

### Enable automatic web search

1. Store `Tavily:ApiKey` in McpServer user-secrets (above), or use the [Key Vault reference](#central-tavily-key-through-aac) below.
2. Create or update an agent through Gateway `/admin/agents` with these fields, retaining the agent ID returned by the API:

   ```json
   {
     "name": "Web assistant",
     "instructions": "Answer clearly, check current facts when needed, and cite sources.",
     "mcpServerEndpoint": "http://localhost:5255/mcp",
     "isDefault": true,
     "enabledTools": ["web_search"]
   }
   ```

3. Restart AppHost after configuration changes, then send a chat question needing current information. The model can call search and use the evidence automatically. Model and search usage are separate; no lookup is guaranteed on a particular prompt.

Basic search uses one Tavily credit per request; the client never retries automatically. Check your [Tavily plan and usage](https://docs.tavily.com/documentation/api-credits); quotas can change. Treat results as evidence to verify, not guaranteed facts. Do not include private information in search questions. No key or exhausted quota produces a tool error instead of an empty success.

Next: [document routes](architecture.md#document-ingestion-and-rag) or [public visitor API](public-chat-api.md).

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
  dotnet test src/MyAIAgent.Orchestrator.Tests/MyAIAgent.Orchestrator.Tests.csproj \
  --filter FullyQualifiedName~Integration.Infrastructure.Repositories
```

Tests write temporary test-database records. Report failures and skips honestly; process health is not persistence verification.
