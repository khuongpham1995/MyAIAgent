#!/usr/bin/env npx tsx
/**
 * PreToolUse hook: block dangerous shell commands in MyAIAgent.
 *
 * Blocks: force-push to main/master, hard reset, branch deletion, rm -rf on source directories.
 * Warns: git push (non-force).
 *
 * Exit codes:
 *   0 — Allow the command
 *   2 — Block the command (PreToolUse only)
 */

const command = process.env.CLAUDE_TOOL_INPUT_COMMAND ?? "";

if (!command) {
  process.exit(0);
}

// BLOCK: force-push to main/master
if (/git push.*(--force|-f).*(main|master)/.test(command)) {
  console.error("Blocked: force-push to main/master is not allowed.");
  process.exit(2);
}

// BLOCK: git reset --hard (destroys uncommitted work)
if (/git reset --hard/.test(command)) {
  console.error(
    "Blocked: git reset --hard can destroy uncommitted work. Use git stash or git restore instead."
  );
  process.exit(2);
}

// BLOCK: rm -rf on source directories
if (/rm -rf.*(MyAIAgent|McpServer|\.claude|src)/.test(command)) {
  console.error("Blocked: rm -rf on source directories is not allowed.");
  process.exit(2);
}

// BLOCK: git branch -D (force-delete a branch)
if (/git branch -D/.test(command)) {
  console.error(
    "Blocked: git branch -D force-deletes branches. Use git branch -d (safe delete) instead."
  );
  process.exit(2);
}

// WARN: git push (non-force)
if (/^git push/.test(command) && !/(--force|-f)/.test(command)) {
  console.error(
    "Note: You are about to push to a remote. Ensure /verify has passed and changes are intentional."
  );
  // exit 0 — allow but warn
}

process.exit(0);
