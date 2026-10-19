# Azure Container Apps delivery

The `CI` workflow validates pull requests and pushes to `main`. A successful `main`
run triggers `CD`: four images are built once, pushed to a shared Azure Container
Registry, deployed to staging, tested, and then promoted by digest to production.
The production job uses a protected GitHub Environment. A manual CD run with a
previously built commit SHA redeploys that release and is the rollback path.

## Bootstrap

1. Create a shared resource group and separate staging and production resource
   groups. Deploy `infra/registry.bicep` once to the shared group. Deploy
   `infra/foundation.bicep` to each application group, passing `stage=staging`
   or `stage=production`. This creates the Container Apps environment, Log
   Analytics workspace, Key Vault, and runtime managed identity. Run these
   commands with an Azure identity allowed to create resources and role
   assignments. The CD identity does not need that permission.
2. Grant each runtime identity `AcrPull` on the shared registry. The foundation
   template grants `Key Vault Secrets User` on its own vault. Populate these
   secrets in **each** vault: `cosmos-connection`, `azure-openai-key`,
   `blob-connection`, `kernel-memory-key`, `kernel-memory-rotation-key`,
   `orchestrator-key`, `orchestrator-rotation-key`, and `google-search-key`. The
   two KernelMemory keys must differ, and so must the two Orchestrator keys
   (Gateway sends `orchestrator-key`; the second one lets you rotate without
   downtime). The deployment fails if a referenced vault secret is missing, so
   create the two Orchestrator secrets before the next deploy. Use distinct
   values per environment. Add `search-admin-key` (the Azure AI Search admin
   key) to each vault as well. The Cosmos databases and containers are created
   by `infra/cosmos.bicep` (below); production never calls `EnsureCreated`.
   Deploy once, with an identity allowed to create resources:
   `az deployment group create -g <rg> -f infra/cosmos.bicep -p accountName=<unique>`
   and `-f infra/search.bicep -p searchName=<unique>`. Cosmos uses the free tier
   (first 1000 RU/s and 25 GB per account, one account per subscription, chosen
   only at creation): one account holds the databases `myaiagent-test` (local
   development, tests, staging) and `myaiagent-prod`, each with 400 RU/s of shared
   throughput. `ChatSessions` is partitioned by `/UserId`; Cosmos cannot change a partition key, so a container created
   earlier with a different key must be deleted and recreated. Do not give any container dedicated throughput or add databases,
   or the account starts billing. Azure AI Search uses the Free tier (50 MB, 3
   indexes, no SLA; an idle service may be deleted, so ingested documents may
   need re-uploading) with the indexes `myaiagent-test` and `myaiagent-prod`. Put the
   Cosmos connection string into `cosmos-connection` and set the GitHub
   variable `SEARCH_ENDPOINT` to the search service endpoint. Staging and
   production share the Cosmos account key, so treat both vaults as holding a
   production credential, and never give PR workflows that key.
3. Provide Azure Blob Storage for KernelMemory documents. The production
   template selects `AzureBlobs` for document and content storage, with its
   connection string from Key Vault. Test document retrieval after restarting
   the KernelMemory replica before accepting production traffic.
4. Register a single-tenant Microsoft Entra API for Gateway. Set the app
   manifest's `api.requestedAccessTokenVersion` to `2`, and expose the App ID
   URI `api://<application-client-id>`. Set `ENTRA_AUDIENCE` to the bare
   application client ID GUID, which is the `aud` claim in v2 access tokens.
   Define app roles `Chat.Access` and
   `Agent.Admin`. Assign the intended users or client applications to the
   appropriate roles. Assign `Agent.Admin` to each deployment identity so the
   authenticated smoke test can call `/admin/agents`. Gateway validates
   bearer tokens and roles in production; local Development remains anonymous.
   This API does not provide a browser sign-in page. Clients obtain an Entra
   access token for `api://<application-client-id>` and send it as a bearer token.
   Chat sessions are bound to the token's `tid` and `oid` claims. A session
   id that the caller does not own starts a new conversation instead of resuming one.
5. Create GitHub Environments named `staging` and `production`. Protect
   `production` with required reviewers and restrict deployments to `main`.
   Configure separate Azure workload identities with federated credentials
   bound to `repo:<owner>/<repo>:environment:staging` and
   `repo:<owner>/<repo>:environment:production`. Grant the staging identity
   deployment rights on staging, `AcrPush` and registry read access; grant the
   production identity deployment rights on production. Both identities need
   an `Agent.Admin` app-role assignment for the smoke test. Store the Azure
   client, tenant, and subscription IDs as each environment's
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` secrets.

Set the following GitHub Environment variables in both environments:

| Variable | Purpose |
| --- | --- |
| `RESOURCE_GROUP` | Resource group for that environment |
| `ACR_NAME`, `ACR_RESOURCE_GROUP` | Shared registry name and resource group |
| `APP_ENVIRONMENT_NAME` | Output from `foundation.bicep` |
| `KEY_VAULT_NAME`, `RUNTIME_IDENTITY_NAME` | Outputs from `foundation.bicep` |
| `ENTRA_TENANT_ID`, `ENTRA_AUDIENCE` | Gateway tenant GUID and bare API client ID GUID |
| `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT` | Chat provider settings |
| `AZURE_EMBEDDING_DEPLOYMENT`, `AZURE_TEXT_DEPLOYMENT` | KernelMemory provider settings |
| `GOOGLE_SEARCH_ENGINE_ID` | MCP search engine identifier |

Optional variables enable the browser chat API described in
[public-chat-api.md](public-chat-api.md); it stays off while they are empty.

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
