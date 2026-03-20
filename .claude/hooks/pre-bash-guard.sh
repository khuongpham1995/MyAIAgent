#!/usr/bin/env bash
# PreToolUse hook: block dangerous shell commands in MyAIAgent.
#
# Blocks: force-push to main, hard reset, branch deletion, rm -rf on source directories.
# Warns: git push (non-force), dropping databases.
#
# Exit codes:
#   0 — Allow the command
#   2 — Block the command (PreToolUse only)

set -euo pipefail

COMMAND="${CLAUDE_TOOL_INPUT_COMMAND:-}"

if [[ -z "$COMMAND" ]]; then
    exit 0
fi

# --- BLOCK: force-push to main/master ---
if echo "$COMMAND" | grep -qE 'git push.*(--force|-f).*(main|master)'; then
    echo "Blocked: force-push to main/master is not allowed." >&2
    exit 2
fi

# --- BLOCK: git reset --hard (destroys uncommitted work) ---
if echo "$COMMAND" | grep -qE 'git reset --hard'; then
    echo "Blocked: git reset --hard can destroy uncommitted work. Use git stash or git restore instead." >&2
    exit 2
fi

# --- BLOCK: rm -rf on source directories ---
if echo "$COMMAND" | grep -qE 'rm -rf.*(MyAIAgent|McpServer|\.claude|src)'; then
    echo "Blocked: rm -rf on source directories is not allowed." >&2
    exit 2
fi

# --- BLOCK: git branch -D (force-delete a branch) ---
if echo "$COMMAND" | grep -qE 'git branch -D'; then
    echo "Blocked: git branch -D force-deletes branches. Use git branch -d (safe delete) instead." >&2
    exit 2
fi

# --- WARN: git push (non-force) ---
if echo "$COMMAND" | grep -qE '^git push' && ! echo "$COMMAND" | grep -qE '(--force|-f)'; then
    echo "Note: You are about to push to a remote. Ensure /verify has passed and changes are intentional." >&2
    # exit 0 — allow but warn
fi

exit 0
