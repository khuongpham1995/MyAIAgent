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
   `blob-connection`, `kernel-memory-key`, `kernel-memory-rotation-key`, and
   `google-search-key`. The two KernelMemory keys must differ. Use distinct
   values per environment. Create the Cosmos databases configured in the
   service appsettings before deployment; production does not call
   `EnsureCreated`.
3. Provide Azure Blob Storage for KernelMemory documents. The production
   template selects `AzureBlobs` for document and content storage, with its
   connection string from Key Vault. Test document retrieval after restarting
   the KernelMemory replica before accepting production traffic.
4. Register a single-tenant Microsoft Entra API for MainSubSys. Set the app
   manifest's `api.requestedAccessTokenVersion` to `2`, and expose the App ID
   URI `api://<application-client-id>`. Set `ENTRA_AUDIENCE` to the bare
   application client ID GUID, which is the `aud` claim in v2 access tokens.
   Define app roles `Chat.Access` and
   `Agent.Admin`. Assign the intended users or client applications to the
   appropriate roles. Assign `Agent.Admin` to each deployment identity so the
   authenticated smoke test can call `/admin/agents`. MainSubSys validates
   bearer tokens and roles in production; local Development remains anonymous.
   This API does not provide a browser sign-in page. Clients obtain an Entra
   access token for `api://<application-client-id>` and send it as a bearer token.
   Chat sessions are bound to the token's `tid` and `oid` claims. Sessions
   created before this ownership check cannot be resumed and must be recreated.
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
| `ENTRA_TENANT_ID`, `ENTRA_AUDIENCE` | MainSubSys tenant GUID and bare API client ID GUID |
| `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT` | Chat provider settings |
| `AZURE_EMBEDDING_DEPLOYMENT`, `AZURE_TEXT_DEPLOYMENT` | KernelMemory provider settings |
| `GOOGLE_SEARCH_ENGINE_ID` | MCP search engine identifier |

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

Only MainSubSys has external ingress. Orchestrator and MCP are reachable only
inside the Container Apps environment; their current internal API filters are
disabled. KernelMemory keeps its API-key filter enabled and MainSubSys sends
the key. Restrict network access to the Container Apps environment and review
service-to-service authentication before adding more callers.
The MCP page fetcher accepts only public HTTP(S) destinations on standard ports,
checks each redirect, and limits a page response to 1 MiB. Outbound DNS and
network policy should also deny access to private and metadata endpoints.

The readiness endpoints currently indicate that the ASP.NET process has
started. They do not query Azure OpenAI, Cosmos DB, Blob Storage, or Google.
The staging smoke test exercises a real gateway-to-Orchestrator request. Add
dependency-specific diagnostics if those services become operationally critical.
Existing persisted agents with localhost MCP endpoints must be updated to the
internal `http://myaia-<stage>-mcpserver` address before those tools can run in ACA.
Validate document upload size and request duration against ACA ingress limits in
staging before enabling large uploads.
