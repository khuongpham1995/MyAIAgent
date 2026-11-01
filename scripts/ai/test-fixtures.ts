/**
 * MyAIAgent | Developer Tooling · AI Workflow
 *
 * Builds isolated temporary Git fixtures and simulated tool/TRX results for automation tests.
 */

import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { git } from './candidate.ts';
import type { TestContext } from 'node:test';

export function fixture(t: TestContext): string {
  const root = mkdtempSync(join(tmpdir(), 'ai workflow '));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  git(root, ['init', '-q']);
  git(root, ['config', 'user.email', 'fixture@example.invalid']);
  git(root, ['config', 'user.name', 'Workflow Fixture']);
  writeFileSync(join(root, '.gitignore'), 'artifacts/\n');
  writeFileSync(join(root, 'example.cs'), 'original\n');
  git(root, ['add', '--', '.gitignore', 'example.cs']);
  commit(root);
  writeFileSync(join(root, 'example.cs'), 'candidate\n');
  return root;
}
export function commit(root: string): void {
  git(root, ['-c', 'core.hooksPath=/dev/null', '-c', 'commit.gpgsign=false', 'commit', '-qm', 'fixture']);
}
export function trx(counters: Record<string, number> = {}): string {
  const values = { total: 3, executed: 3, passed: 3, failed: 0, error: 0, timeout: 0, aborted: 0,
    inconclusive: 0, passedButRunAborted: 0, notRunnable: 0, notExecuted: 0, disconnected: 0,
    warning: 0, completed: 0, inProgress: 0, pending: 0, ...counters };
  return `<TestRun><ResultSummary outcome="Completed"><Counters ${Object.entries(values).map(([k, v]) => `${k}="${v}"`).join(' ')} /></ResultSummary></TestRun>`;
}
export function successfulRun(command: string, args: string[]): { status: number } {
  if (command === 'dotnet' && args[0] === 'test') {
    const directory = args[args.indexOf('--results-directory') + 1];
    mkdirSync(directory, { recursive: true });
    writeFileSync(join(directory, 'results.trx'), trx());
  }
  return { status: 0 };
}
