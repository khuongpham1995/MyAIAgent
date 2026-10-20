# Configuration security

See [local development](local-development.md) for per-service setup and [architecture](architecture.md) for the actual request paths.

The source repository is public. Assume every committed file, previous commit, build log, and published image can be inspected by someone outside the project.

## Configuration boundaries

- Store nonsecret settings in Azure App Configuration (AAC). Use one store per environment for access isolation; labels and service prefixes select settings but do not create an authorization boundary.
- Store API keys, shared service keys, signing keys, and remaining provider connection strings in Key Vault. Container Apps resolves the existing Key Vault references. Do not duplicate plaintext secrets in AAC.
- AAC and Cosmos authenticate through Microsoft Entra ID. Applications use their explicitly assigned managed identity outside Development; Development uses `AzureCliCredential` after `az login`. No AAC or Cosmos connection-string fallback exists.
- AAC is required outside Development. Locally it is optional. Configure `AppConfiguration:Endpoint`, `AppConfiguration:KeyPrefix`, and `AppConfiguration:Label`. For example, `Orchestrator:AiProvider:Type` with label `staging` becomes `AiProvider:Type` in the Orchestrator.
- Configuration precedence is appsettings defaults, AAC, then developer secrets, environment variables, and command-line overrides. Deployed Key Vault references and identity/bootstrap values remain authoritative over AAC.
- Keep `appsettings*.json` safe to publish. Store local secrets with `dotnet user-secrets`; that storage is outside Git but is not encrypted. Never use production credentials for local development.

## Identity and resource access

Each service has a separate user-assigned managed identity. `infra/access.bicep` grants:

| Service | Secret access | Cosmos access |
| --- | --- | --- |
| Gateway | Active Orchestrator/KernelMemory/MCP keys; optional public JWT signing secret | None |
| Orchestrator | Azure OpenAI key, its active/rotation service keys, and the active MCP key | ChatSessions and AgentDefinitions in its environment database |
| KernelMemory | Azure OpenAI, Search and Blob credentials; its active/rotation service keys | DocumentRecords in its environment database |
| McpServer | Google Search key and its active/rotation service keys | None |

All four identities can pull images from the shared registry and read nonsecret AAC settings for their environment. Key Vault grants are secret-scoped; there is no shared vault-wide runtime reader. Role assignments belong to privileged bootstrap, not routine CD.

Cosmos account-key authentication is disabled. Provision databases and containers through `infra/cosmos.bicep`; runtime startup and repository tests never create them. Test/developer identities must be scoped to `myaiagent-test`. A test credential must not have a production grant. The application database-name guard complements Azure RBAC.

Conversation writes require the current Cosmos ETag. An overlapping request cannot overwrite a newer turn with a stale session snapshot. A conflicting optional title or summary save is discarded through the best-effort fallback; a conflicting turn save still fails and must be retried from a fresh request. These checks require all writers to use the updated application; old revisions without ETag checks must be retired during rollout.

Azure AI Search and Blob provider credentials still use keys because the pinned KernelMemory provider configuration uses them. The shared Search admin key can reach both indexes; independent service identities do not remove that provider-level cross-environment risk. Use separate Search services or migrate that provider to scoped identity-based access when stronger isolation is required.

MCP authorization is enabled by default and cannot be disabled outside Development. Both active and rotation keys must be configured. Authentication runs before MCP protocol dispatch on the root and /mcp paths; health checks remain anonymous. Missing credentials return 401; invalid credentials return 403. The header is `Authorization`.

Gateway and Orchestrator require `McpServer:AccessKey` outside Development. The shared HTTP handler adds it only to the exact trusted `McpServer:BaseUrl` origin and the root or /mcp protocol paths (including /mcp/), with no credentials, query, or fragment in the URL. Redirects and cookies are disabled. Production origins require HTTPS; Development also permits loopback HTTP. Deployment uses the internal HTTPS FQDN, not a redirect from the short HTTP name. An agent-controlled external MCP endpoint does not receive this key. HTTP authentication failures propagate to safe chat errors instead of an apparently successful turn without tools.

This is service authentication, not per-caller identity or per-tool authorization. Keep agent administration restricted; an approved MCP server still supplies untrusted tool descriptions and results.

## Outbound destination policy

Orchestrator permits agent endpoints on the configured first-party MCP service or origins explicitly listed in `McpServer:AllowedExternalOrigins`. The external list defaults to empty. Each entry is an HTTPS origin (scheme, host, and effective port), with no credentials, wildcard, path beyond `/`, query, or fragment. Explicit nonstandard HTTPS ports are supported. Paths and protocol queries on approved external origins are permitted; endpoint fragments and URL user information are rejected. Configuration is validated at startup and captured until restart.

First-party and external MCP traffic use separate factory-managed clients. The first-party client permits only the configured origin and protocol paths. The external client is anonymous and cannot use the first-party client or its key, including when a legacy SSE server advertises another message URL. Every SDK-generated request must satisfy its client's destination policy. Proxies, automatic redirects, cookies, and default credentials are not used. HTTP/1.1 is selected explicitly so every connection passes through the checked TCP connection callback.

The shared connection policy resolves each new connection once, rejects the entire DNS answer if any address is prohibited, and connects directly to the validated addresses. TLS still validates the original hostname. Public connections exclude IANA special-purpose address blocks (conservatively including globally reachable special-purpose assignments), IPv6 addresses outside `2000::/3`, scoped IPv6, and Azure's `168.63.129.16` platform address. IPv4-mapped IPv6 addresses are classified as IPv4. Failed or empty DNS answers do not bypass validation. This range table requires maintenance as address assignments change.

Only the configured first-party service may use RFC1918 or IPv6 unique-local destinations. Development also permits loopback when the configured first-party origin is itself loopback. There is no general Development bypass: external MCP and page-fetch destinations must remain public. Link-local, metadata, and other special-purpose addresses remain prohibited even for the first-party service, including the unique-local metadata addresses `fd00:ec2::254` (AWS) and `fd20:ce::254` (Google Cloud).

Agent creation checks endpoint permissions without DNS or HTTP requests. Existing stored endpoints are checked again before use; unapproved agents are not automatically rewritten. A destination-policy denial during discovery produces a safe chat failure before model invocation or turn persistence. Denial logs use agent identifiers and bounded reasons rather than URLs, queries, or resolved addresses. Cancellation remains propagated.

Page fetching shares the address classifier and connection checks while retaining public HTTP/HTTPS on default ports, validated redirects, no HTTPS downgrade, five redirects, and a 1 MiB response limit. Policy denials are not retried. Transient failures retain the bounded retry policy. Network-level egress controls remain complementary protection; an application allowlist does not establish the trustworthiness of an approved server.

## Repository controls

Automation scripts use strict TypeScript on Node.js 20.6+, with Bicep for infrastructure. After checkout, run `npm ci --ignore-scripts` to install the locked development tools. The full quality gate runs `npm run typecheck` before automation tests. The secret scanner also uses `curl` and `tar` on macOS or Linux; GitHub protection configuration uses the GitHub CLI (`gh`).

Run `npm run security:scan`. It downloads Gitleaks 8.30.1 from the official release, verifies a pinned SHA-256 checksum, scans all locally available Git history and nonignored worktree files, and writes redacted reports under ignored `artifacts/security/`. It does not follow worktree symlinks. Use a complete clone for a complete history scan.

The scanner uses built-in rules, ignores inline suppression comments, and permits only three exact historical fingerprints from commit `5e41834bde7cde94dbcd9ca8327108dd800f53a6`: repeated hexadecimal JWT test strings in PublicChatSettingsTests, PublicChatEndpointsTests, and PublicChatTestApp. They were verified as test-only fixtures and replaced with runtime-generated test keys. No file-wide or future-commit exemption is granted. Detection is not proof that every possible credential leak is absent.

GitHub CI scans history and checked-out files with no Azure identity. External Actions are pinned to commit SHAs. Dependabot tracks NuGet and Action updates. CODEOWNERS identifies configuration and delivery paths for maintainer attention; it does not itself require approval.

Inspect administrative protections with:

```bash
npm run security:configure -- '<owner>/<repository>'
```

The explicit `--apply` option enables secret scanning, push protection, vulnerability alerts, automated security fixes, and private vulnerability reporting. It creates default-branch protection when absent and restricts staging/production deployments to the default branch. It preserves existing branch protection and existing environment reviewers rather than weakening them. Verify existing protections separately.

The initial solo-maintainer policy requires a PR, current successful `quality` and `secrets` checks, resolved conversations, and linear history; it blocks force pushes/deletions and applies to administrators. It requires zero additional approving reviews so the owner can merge their own checked PR. Production requires owner approval and allows self-review. Add trusted reviewers and disable self-review when a second maintainer is available. Both checks are required immediately, including during rollout. Older PRs remain blocked until they include the updated CI workflow and publish a passing `secrets` check; do not temporarily remove that requirement to merge the first hardening PR.

## Rotation and incident response

If a credential was committed, revoke or rotate it immediately. Removing the current file or adding a Git ignore rule does not remove exposure through existing clones or history. Review affected service logs and Azure access records. History rewriting is a separate coordinated operation and does not replace revocation.

For Orchestrator/KernelMemory keys, put a fresh value in the inactive slot, deploy consumers with that value, confirm authenticated requests, then replace the old slot. Maintain distinct values in the two slots. Container Apps Key Vault references and already-created HTTP clients do not guarantee immediate refresh; use a controlled revision rollout and verify both sides before retiring a key.

For MCP, callers read only `mcp-key`; the server reads that secret plus `mcp-rotation-key`. To rotate without requiring callers to read the rotation slot: put the new value into `mcp-rotation-key`, roll out/restart MCP and verify that value works, then replace `mcp-key` with the new value and restart both callers. The server accepts either slot during propagation (the slots may temporarily match). Once both callers are verified, replace the rotation slot with a distinct fresh standby value and restart MCP. Do not retire the old value until the first server rollout succeeds. Configuration snapshots and Key Vault refresh timing require explicit verification; live key refresh is not promised.

Key Vault purge protection and audit diagnostics are configured in foundation infrastructure. AAC diagnostics are configured in its provisioning template. Restrict log readers and retention, and do not log configuration dumps, Authorization headers, request bodies, or secret values. Network restrictions/private endpoints can add protection, but require a separately reviewed network design and cost estimate.

## References

- [IANA IPv4 special-purpose addresses](https://www.iana.org/assignments/iana-ipv4-special-registry/)
- [IANA IPv6 special-purpose addresses](https://www.iana.org/assignments/iana-ipv6-special-registry/)
- [Azure platform address 168.63.129.16](https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16)
- [AWS instance metadata endpoints](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-instance-metadata-service.html)
- [Google Cloud metadata endpoints](https://cloud.google.com/compute/docs/metadata/querying-metadata)
- [Secure Azure App Configuration](https://learn.microsoft.com/azure/azure-app-configuration/secure-azure-app-configuration)
- [Cosmos identity-based access](https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-connect-role-based-access-control)
- [Secure Key Vault](https://learn.microsoft.com/azure/key-vault/general/secure-key-vault)
- [GitHub Actions security](https://docs.github.com/en/actions/reference/security/secure-use)
