# Azure Container Apps delivery

Provision Azure dependencies and deliver four services to Container Apps. This describes configured automation, not a completed production deployment. For Aspire, see [local development](local-development.md).

**Checks:** PRs and `main` pushes run CI.

**Release:** successful `main` CI → build four images once → staging + smoke tests → production approval → promote the same digests. Manual CD with a previous release SHA provides rollback.

## Delivery commands

Install Node.js 20.6+, then run from the repository root:

```bash
npm ci --ignore-scripts
npm run deploy:aca -- staging <gateway-image> <orchestrator-image> <kernel-memory-image> <mcp-image>
npm run deploy:smoke -- <gateway-fqdn> <Entra-audience>
```

CI/CD installs the same locked tools. Deployment writes the validated Gateway hostname to `GITHUB_OUTPUT` in Actions.

| Smoke/deployment check | Bound |
| --- | --- |
| HTTPS readiness and authorization | 30 attempts per phase, 10-second delays; 10/30-second request timeouts |
| Authenticated requests | `/admin/agents` and `/admin/tools`; `isSuccess: true`, JSON ≤ 1 MiB; no model/Google call |
| Redirects | Rejected to prevent token forwarding |
| Azure deployment / token retrieval | 30 minutes / 30 seconds |

## Bootstrap

Use a privileged bootstrap identity for resources/role assignments. Routine CD must not read secrets or assign roles. The local quality gate deploys nothing.

1. **Foundation:** create the shared registry with `infra/registry.bicep`. Deploy `infra/foundation.bicep` per environment: Container Apps environment, logs, protected/audited RBAC vault, four identities. Save `identityNames`, `identityPrincipalIds`, `vaultName`, `environmentName`, `logAnalyticsWorkspaceId`.
2. **Cosmos:** deploy `infra/cosmos.bicep` in the shared data group. It disables account keys and creates `myaiagent-test` / `myaiagent-prod`, each with 400 RU/s shared throughput. Match EF partitions: `ChatSessions` → `/UserId`, `AgentDefinitions` → `/Id`, `DocumentRecords` → `/IndexName`. Extra throughput/databases can exceed free-tier limits; partition changes need migration.
3. **AAC:** deploy `infra/appconfiguration.bicep` per environment, or harden existing stores. Supply `storeName`, `skuName`, and logging workspace ID. Access keys are disabled; diagnostics enabled; paid tiers add purge protection. **Review costs; two free stores are not guaranteed.** Use nonsecret keys such as `Gateway:PublicChat:AgentId` with `staging` / `production` labels. Labels/prefixes do not isolate access.
4. **Documents:** provide Search (`infra/search.bicep`) and Blob Storage (`AzureBlobs`). Search Free has 50 MB, three indexes, no SLA, and may be removed when idle. Both environments share Search/admin credentials. Verify retrieval after a replica restart.
5. Populate the following secrets in each environment vault with distinct environment values where the provider supports separate keys:

   | Secret | Consumers |
   | --- | --- |
   | `azure-openai-key` | Orchestrator, KernelMemory |
   | `blob-connection`, `search-admin-key` | KernelMemory |
   | `orchestrator-key` | Gateway, Orchestrator |
   | `orchestrator-rotation-key` | Orchestrator |
   | `kernel-memory-key` | Gateway, KernelMemory |
   | `kernel-memory-rotation-key` | KernelMemory |
   | `google-search-key` | McpServer |
   | `mcp-key` | Gateway, Orchestrator, McpServer |
   | `mcp-rotation-key` | McpServer |

   Keep active/rotation keys distinct. Create any public-chat signing secret before its grant. **No `cosmos-connection` or AAC connection-string secret:** these use identities.
6. **Runtime grants:** deploy `infra/access.bicep` per app group **after secrets exist**. Supply `stage`, `identityNames`, `vaultName`, `registryName`/`registryResourceGroup`, `appConfigurationName`/`appConfigurationResourceGroup`, `cosmosAccountName`/`cosmosResourceGroup`, and optional `publicChatJwtVaultEntry`. Grants cover scoped secrets, registry pull, AAC read, and environment Cosmos containers. Resource groups must be in the current subscription; allow RBAC propagation.
7. **Developers:** grant only test-container Cosmos data access with `infra/modules/cosmos-access.bicep`, `databaseName=myaiagent-test`, principal ID, and container names. Use `az login` / `CosmosDb:Endpoint`; never grant production access for local tests.
8. **Gateway Entra API:** single tenant, `api.requestedAccessTokenVersion=2`, exposed URI `api://<application-client-id>`, bare client GUID for `ENTRA_AUDIENCE`. Define/assign `Chat.Access` and `Agent.Admin`; deployment identities need `Agent.Admin` for smoke tests. Owners derive from validated `tid`/`oid`.
9. **GitHub Environments:** create `staging` / `production` with separate federated identities bound to `repo:<owner>/<repo>:environment:<stage>`. Staging needs deployment + `AcrPush`; production deployment only. Keep these separate from runtime identities. Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` per environment. Apply [repository protections](security.md#repository-controls).

### Migrating the previous shared identity/key setup

1. Provision service identities/grants, set CD variables, and validate access.
2. Remove old vault-wide `Key Vault Secrets User` grants and detach the shared runtime identity. Incremental Bicep does not remove old assignments/identities.
3. After all callers migrate, remove obsolete Cosmos/AAC secrets and revoke keys. Disabling account keys breaks unmigrated callers. No data migration is included.

Set the following GitHub Environment variables in both environments:

| Variable | Purpose |
| --- | --- |
| `RESOURCE_GROUP` | Resource group for that environment |
| `ACR_NAME`, `ACR_RESOURCE_GROUP` | Shared registry name and resource group |
| `APP_ENVIRONMENT_NAME` | Output from `foundation.bicep` |
| `KEY_VAULT_NAME` | Environment vault from foundation |
| `GATEWAY_IDENTITY_NAME`, `ORCHESTRATOR_IDENTITY_NAME`, `KERNEL_MEMORY_IDENTITY_NAME`, `MCP_IDENTITY_NAME` | Four identities from foundation |
| `APP_CONFIG_ENDPOINT` | Environment AAC endpoint; no access key |
| `COSMOS_ENDPOINT` | Shared Cosmos account endpoint; access is scoped by identity |
| `SEARCH_ENDPOINT` | Azure AI Search service endpoint |
| `ENTRA_TENANT_ID`, `ENTRA_AUDIENCE` | Gateway tenant GUID and bare API client ID GUID |
| `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT` | Chat provider settings |
| `AZURE_EMBEDDING_DEPLOYMENT`, `AZURE_TEXT_DEPLOYMENT` | KernelMemory provider settings |
| `GOOGLE_SEARCH_ENGINE_ID` | MCP search engine identifier |

Optional [public-chat settings](public-chat-api.md) override AAC when nonempty. Empty values are omitted: remove AAC settings to disable an AAC-configured feature. Public chat stays off when neither source configures it. Signing secrets stay in Key Vault.

| Variable | Purpose |
| --- | --- |
| `PUBLIC_CHAT_ISSUER`, `PUBLIC_CHAT_AUDIENCE` | `iss` and `aud` of the tokens your backend issues |
| `PUBLIC_CHAT_JWKS_URI` | HTTPS JWKS endpoint for asymmetric signing keys |
| `PUBLIC_CHAT_SIGNING_KEY_SECRET` | Name of a Key Vault secret holding an HS256 key; use instead of the JWKS URI |
| `PUBLIC_CHAT_AGENT_ID` | Orchestrator agent id that public visitors talk to |
| `PUBLIC_CHAT_ALLOWED_ORIGIN` | Browser origin allowed to call the public API, for example `https://app.example.com`; required once public chat is enabled |

Use the **same** `ACR_NAME` / `ACR_RESOURCE_GROUP` in both environments for digest promotion. Never put secrets in GitHub variables, logs, or committed parameters. Container Apps resolves vault references through runtime identity.

## Operations

- **PR:** quality gate + Bicep compile, without Azure identity.
- **Staging:** after successful `main` CI, smoke checks readiness, anonymous admin 401, and `Agent.Admin` 200. Production requires approval.
- **Rollback:** run CD from `main` with a prior successful `release_sha`. The SHA must be on `main`; existing ACR digests are reused. Keep old manifests and verify image compatibility with current settings/infrastructure, especially identity changes. Scripts/infrastructure come from the pinned workflow revision; release source is checked out separately.
- **Failure:** a failed app deployment leaves the healthy single revision serving. Roll back failed production smoke checks; inspect ACA revision/system logs.

### Renamed resources

| Resource | Current name | Previous name |
| --- | --- | --- |
| Container app | `myaiagent-<stage>-<service>` | Gateway: `myaia-<stage>-mainsubsys` (`MainSubSys`) |
| Image repository | `myaiagent-<service>` | `myaiaagent-<service>` |

Renamed apps get new FQDNs; repoint clients before manually deleting old apps. Pre-rename rollback images are absent under new repository names. Re-running foundation with a new prefix also creates duplicate logs/environment/identity/vault resources, with an empty vault. Retain existing foundation names in GitHub variables unless rebuilding intentionally.

### Service access and verification

- Only Gateway has external ingress. Keep Orchestrator, KernelMemory, and MCP internal; shared keys are required outside Development. Provision keys/grants before rollout. See [security](security.md#mcp-service-access) for scope and rotation.
- Both MCP callers use `https://myaiagent-<stage>-mcpserver.internal.<environment-default-domain>`, without redirects. Update stored localhost agent endpoints to this configured origin, optionally `/mcp`.
- External MCP is blocked unless approved in `McpServer:AllowedExternalOrigins`. Use AAC entries such as `Orchestrator:McpServer:AllowedExternalOrigins:0` with the environment label, or `McpServer__AllowedExternalOrigins__0`. Restart Orchestrator. Existing agents are not rewritten; see [outbound policy](security.md#outbound-destination-policy).
- First-party MCP permits internal addresses. External MCP/pages reject private, metadata, special-purpose, and mixed DNS answers. Add network egress restrictions where needed.
- Health/readiness reports process startup, not OpenAI/Cosmos/Blob/Google health. Smoke tests exercise Orchestrator and MCP discovery; add dependency diagnostics as needed.
- Validate document sizes/durations against ACA ingress limits in staging. Production and multi-replica behavior still need live validation.
