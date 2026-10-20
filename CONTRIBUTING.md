# Contributing to MyAIAgent

This guide explains how to explore the code, extend an existing service, and verify a change before delivery. Start with the [repository guide](docs/repository-guide.md) and [architecture](docs/architecture.md).

## Work locally

Use .NET 10 and Node.js 20.6+. Install locked automation dependencies with `npm ci --ignore-scripts`. The [local setup guide](docs/local-development.md) covers Azure resources and Aspire; Azure credentials are not needed for the default automated quality gate.

For a feature, follow the existing service boundaries:

1. Add domain state only when the feature needs it.
2. Define application contracts and dependency ports in the owning service.
3. Place each MediatR command/query, handler, and validator in its feature folder.
4. Implement external I/O in Infrastructure, using typed HTTP clients where applicable.
5. Keep Presentation endpoints thin and map inputs through the application-owned mappings.
6. Add tests for the behavior, failure cases, cancellation, and ownership boundaries that actually change.

Service projects must not reference sibling service projects. Shared code belongs in Common only when it is genuinely shared. Inspect the current implementation before following an example in a reference document.

## Checks

Run from the repository root:

```bash
# Fast iteration for an affected service
npm run quality -- --project Gateway

# Required full gate before review
npm run quality
```

The full gate runs TypeScript checks and automation tests, restores/builds the solution, runs four .NET test projects, and checks formatting of changed C# files without rewriting them. Targeted results do not replace the full gate. Reports and TRX files are ignored under `artifacts/ai/`.

Live Cosmos tests use `COSMOS_TEST_ENDPOINT`, Azure CLI identity, and preprovisioned `myaiagent-test` containers. They are visibly skipped when unconfigured. Never grant production access to make a test pass. See [local setup](docs/local-development.md) and [security](docs/security.md).

CI additionally compiles Bicep and validates workflow syntax using pinned tools in `.github/actions/`. Changes to deployment/configuration should receive those checks too. `npm run security:scan` scans available Git history and current nonignored files; its tool download and network requirements are described in [security](docs/security.md).

## Documentation and comments

- Write code, comments, docs, and commit messages in English.
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

Follow [AGENTS.md](AGENTS.md) and the shared [Plan → Build/test → Verify → Commit workflow](.agents/docs/workflow.md). The two Verify reviewers run concurrently on the same tested candidate. A passing build alone does not replace those reviews. Commit, push, and PR creation each require the delivery scope selected by the maintainer.

For optional AI tooling, use the [Codex/Claude setup guide](.agents/docs/setup.md). It documents native model/mode selection, the four `ai-*` skills, and evidence checks. These are assistant workflow controls, not application runtime dependencies.

Report security concerns through [SECURITY.md](SECURITY.md), rather than posting credentials or exploit details in a public issue.
