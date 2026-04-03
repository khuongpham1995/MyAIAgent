#!/usr/bin/env npx tsx
/**
 * PostToolUse hook: remind Claude to invoke the code-reviewer agent after
 * modifying a production C# file.
 *
 * Skips test projects (.Test/) to avoid noise during TDD red-green cycles.
 * Skips generated files (obj/ bin/).
 *
 * Exit codes:
 *   0 — No-op (non-C# file, test file, or generated output)
 *   0 — Message written to stdout (Claude Code surfaces it as hook feedback)
 *
 * Environment variable FILE_PATH is set by Claude Code to the edited file.
 */

const filePath = process.env.FILE_PATH ?? "";

// Only act on C# source files
if (!filePath || !filePath.endsWith(".cs")) {
  process.exit(0);
}

// Skip build output and generated files
if (filePath.includes("/obj/") || filePath.includes("/bin/")) {
  process.exit(0);
}

// Skip test projects — avoid interrupting TDD red-green cycles
if (filePath.includes(".Test/")) {
  process.exit(0);
}

console.log(
  `[auto-review] Production file changed: ${filePath}\n` +
    `When you finish editing, invoke the code-reviewer agent to review this change against project rules.`
);

process.exit(0);
