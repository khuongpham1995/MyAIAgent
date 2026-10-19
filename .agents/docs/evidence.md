# Candidate and verification evidence

Run these commands from the repository root. Artifacts stay in ignored
`artifacts/ai/<run>/`; do not add them to Git or paste secrets into reports.

## Scope and quality

```bash
node scripts/ai/quality.mjs
node scripts/ai/quality.mjs --project Orchestrator
node scripts/ai/quality.mjs --base <merge-base-commit>
```

The default comparison base is HEAD. For branch/PR review, first resolve the
merge-base with the actual target branch and supply that commit through `--base`.
The manifest records HEAD, resolved base and every changed tracked/untracked file,
including deletions, executable modes and symlink targets. A SHA-256 digest covers
this content manifest; it is stable when the same content is staged. It is a digest
of the final candidate, not a hash of a display-dependent Git patch.

All non-ignored changes belong to the candidate. If unrelated work exists, use an
isolated checkout/worktree before starting; never silently include, stash or discard
it. Partial-hunk commits are not supported by this gate. Git conflicts, submodules
and assume-unchanged/skip-worktree flags are rejected rather than hidden. Ignored
local settings, tools and external services are outside the manifest; reviewers
must disclose relevant environmental limits.

Full quality runs workflow tests, restores/builds the solution, tests each of the
four runnable service projects, and verifies added/modified candidate C# files.
It records total/passed/failed/skipped counts from fresh VSTest TRX counters per
suite. Skips remain visible and need review (the gate prints a WARNING and records it in `warnings`; Cosmos repository tests skip when `COSMOS_TEST_CONNECTION_STRING` is unset, so report them as skipped, never as passed); zero tests, missing reports or failures
cannot pass. No changed C# files means format is NOT_APPLICABLE. Whole-solution
format drift is a separate baseline, not hidden by a claim of full format success.
Changes to formatting policy may justify a broader explicit format check.

`--project` accepts Gateway, Orchestrator, KernelMemory or McpServer. Formatting is deferred to the full gate. Its report
is marked `targeted`; it cannot satisfy the final evidence check. After iteration,
run the full gate. Quality checks candidate freshness again before reporting PASS.

## Parallel review

Check the full report before launching both read-only reviewers:

```bash
node scripts/ai/candidate.mjs check artifacts/ai/<run>/quality.json
```

The command prints `candidateDigest` and `qualitySha256`. Pass the same report and
both identifiers to the two concurrent reviewers, alongside the task/plan. Each
returns its narrative and one JSON record in its response:

```json
{
  "reviewer": "code-reviewer",
  "verdict": "PASS",
  "candidateDigest": "<actual candidate digest>",
  "qualitySha256": "<actual quality report SHA-256>",
  "summary": "What was reviewed and the outcome.",
  "changes": ["Checks inspected and findings, including path/line when relevant."],
  "risks": ["Untested behavior and residual limitations."]
}
```

The other reviewer uses `security-reviewer`. Allowed verdicts are PASS, NEEDS FIXES
and BLOCKED. The owner saves the actual returned records as `code-review.json` and
`security-review.json` beside the quality report. Do not manufacture a PASS from
missing or blocked review. Reviewers never write these files themselves.

```bash
node scripts/ai/candidate.mjs check artifacts/ai/<run>/quality.json artifacts/ai/<run>/code-review.json artifacts/ai/<run>/security-review.json
```

Both distinct PASS reports must match the current candidate and exact quality
report bytes. Any source/config/test change requires fresh quality and both reviews.
Do not edit while reviewers run. Concurrency and honest reviewer provenance remain
the owner's responsibility; these local JSON records are not signed attestations.

## Commit check

Only after explicit user authorization, stage the complete reviewed candidate
using explicit paths. Preserve unrelated work. Immediately before committing:

```bash
node scripts/ai/candidate.mjs check artifacts/ai/<run>/quality.json artifacts/ai/<run>/code-review.json artifacts/ai/<run>/security-review.json --staged
```

This additionally requires a nonempty staged diff, no unstaged/untracked changes,
and a clean `git diff --cached --check`. Changed HEAD/base/content, missing reviews,
partial quality runs and stale evidence return BLOCKED with nonzero exit status.
No files or Git state are mutated by this checker. There is no bypass flag.

These are explicit workflow commands, not Git hooks or a defense against someone
editing the scripts/artifacts or invoking Git directly. Native permissions govern
tool execution; actual commit/PR authorization follows [workflow.md](workflow.md).
