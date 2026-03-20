#!/usr/bin/env bash
# PostToolUse hook: auto-format a C# file after Claude edits or writes it.
# Runs dotnet format on the specific file to keep formatting clean without
# requiring a full solution format pass.
#
# Environment variable FILE_PATH is set by Claude Code to the edited file.

set -euo pipefail

# Only act on C# source files
if [[ -z "${FILE_PATH:-}" ]] || [[ "$FILE_PATH" != *.cs ]]; then
    exit 0
fi

# Skip generated files and obj/bin directories
if [[ "$FILE_PATH" == *"/obj/"* ]] || [[ "$FILE_PATH" == *"/bin/"* ]]; then
    exit 0
fi

# Run dotnet format on the specific file (quiet — don't noise up Claude's output)
if command -v dotnet &>/dev/null; then
    dotnet format MyAIAgent.sln --include "$FILE_PATH" --verbosity quiet 2>/dev/null || true
fi

exit 0
