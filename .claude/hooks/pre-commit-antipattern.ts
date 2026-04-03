#!/usr/bin/env npx tsx
/**
 * Pre-commit hook: detect anti-patterns in staged C# files for MyAIAgent.
 *
 * Exit codes:
 *   0 — No issues (or no .cs files staged)
 *   1 — Issues found, commit blocked
 */

import { execSync } from "node:child_process";
import { readFileSync, existsSync } from "node:fs";

const stagedFiles = execSync("git diff --cached --name-only --diff-filter=ACM")
  .toString()
  .split("\n")
  .filter((f) => f.endsWith(".cs") && existsSync(f));

if (stagedFiles.length === 0) {
  process.exit(0);
}

console.log("Checking staged C# files for anti-patterns...");

const checks: Array<{ pattern: RegExp; message: string; skip?: RegExp }> = [
  {
    pattern: /DateTime\.(Now|UtcNow)/,
    message: "Use TimeProvider instead of DateTime.Now/UtcNow",
  },
  {
    pattern: /new HttpClient\(\)/,
    message: "Use IApiClient<T> instead of new HttpClient()",
  },
  {
    pattern: /async void/,
    message: "async void is dangerous — use async Task instead",
    skip: /EventArgs/,
  },
  {
    pattern: /\.Result\b|\.GetAwaiter\(\)\.GetResult\(\)/,
    message: "Avoid sync-over-async (.Result / .GetAwaiter().GetResult())",
  },
  {
    pattern: /Log(?:Information|Warning|Error|Debug|Critical).*\$"/,
    message: "Use structured logging parameters instead of string interpolation",
  },
  {
    pattern: /IMediator/,
    message: "Inject ISender instead of IMediator (prefer the narrow interface)",
  },
];

let errors = 0;

for (const file of stagedFiles) {
  const lines = readFileSync(file, "utf8").split("\n");

  for (const { pattern, message, skip } of checks) {
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      if (pattern.test(line) && (!skip || !skip.test(line))) {
        console.log(`  ${file}:${i + 1}: ${message}`);
        errors++;
      }
    }
  }
}

if (errors > 0) {
  console.log(`\nFound ${errors} anti-pattern issue(s) in staged files.`);
  console.log("Fix the issues above before committing.");
  process.exit(1);
}

console.log("No anti-patterns detected in staged files.");
process.exit(0);
