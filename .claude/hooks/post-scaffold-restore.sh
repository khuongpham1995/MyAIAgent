#!/usr/bin/env bash
# PostToolUse hook: run dotnet restore when a .csproj file is modified.
# Keeps the solution in a buildable state after package or project reference changes.
#
# Environment variable FILE_PATH is set by Claude Code to the edited file.

set -euo pipefail

if [[ -z "${FILE_PATH:-}" ]] || [[ "$FILE_PATH" != *.csproj ]]; then
    exit 0
fi

echo "Detected .csproj change — running dotnet restore..."

if command -v dotnet &>/dev/null; then
    dotnet restore MyAIAgent.sln --verbosity quiet 2>/dev/null || true
fi

exit 0
