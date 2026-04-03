#!/usr/bin/env npx tsx
/**
 * PostToolUse hook: run dotnet restore when a .csproj file is modified.
 * Keeps the solution in a buildable state after package or project reference changes.
 *
 * Environment variable FILE_PATH is set by Claude Code to the edited file.
 */

import { execSync } from "node:child_process";

const filePath = process.env.FILE_PATH ?? "";

if (!filePath || !filePath.endsWith(".csproj")) {
  process.exit(0);
}

console.log("Detected .csproj change — running dotnet restore...");

try {
  execSync("dotnet restore MyAIAgent.sln --verbosity quiet", {
    stdio: "ignore",
  });
} catch {
  // Non-zero exit is non-fatal
}

process.exit(0);
