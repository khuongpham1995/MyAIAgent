# Azure Container Apps delivery

The `CI` workflow validates pull requests and pushes to `main`. A successful `main`
run triggers `CD`: four images are built once, pushed to a shared Azure Container
Registry, deployed to staging, tested, and then promoted by digest to production.
The production job uses a protected GitHub Environment. A manual CD run with a
previously built commit SHA redeploys that release and is the rollback path.

For local deployment, install Node.js 20.6+ and run `npm ci --ignore-scripts` before running `npm run deploy:aca -- staging <gateway-image> <orchestrator-image> <kernel-memory-image> <mcp-image>`. CI/CD installs these locked tools automatically. The deployment command writes the validated gateway hostname to `GITHUB_OUTPUT` when running in Actions. Use `npm run deploy:smoke -- <gateway-fqdn> <Entra-audience>` for HTTPS readiness and authorization checks; it allows 30 attempts per retry phase, a 10-second delay, and 10/30-second request timeouts. Authenticated responses must contain `isSuccess: true` within a 1 MiB JSON body. Redirects are rejected so a service token cannot be forwarded. Azure deployment calls have a 30-minute timeout; token retrieval has a 30-second timeout.

## Bootstrap

Bootstrap requires an Azure identity allowed to create resources and role assignments. Routine CD identities must not have role-assignment or secret-reading permissions. No Azure resources are deployed by the local quality gate.

1. Create the shared registry with `infra/registry.bicep`. Deploy `infra/foundation.bicep` separately to staging and production resource groups. It creates the environment, logging workspace, RBAC Key Vault with purge protection/audit logs, and four service identities. Save `identityNames`, `identityPrincipalIds`, `vaultName`, `environmentName`, and `logAnalyticsWorkspaceId` from its outputs.
2. Provision Cosmos with `infra/cosmos.bicep` in a shared data resource group. It disables account-key authentication and creates `myaiagent-test` and `myaiagent-prod`, each with 400 RU/s shared throughput. `ChatSessions` uses `/UserId`, `AgentDefinitions` uses `/Id`, and `DocumentRecords` uses `/IndexName`. These names and partition keys must match the EF model. Creating extra databases or dedicated container throughput can exceed the free-tier allowance. Partition keys cannot be changed in place; plan a migration for existing data.
3. Provision a separate Azure App Configuration store for each environment with `infra/appconfiguration.bicep`, or harden existing stores equivalently. Supply `storeName`, `skuName`, and the foundation logging workspace ID explicitly. The template disables access keys and enables diagnostics; paid tiers also enable purge protection. **Review the selected tier and cost before deploying**; do not assume two free stores are available. Keep credentials out of AAC. Use keys such as `Gateway:PublicChat:AgentId` with the `staging` or `production` label. Labels/prefixes do not isolate permissions between environments.
4. Provide Azure AI Search using `infra/search.bicep` and Blob Storage for KernelMemory. Search Free has 50 MB, three indexes, no SLA, and may be removed when idle. The two environments currently share this service and its admin credential. Blob configuration uses `AzureBlobs` in deployment; verify document retrieval after restarting a replica.
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

   Active and rotation keys must differ. The optional public-chat signing secret must also exist before granting its access. **Do not create `cosmos-connection` or an AAC connection-string secret**; those services use identities.
6. Deploy `infra/access.bicep` in each application resource group **after the secrets exist**. Supply `stage`, the foundation `identityNames` object, `vaultName`, `registryName`/`registryResourceGroup`, `appConfigurationName`/`appConfigurationResourceGroup`, `cosmosAccountName`/`cosmosResourceGroup`, and optional `publicChatJwtVaultEntry`. It grants each service only its listed secrets, registry pull, AAC read, and required Cosmos containers in the environment database. Resource groups may differ but must be in the current subscription. Allow time for RBAC propagation before starting applications.
7. Grant developer identities Cosmos data-plane access only to the test containers, using `infra/modules/cosmos-access.bicep` with `databaseName=myaiagent-test`, the developer principal ID, and the needed container names. Developers use `az login` and `CosmosDb:Endpoint`; applications/tests never create resources. Do not grant developers access to production merely to enable local runs.
8. Register the single-tenant Entra API for Gateway. Set `api.requestedAccessTokenVersion` to `2`, expose `api://<application-client-id>`, and use the bare client GUID for `ENTRA_AUDIENCE`. Define `Chat.Access` and `Agent.Admin`, assign intended callers, and give deployment identities `Agent.Admin` for smoke tests. Gateway derives owners from validated `tid`/`oid` claims; clients cannot select a different owner.
9. Create `staging` and `production` GitHub Environments. Bind separate Azure federated identities to `repo:<owner>/<repo>:environment:staging` and `...:production`. Grant staging deployment rights plus `AcrPush`; grant production only its deployment rights. Keep both identities separate from runtime identities. Store their `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` in the corresponding GitHub Environment. Use the protection inspection/application script described in [security.md](security.md) to restrict environment deployments and protect the default branch.

### Migrating the previous shared identity/key setup

Before deploying the new revision, provision service identities and grants, set the new CD variables below, and validate access. Remove the old vault-wide `Key Vault Secrets User` assignment and detach the previous shared runtime identity from all apps. Incremental Bicep deployments do not delete old identities or role assignments automatically. After all consumers have migrated, remove obsolete `cosmos-connection`/AAC connection-string secrets and revoke old keys. Disabling Cosmos/AAC access keys immediately breaks old key-based consumers; coordinate this as a migration instead of applying it blindly to a live account. No data migration is included here.

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

Optional variables override the browser chat API settings described in
[public-chat-api.md](public-chat-api.md). Empty values are omitted so labeled AAC
settings can take effect. Public chat stays off when neither source configures it;
to disable an AAC-configured feature, remove its AAC configuration rather than
setting a deployment variable to an empty value. Signing secrets remain in Key Vault.

| Variable | Purpose |
| --- | --- |
| `PUBLIC_CHAT_ISSUER`, `PUBLIC_CHAT_AUDIENCE` | `iss` and `aud` of the tokens your backend issues |
| `PUBLIC_CHAT_JWKS_URI` | HTTPS JWKS endpoint for asymmetric signing keys |
| `PUBLIC_CHAT_SIGNING_KEY_SECRET` | Name of a Key Vault secret holding an HS256 key; use instead of the JWKS URI |
| `PUBLIC_CHAT_AGENT_ID` | Orchestrator agent id that public visitors talk to |
| `PUBLIC_CHAT_ALLOWED_ORIGIN` | Browser origin allowed to call the public API, for example `https://app.example.com`; required once public chat is enabled |

The two environments must use the **same** `ACR_NAME` and
`ACR_RESOURCE_GROUP` so production receives the exact staging image digests.
Do not put API keys or connection strings in GitHub variables, workflow logs,
or committed parameter files. Container Apps resolves Key Vault references
through its runtime identity.

## Operations

- A pull request runs the full project quality gate and compiles all Bicep
  templates. It does not receive an Azure identity.
- A successful push to `main` deploys staging. The smoke test waits for
  readiness, requires HTTP 401 for anonymous admin access, and requires HTTP
  200 for an `Agent.Admin` token. Production waits for environment approval.
- To roll back, run **CD** manually from `main` with `release_sha` set to a
  prior successful release commit. The workflow verifies that the SHA is on
  `main`, reads existing ACR digests without rebuilding, and repeats staging
  and production promotion. Keep previous image manifests in ACR.
  Delivery scripts and infrastructure come from the pinned workflow revision;
  release source is checked out separately for image builds. Historical releases
  do not need to contain the current TypeScript tools. Confirm that a rollback
  image supports the current configuration and infrastructure contracts, especially
  across the identity migration described above.
- An app deployment failure leaves the existing healthy single revision
  serving traffic. If the production smoke test fails after deployment, run
  the rollback workflow. Check ACA revision and system logs for the cause.

Naming changed twice: the gateway was renamed from `MainSubSys`, and the `myaia` /
`myaiaagent` prefixes became `myaiagent`. Container apps are now
`myaiagent-<stage>-<service>` (the gateway was `myaia-<stage>-mainsubsys`) and registry
repositories are `myaiagent-<service>` (previously `myaiaagent-<service>`). The first
deployment therefore creates new apps with new FQDNs while the old ones keep running:
repoint clients, then delete the old apps by hand. Manual rollbacks to commits built
before the rename cannot find images under the new repository names. Re-running
`foundation.bicep` with the new prefix would also create a second Log Analytics workspace,
Container Apps environment, runtime identity and Key Vault (the vault starts empty); keep
using the existing foundation resources, whose names the workflow takes from GitHub
variables, unless you intend to rebuild them.

Only Gateway has external ingress. Orchestrator, KernelMemory and MCP are
reachable only inside the Container Apps environment. The Orchestrator trusts the
user id it is given, so it requires the shared service key (`ServiceAuthorization`)
and refuses to start in production without it; Gateway sends the key. KernelMemory
keeps its API-key filter enabled and Gateway sends that key too. MCP's internal API
filter is still disabled. Restrict network access to the Container Apps environment and review
service-to-service authentication before adding more callers.
The MCP page fetcher accepts only public HTTP(S) destinations on standard ports,
checks each redirect, and limits a page response to 1 MiB. Outbound DNS and
network policy should also deny access to private and metadata endpoints.

The readiness endpoints currently indicate that the ASP.NET process has
started. They do not query Azure OpenAI, Cosmos DB, Blob Storage, or Google.
The staging smoke test exercises a real gateway-to-Orchestrator request. Add
dependency-specific diagnostics if those services become operationally critical.
Existing persisted agents with localhost MCP endpoints must be updated to the
internal `http://myaiagent-<stage>-mcpserver` address before those tools can run in ACA.
Validate document upload size and request duration against ACA ingress limits in
staging before enabling large uploads.
