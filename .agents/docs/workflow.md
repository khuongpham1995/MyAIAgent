# Delivery workflow

This is the shared policy for Codex and Claude. Provider adapters select models
and tools; they do not duplicate this policy. See [setup.md](setup.md) for native
controls. Instructions cannot themselves change a host's native mode.

For an optional source-based security audit, use the separate [ai-audit workflow](audit.md). It tracks coverage and independently verified findings; it does not replace any delivery stage below.

| Stage | Claude | Codex | Exit condition |
| --- | --- | --- | --- |
| Plan | Opus, native Plan mode | Astra, native Plan mode | Concrete scope, files, acceptance criteria, tests and risks |
| Build | Sonnet | Sol | Implementation and relevant tests; quality command passes |
| Verify | Two Sonnet reviewers in parallel, then the Opus lead | Two Sol reviewers in parallel, then the Astra lead | Both reports PASS, lead ALIGNED, blocking findings resolved |
| Commit | Sonnet | Sol | Task-owned commit; optional PR; Summary / Changes / Risks |

## Questions and approvals

Ask only for a consequential missing decision, an unresolved preflight requirement,
or a delivery choice required by this workflow. A stage transition alone does not
require approval; preserve the user's existing task authorization.

Use the host's interactive Question UI when an appropriate tool is available and
permitted in the active mode. Use `request_user_input` for clarifications where
permitted; use `request_user_input_async` or the native approval mechanism for
authorization decisions when the synchronous tool cannot request permission.
Use plain-text questions only when no permitted interactive tool is available,
and explain that limitation briefly. Do not replace a Question with repeated
instructions asking the user to operate native controls manually.

Keep required questions pending until an explicit answer arrives. A preselected
option, timeout, or missing answer is not consent. Reuse valid answers within their
task/session scope; do not ask the same question again without materially changed
requirements or evidence. Continue independent permitted work while waiting.

Question approval authorizes only the stated action. It does not change native mode,
select a model, bypass sandbox restrictions, or replace quality/review gates. Follow
[native preflight](setup.md#native-modemodel-preflight) for state verification and
reuse of model confirmations. If a tool action is denied, report the actual reason
and use the host's supported approval flow rather than inventing an override.

## Plan

1. Read `AGENTS.md`, `.agents/docs/project.md`, affected source and existing tests.
   Inspect branch, status, staged and unstaged changes; record pre-existing user work.
2. Map services/layers and external contracts. Identify concrete acceptance criteria
   and regression, negative and integration tests needed.
3. Return **Summary**, **Changes** (ordered steps with real paths and test cases),
   and **Risks** (compatibility, auth/data boundaries, assumptions and questions).
4. Do not edit source or run mutating agents in Plan mode. Hand the plan to Build
   through the conversation, or a user-selected handoff file when switching sessions.
   A read-only sandbox alone does not enable native Plan mode. Plan runs in the
   main session. Before presenting the plan it consults the read-only `tech-lead`
   (optional for configuration, docs and one-line fixes) following [team.md](team.md)
   **Dispatch**, and resolves each requested change with evidence.
   Name the Build agent that owns each slice of the plan.

Ask only about consequential ambiguity. Respect the host's native Plan-to-Build
transition; a plan-only request ends here. Existing authorization to implement
persists after that transition; do not add another approval ritual.

## Build

1. Confirm the concrete plan and acceptance criteria are available. If a fresh
   session lacks the plan, obtain that context before guessing what to implement.
2. Use Sonnet/Sol. Make focused changes in dependency order and preserve unrelated work.
   The main session owns integration, build fixes and the full quality gate. When the
   plan contains independent work with disjoint file ownership, it may dispatch up
   to two Build subagents concurrently in the shared worktree: the service engineer
   whose persona owns those paths, `build-worker` for Common, Aspire, clients,
   tooling and cross-cutting work, or `test-engineer` for tests ([team.md](team.md)).
   Give each worker its exact files, goal, acceptance criteria and targeted tests.
   Keep overlapping or shared files with the main session; do not edit worker-owned
   files while they run.
   When the plan assigns `test-engineer`, run it before the SDE for that behavior.
   Follow [build-workers.md](build-workers.md).
   For behavior changes, reproduce the bug or write a meaningful failing test first
   when practical, then implement and refactor. Do not add tests that merely repeat code.
3. Wait for every worker and inspect its diff and test results before integrating.
   The main session resolves conflicts and failures. Do not run Build workers
   alongside Verify reviewers. Run `npm run quality -- --project <Service>` while
   iterating, then `npm run quality` for the full gate (including workflow tests).
   For a branch review, pass `--base <merge-base-commit>` to include committed task
   changes; resolve it against the intended target branch, not an assumed `main`.
   See [evidence.md](evidence.md) for scope, counts and candidate checks.
4. Fix failures within scope before review. Distinguish pre-existing failures from
   regressions with evidence. Do not weaken assertions, exclude failing tests,
   disable format checks or report an unavailable check as passed.

The quality command is an explicit deterministic check, not a Git hook or security
audit. It does not enforce review or commit authorization. Failed gates remain
blocked unless the user explicitly accepts a documented exception for this change.
No project hooks are installed; do not add mutating per-edit hooks or hook
dependencies installed over the network.

## Verify

1. Freeze the candidate after Build. Record HEAD, comparison base for a branch review,
   task file list and quality results from `quality.json`. Run the quality-only candidate check in
   [evidence.md](evidence.md) before dispatch. Include staged, unstaged and untracked task
   files, not only `git diff --cached`. For a branch review also inspect the
   merge-base-to-HEAD diff. Do not assume the base branch is named `main`.
2. Launch **code-reviewer** and **security-reviewer** concurrently, using Sonnet/Sol,
   with the same `quality.json` path, candidate digest, quality SHA-256, scope,
   acceptance criteria and test evidence. Send facts, not conclusions: follow
   **Blind review inputs** in `.agents/docs/review.md`. Both read
   `.agents/docs/review.md`. Neither may edit, stage, format, build, restore, commit or
   dispatch mutating agents. They may inspect code, diffs and existing logs.
3. Wait for both reports. Missing, timed-out or failed review is **BLOCKED**, not
   approval. If parallel agents are unavailable, disclose that limitation; never
   present sequential self-review as the requested parallel verification.
4. Preserve both returned review JSON records beside `quality.json`; reviewers
   themselves remain read-only. Run the candidate check with both records.
   Consolidate findings. Fix critical/high findings before commit; document lower
   severity risks. Reject false positives with evidence.
5. Result review: once both reviewers return PASS on the final candidate, launch
   `tech-lead` alone with the blind inputs, both review records as paths, and the
   owner's draft Summary / Changes / Risks as a path it opens last. It cannot clear or
   downgrade a reviewer finding. Save its record as `lead-review.json` beside
   `quality.json`. REVISE about the candidate means a fix and a full rerun. REVISE about
   only the owner report is fixed without rerunning quality or the reviewers; relaunch
   `tech-lead` on the same candidate for a fresh record. Never edit a returned verdict.
6. After code/config/test changes, rerun affected tests and quality checks, then
   both reviews and the result review on the new candidate. Never change files while
   reviewers run. User edits during review invalidate affected evidence; obtain a fresh review.

## Commit and optional PR

Only a direct user invocation of Commit or an explicit user request to commit
authorizes a local commit of the verified task. Selecting **Commit + push** (or
explicitly asking to push) additionally requests pushing the task branch;
**Commit + push + PR** (or an explicit PR request) also requests creating the PR.
Automatic skill selection or an agent invoking the stage does not grant authorization. Check the user's actual
request before any Git mutation; preserve authorization already given in the session.
Configuring this workflow alone does not request committing or publishing it.

1. Confirm quality evidence, both review verdicts and the ALIGNED lead record still
   describe the exact candidate, using `candidate.ts check` with all three files. Stop on unresolved
   blockers; do not label it ready. This check is required by the adapters; it is
   not a Git hook and cannot prevent a user from running Git directly.
   Once ready, if the user has not already selected the delivery option, show one
   question: **How would you like to finish this change?**

   - **Commit only** — create the verified local commit; no push.
   - **Commit + push** — create the verified commit and push the task branch; no PR.
   - **Commit + push + PR** — create the verified commit, push the task branch and
     open a draft PR (or update its existing PR).

   Use the host's interactive question UI when available and permitted in the
   active mode; otherwise show the same question and choices in plain text.
   Wait for an explicit answer before staging or committing. A default selection,
   timeout or missing answer is not consent. Ask once per task; an explicit
   delivery choice in the user's request, or an earlier choice already supplies the answer.
   This question selects the delivery scope; it does not bypass quality gates,
   destination checks or native execution permissions.
2. Inspect status, staged/unstaged diffs and untracked task files. Stage explicit
   paths/hunks only; never `git add .` or `git commit -a`. If unrelated changes are
   staged, preserve the index and isolate task paths or ask a focused question.
   Never silently include, unstage or discard user work.
3. Review the staged diff and run the candidate check again with `--staged`
   (includes `git diff --cached --check`). It requires the complete candidate in the
   index with no remaining unstaged or untracked files. Confirm no secrets,
   artifacts or unrelated changes. Use a conventional title and English body:

   ```text
   chore(ai): add a consistent delivery workflow

   Summary
   Explain the problem and resulting behavior.

   Changes
   - Describe the final implementation.
   - Validation: exact checks, results, both reviewer verdicts and the lead verdict.

   Risks
   - Residual risk, missing checks and rollback considerations, or None identified.
   ```

4. Commit without amending, force-pushing or skipping hooks. Report hash and
   remaining worktree state. A failed check requires the explicit exception above,
   recorded in Risks; never silently bypass it. The candidate checker has no
   bypass flag: an explicitly accepted exception remains a documented manual
   exception, never a generated PASS.
5. For **Commit + push** and **Commit + push + PR**,
   inspect remotes, branch, upstream, intended base and existing PRs.
   Use a task branch; never push the default/protected branch. If destination is
   ambiguous, ask before publishing. Push only that branch, without force. For
   **Commit + push** stop after the push and report the branch; do not create a PR.
   For **Commit + push + PR**, create
   a draft PR by default, or update the existing task PR instead of duplicating it.
   Use `.github/pull_request_template.md`; write the body to a temporary file and
   use `gh pr create --draft --base <base> --head <branch> --body-file <file>`.
   Report the URL. If push/PR fails, retain the commit and report the exact blocker.

Every stage ends with **Summary / Changes / Risks**. Include command results,
test counts, findings and unrun checks. A passing build or clean review does not
prove that the application is secure.
