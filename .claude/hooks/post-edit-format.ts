#!/usr/bin/env npx tsx
/**
 * PostToolUse hook: auto-format a C# file after Claude edits or writes it.
 * Runs dotnet format on the specific file to keep formatting clean without
 * requiring a full solution format pass.
 *
 * Environment variable FILE_PATH is set by Claude Code to the edited file.
 */

import { execSync } from "node:child_process";

const filePath = process.env.FILE_PATH ?? "";

// Only act on C# source files
if (!filePath || !filePath.endsWith(".cs")) {
  process.exit(0);
}

// Skip generated files and obj/bin directories
if (filePath.includes("/obj/") || filePath.includes("/bin/")) {
  process.exit(0);
}

try {
  execSync(
    `dotnet format MyAIAgent.sln --include "${filePath}" --verbosity quiet`,
    { stdio: "ignore" }
  );
} catch {
  // Non-zero exit from dotnet format is non-fatal
}

process.exit(0);
