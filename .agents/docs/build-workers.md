# Build workers

Use `build-worker` only during Build when the plan has independent work with
disjoint file ownership. Run at most two workers at once in the shared worktree.
The main agent remains responsible for integration, quality evidence and delivery.

The assignment must name the goal, exact owned paths, acceptance criteria and
targeted tests. Tell each worker that other agents share the worktree, to preserve
unrelated edits, and to report any required change outside its ownership before
making it. The main agent owns shared files and does not edit worker-owned paths
until the worker has finished. If safe ownership cannot be drawn, work sequentially.

Workers may edit their assigned implementation and test files and run focused
checks. They must inspect their own diff and report changed paths, test commands,
results and unresolved risks. They must not spawn agents, stage or commit, push,
publish, run the full quality gate, or start Verify reviewers. A worker failure or
missing report leaves the assignment unfinished; the main agent resolves it before
running the full gate. Complete all Build work before starting Verify.
