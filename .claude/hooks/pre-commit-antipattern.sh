#!/usr/bin/env bash
# Pre-commit hook: detect anti-patterns in staged C# files for MyAIAgent.
#
# Exit codes:
#   0 — No issues (or no .cs files staged)
#   1 — Issues found, commit blocked

set -euo pipefail

STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)

if [[ -z "$STAGED_FILES" ]]; then
    exit 0
fi

echo "Checking staged C# files for anti-patterns..."

ERRORS=0

for FILE in $STAGED_FILES; do
    if [[ ! -f "$FILE" ]]; then
        continue
    fi

    # DateTime.Now / DateTime.UtcNow — use TimeProvider
    if grep -n 'DateTime\.\(Now\|UtcNow\)' "$FILE" 2>/dev/null; then
        echo "  $FILE: Use TimeProvider instead of DateTime.Now/UtcNow"
        ERRORS=$((ERRORS + 1))
    fi

    # new HttpClient() — use IApiClient<T> via IHttpClientFactory
    if grep -n 'new HttpClient()' "$FILE" 2>/dev/null; then
        echo "  $FILE: Use IApiClient<T> instead of new HttpClient()"
        ERRORS=$((ERRORS + 1))
    fi

    # async void — except event handlers
    if grep -n 'async void' "$FILE" | grep -v 'EventArgs' 2>/dev/null; then
        echo "  $FILE: async void is dangerous — use async Task instead"
        ERRORS=$((ERRORS + 1))
    fi

    # Sync-over-async — .Result / .GetAwaiter().GetResult()
    if grep -n '\.Result\b\|\.GetAwaiter()\.GetResult()' "$FILE" 2>/dev/null; then
        echo "  $FILE: Avoid sync-over-async (.Result / .GetAwaiter().GetResult())"
        ERRORS=$((ERRORS + 1))
    fi

    # String interpolation in log messages
    if grep -n 'Log\(Information\|Warning\|Error\|Debug\|Critical\).*\$"' "$FILE" 2>/dev/null; then
        echo "  $FILE: Use structured logging parameters instead of string interpolation"
        ERRORS=$((ERRORS + 1))
    fi

    # Direct IMediator injection (prefer ISender)
    if grep -n 'IMediator' "$FILE" 2>/dev/null; then
        echo "  $FILE: Inject ISender instead of IMediator (prefer the narrow interface)"
        ERRORS=$((ERRORS + 1))
    fi
done

if [[ $ERRORS -gt 0 ]]; then
    echo ""
    echo "Found $ERRORS anti-pattern issue(s) in staged files."
    echo "Fix the issues above before committing."
    exit 1
fi

echo "No anti-patterns detected in staged files."
exit 0
