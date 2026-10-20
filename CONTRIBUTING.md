# Contributing to MyAIAgent

Extend a service and verify the change. Start with the [repository map](docs/repository-guide.md) and [request flows](docs/architecture.md).

## Work locally

Use .NET 10 and Node.js 20.6+; run `npm ci --ignore-scripts`. Follow [local setup](docs/local-development.md) for Aspire/providers. The default quality gate needs no Azure credentials.

For a feature, follow the existing service boundaries:

1. Add domain state only when the feature needs it.
2. Define application contracts and dependency ports in the owning service.
3. Place each MediatR command/query, handler, and validator in its feature folder.
4. Implement external I/O in Infrastructure, using typed HTTP clients where applicable.
5. Keep Presentation endpoints thin and map inputs through the application-owned mappings.
6. Add tests for the behavior, failure cases, cancellation, and ownership boundaries that actually change.

Services communicate over HTTP, not sibling project references. Put code in Common only when shared. Current source takes precedence over reference examples.

## Checks

Run from the repository root:

```bash
# Fast iteration for an affected service
npm run quality -- --project Gateway

# Required full gate before review
npm run quality
```

| Check | Coverage |
| --- | --- |
| Full quality | TypeScript/automation, solution build, four .NET suites, changed C# format verification. Targeted runs do not replace it. |
| Live Cosmos | `COSMOS_TEST_ENDPOINT`, Azure CLI identity, preprovisioned `myaiagent-test` containers. Skips when unset; never grant production access for tests. |
| CI infrastructure checks | Bicep/workflow validation using pinned `.github/actions/` tools; run for deployment/config changes. |
| `npm run security:scan` | Available Git history and nonignored files; see [security](docs/security.md) for download/network requirements. |

Quality reports/TRX stay in ignored `artifacts/ai/`.

## Documentation and comments

- Write in English. Keep guides concise: short introductions, task-focused steps, and tables for settings; link to details instead of repeating them.
- Give maintained source files a short purpose header describing their specific role. Test headers explain what behavior they cover; preserve Arrange / Act / Assert comments.
- For C#, TypeScript, and Bicep, use a `/** ... */` block with a `MyAIAgent | <component>` signature, a blank line, and the file's purpose. The signature identifies the project and area, not an author or license.
- Use native comment syntax and preserve shebangs, parser directives, XML declarations, and Markdown frontmatter.
- Document JSON, generated lockfiles, and symlinks in the [repository guide](docs/repository-guide.md). Do not insert comments into strict JSON or edit generated files for prose.
- Update request-flow diagrams and examples when behavior changes. Distinguish implemented features from intended integrations.
- Use placeholders in examples. Never copy private deployment endpoints, account identifiers, credentials, or private operational evidence into public documentation.

Example file header:

```csharp
/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards chat requests to the Orchestrator and maps its response.
 */
```

## Review and delivery

Follow [AGENTS.md](AGENTS.md) and [Plan → Build/test → Verify → Commit](.agents/docs/workflow.md). Both read-only reviewers run concurrently on the tested candidate. Commit/push/PR require the maintainer's selected scope.

Optional assistant controls: [Codex/Claude setup](.agents/docs/setup.md). Report vulnerabilities privately through [SECURITY.md](SECURITY.md); never post secrets or exploit details in public issues.
