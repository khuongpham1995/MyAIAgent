# Configuration security

Protect credentials and service access in this public repository. Treat Git history, logs, and published images as public. See [local setup](local-development.md) and [request flows](architecture.md).

## Configuration boundaries

| Data | Store and access |
| --- | --- |
| Nonsecret settings | AAC, one store per environment. Labels/prefixes select values; they do not isolate access. |
| Deployed secrets | Key Vault; Container Apps resolves secret references. Never duplicate plaintext secrets in AAC. |
| Local secrets | Per-project user-secrets, outside Git but unencrypted. No production credentials. |
| AAC/Cosmos credentials | Entra identity: Azure CLI in Development; explicitly assigned managed identity elsewhere. No key/connection-string fallback. |

AAC is optional locally and required elsewhere. Set `AppConfiguration:Endpoint`, `KeyPrefix`, and `Label`. Prefix removal maps `Orchestrator:AiProvider:Type` to `AiProvider:Type`.

Precedence: appsettings → AAC → user-secrets → environment → command line. Deployed Key Vault references and identity/bootstrap settings override AAC. Keep tracked `appsettings*.json` safe to publish.

## Identity and resource access

Each service has a separate user-assigned managed identity. `infra/access.bicep` grants:

| Service | Secret access | Cosmos access |
| --- | --- | --- |
| Gateway | Active Orchestrator/KernelMemory/MCP keys; optional public JWT signing secret | None |
| Orchestrator | Azure OpenAI key, its active/rotation service keys, and the active MCP key | ChatSessions and AgentDefinitions in its environment database |
| KernelMemory | Azure OpenAI, Search and Blob credentials; its active/rotation service keys | DocumentRecords in its environment database |
| McpServer | Google Search key and its active/rotation service keys | None |

All four identities can pull registry images and read their environment's AAC. Key Vault grants are secret-scoped. Privileged bootstrap owns role assignments; routine CD does not.

Cosmos account keys are disabled. Provision with `infra/cosmos.bicep`; runtime/tests never create containers. Scope developer/test grants to `myaiagent-test`, with no production access. Database-name guards complement Azure RBAC.

Conversation writes require current Cosmos ETags. Conflicting metadata saves fall back; conflicting turn saves fail and need a fresh request. Retire old writers without ETag checks during rollout.

KernelMemory's Search/Blob providers still use keys. The shared Search admin key reaches both indexes. Stronger environment isolation needs separate Search services or a provider migration to scoped identity access.

### MCP service access

| Rule | Behavior |
| --- | --- |
| Server authorization | Enabled by default; required outside Development. Configure active and rotation keys. |
| Protocol routes | Authenticate before dispatch on `/` and `/mcp`; health stays anonymous. |
| Header/status | `Authorization`; missing key → 401, invalid key → 403 |
| Callers | Gateway and Orchestrator require `McpServer:AccessKey` outside Development. |
| Credential scope | Exact `McpServer:BaseUrl` origin, paths `/`, `/mcp`, `/mcp/`; no URL credentials, query, or fragment |
| Transport | HTTPS in production, optional loopback HTTP in Development; no redirects/cookies |

Deploy callers against the internal HTTPS FQDN. External agent endpoints never receive the first-party key. Discovery authentication failures become safe chat errors, before model use.

Shared keys do not identify callers or grant per-tool permissions. Restrict agent administration and treat approved tool descriptions/results as untrusted.

## Outbound destination policy

Agent endpoints must use the configured first-party MCP service or `McpServer:AllowedExternalOrigins` (empty by default).

| Destination | Allowed |
| --- | --- |
| Allowlist entry | HTTPS scheme, host, effective port; nonstandard ports supported. No credentials, wildcard, non-root path, query, or fragment. |
| Approved external endpoint | Paths/protocol queries allowed; no user information/fragments. Must resolve only to public addresses. |
| First-party endpoint | Configured origin and protocol routes only. RFC1918/IPv6 unique-local allowed; Development loopback only when that origin is loopback. |
| Page fetching | Public HTTP/HTTPS, default ports, validated redirects, no HTTPS downgrade, five redirects, 1 MiB limit. Policy denials are not retried; transient retries remain bounded. |

First-party and external traffic use separate factory-managed clients; external is anonymous, including legacy SSE message requests. Every SDK request is checked. Proxies, redirects, cookies, and default credentials are disabled. HTTP/1.1 routes connections through the checked TCP callback.

For each connection, DNS resolves once. Any prohibited answer rejects the whole result; otherwise the socket connects directly to validated addresses. TLS verifies the original hostname. Empty/failed DNS answers fail closed.

Public connections reject IANA special-purpose blocks (including globally reachable special assignments), IPv6 outside `2000::/3`, scoped IPv6, and Azure `168.63.129.16`. IPv4-mapped IPv6 is checked as IPv4. Maintain the range table as assignments change.

Link-local/metadata addresses remain blocked even for first-party, including `fd00:ec2::254` (AWS) and `fd20:ce::254` (Google). There is no general Development bypass.

Validation runs at startup, agent creation (without network calls), and before stored endpoints are used. Restart after configuration changes; existing agents are not rewritten. Discovery denial stops chat before model use/persistence. Logs contain agent IDs and bounded reasons, not URLs or addresses; cancellation propagates.

Add network egress controls where needed. An approved origin is not proof of trusted content.

## Repository controls

Install Node.js 20.6+ tools with `npm ci --ignore-scripts`. Quality checks TypeScript before automation tests. Secret scanning needs `curl`/`tar` on macOS/Linux; protection configuration needs `gh`.

`npm run security:scan` downloads Gitleaks 8.30.1 with a pinned SHA-256 check. It scans available history/nonignored files, skips worktree symlinks, and writes redacted `artifacts/security/` reports. Use a complete clone for full history coverage.

Built-in rules ignore inline suppressions. Only three exact fingerprints from `5e41834bde7cde94dbcd9ca8327108dd800f53a6` are exempt: verified JWT fixtures in PublicChatSettingsTests, PublicChatEndpointsTests, and PublicChatTestApp, since replaced by generated keys. No file/future-commit exemption exists. Scanning cannot prove all leaks absent.

CI scans without Azure identity. External Actions are SHA-pinned; Dependabot tracks packages/Actions. CODEOWNERS identifies review areas but does not enforce approval.

Inspect administrative protections with:

```bash
npm run security:configure -- '<owner>/<repository>'
```

Add `--apply` to enable secret scanning, push protection, vulnerability alerts/fixes, and private reporting. It creates missing default-branch protection and restricts deployments to that branch, preserving existing protections/reviewers. Inspect existing settings separately.

Solo-maintainer policy: PR, passing `quality`/`secrets`, resolved conversations, linear history, no force push/deletion, administrators included. Additional approvals: zero. Production needs owner approval with self-review allowed. Add reviewers and disable self-review when another maintainer joins. Never remove required checks to merge an older PR; update its workflow/evidence first.

## Rotation and incident response

**Committed credential:** revoke/rotate immediately, then review service logs and Azure access records. Deleting files or ignoring them does not remove history exposure. History rewriting is separate and never replaces revocation.

**Orchestrator/KernelMemory:** set a fresh inactive key → deploy callers with it → verify authenticated requests → replace the old slot. Keep slots distinct. Use controlled revision rollouts; Key Vault references/HTTP clients do not guarantee immediate refresh.

**MCP** (callers read `mcp-key` only):

1. Set the new value in `mcp-rotation-key`; restart MCP and verify it works.
2. Set `mcp-key` to that value; restart and verify both callers. Slots may temporarily match.
3. Set a distinct standby rotation key; restart MCP.

Do not retire the old key before the first server rollout succeeds. Verify each rollout; live refresh is not promised.

Foundation configures Key Vault purge protection/audit logs; the AAC template configures diagnostics. Restrict log access/retention. Never log configuration dumps, Authorization headers, request bodies, or secrets. Review network/private-endpoint designs and costs separately.

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
