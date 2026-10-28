/**
 * MyAIAgent | Developer Tooling · AI Workflow · Tests
 *
 * Checks gate ordering, failure propagation, candidate changes, and full-versus-targeted evidence using fake tools.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { parseOptions, runQuality, services, testProject } from './quality.ts';
import { fixture, successfulRun, trx } from './test-fixtures.ts';
import type { SpawnSyncOptions } from 'node:child_process';
import type { ToolResult } from './types.ts';

const quiet = { output() {} };
test('full gate binds candidate and all five suites, runs task format using safe argv', t => {
  // Arrange
  const root = fixture(t);
  const calls: { command: string; args: string[]; options: SpawnSyncOptions }[] = [];

  // Act
  const report = runQuality({ root, ...quiet, run(command, args, options) {
    calls.push({ command, args, options });
    return successfulRun(command, args);
  } });

  // Assert
  assert.equal(report.status, 'PASS');
  assert.equal(report.scope, 'full');
  assert.deepEqual(calls.map(c => c.args[0]), ['run', '--test', 'restore', 'build', 'test', 'test', 'test', 'test', 'test', 'format']);
  assert.ok(calls[1].args.includes('scripts/security/configure-github.test.ts'));
  assert.ok(calls[1].args.includes('scripts/security/scan-secrets.test.ts'));
  assert.ok(calls[1].args.includes('scripts/deploy/aca.test.ts'));
  assert.equal(report.tests.total, 15);
  assert.equal(report.tests.passed, 15);
  assert.deepEqual(report.tests.suites.map(s => s.service), services);
  assert.equal(report.candidate.digest.length, 64);
  assert.deepEqual(calls.at(-1)!.args.slice(-2), ['--include', 'example.cs']);
  for (const call of calls) {
    assert.equal(call.options.cwd, root);
    assert.equal(call.options.shell, false);
  }
  assert.equal(calls[2].args[1], 'src/MyAIAgent.slnx');
  assert.ok(calls[3].args.includes('--no-restore'));
  assert.ok(calls[4].args.includes('--no-build'));
  assert.ok(calls.at(-1)!.args.includes('--verify-no-changes'));
  assert.deepEqual(JSON.parse(readFileSync(join(report.directory, 'quality.json'), 'utf8')), report);
});

for (const failureAt of [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]) {
  test(`gate ${failureAt + 1} failure stops subsequent gates and persists failure`, t => {
    // Arrange
    const root = fixture(t);
    let calls = 0;

    // Act
    const report = runQuality({ root, ...quiet, run(command, args) {
      return calls++ === failureAt ? { status: 7 } : successfulRun(command, args);
    } });

    // Assert
    assert.equal(calls, failureAt + 1);
    assert.equal(report.status, 'FAIL');
    assert.equal(report.steps.at(-1)!.exitCode, 7);
    assert.equal(JSON.parse(readFileSync(join(report.directory, 'quality.json'), 'utf8')).status, 'FAIL');
  });
}
for (const result of [{ status: null, error: new Error('node not found') }, { status: null, signal: 'SIGTERM' }] satisfies ToolResult[]) {
  test(`missing or interrupted tool cannot pass: ${result.signal ?? 'ENOENT'}`, t => {
    // Arrange & Act
    const report = runQuality({ root: fixture(t), ...quiet, run: () => result });

    // Assert
    assert.equal(report.status, 'FAIL');
    assert.equal(report.steps.length, 1);
  });
}
test('missing TRX cannot pass even when dotnet exits zero', t => {
  // Arrange & Act
  const report = runQuality({ root: fixture(t), ...quiet, run: () => ({ status: 0 }) });

  // Assert
  assert.equal(report.status, 'FAIL');
  assert.match(report.error!, /Expected one fresh TRX/);
});
test('skipped tests are surfaced as a warning and never counted as passed', t => {
  // Arrange
  const lines: string[] = [];

  // Act
  const report = runQuality({ root: fixture(t), output: line => lines.push(line), run(command, args) {
    const result = successfulRun(command, args);
    if (command === 'dotnet' && args[0] === 'test' && args[1].includes('Orchestrator')) {
      const directory = args[args.indexOf('--results-directory') + 1];
      writeFileSync(join(directory, 'results.trx'), trx({ total: 5, passed: 3, notExecuted: 2 }));
    }
    return result;
  } });

  // Assert
  assert.equal(report.status, 'PASS');
  assert.equal(report.tests.skipped, 2);
  assert.equal(report.warnings.length, 1);
  assert.match(report.warnings[0], /2 test\(s\) were skipped, not passed \(Orchestrator: 2\)/);
  assert.match(report.warnings[0], /COSMOS_TEST_ENDPOINT/);
  assert.ok(lines.some(line => line.includes('WARNING')));
});
test('a run without skipped tests has no warnings', t => {
  // Arrange & Act
  const report = runQuality({ root: fixture(t), ...quiet, run: successfulRun });

  // Assert
  assert.deepEqual(report.warnings, []);
});
test('targeted run uses owning test project and is explicitly partial', t => {
  // Arrange & Act
  const report = runQuality({ root: fixture(t), project: 'Orchestrator', ...quiet, run: successfulRun });

  // Assert
  assert.equal(report.status, 'PASS');
  assert.equal(report.scope, 'targeted');
  assert.equal(report.steps[0].command![2], testProject('Orchestrator'));
  assert.equal(report.tests.suites.length, 1);
});
test('console client suite is included in full and targeted quality evidence', t => {
  // Arrange
  const root = fixture(t);

  // Act
  const report = runQuality({ root, project: 'ConsoleClient', ...quiet, run: successfulRun });

  // Assert
  assert.equal(report.status, 'PASS');
  assert.equal(report.tests.suites[0].service, 'ConsoleClient');
  assert.equal(report.steps[0].command![2], 'tests/Clients/MyAIAgent.ConsoleClient.Tests/MyAIAgent.ConsoleClient.Tests.csproj');
  assert.deepEqual(parseOptions(['--project', 'ConsoleClient']), { project: 'ConsoleClient' });
});
test('configuration-only or deleted C# candidate never falls back to full format', t => {
  // Arrange
  const root = fixture(t);
  unlinkSync(join(root, 'example.cs'));

  // Act
  const report = runQuality({ root, ...quiet, run: successfulRun });

  // Assert
  assert.equal(report.status, 'PASS');
  assert.equal(report.steps.at(-1)!.status, 'NOT_APPLICABLE');
  assert.deepEqual(report.format!.include, []);
});
test('edits during checks invalidate evidence', t => {
  // Arrange
  const root = fixture(t);

  // Act
  const report = runQuality({ root, ...quiet, run(command, args) {
    writeFileSync(join(root, 'new.txt'), 'concurrent edit');
    return successfulRun(command, args);
  } });

  // Assert
  assert.equal(report.status, 'FAIL');
  assert.match(report.error!, /Candidate changed/);
});
test('invalid, duplicate or missing CLI arguments fail closed', () => {
  // Arrange, Act & Assert
  for (const args of [['--project', 'Unknown'], ['--project'], ['--filter', 'x'], ['--base', 'HEAD', '--base', 'x']]) {
    assert.throws(() => parseOptions(args));
  }
  assert.deepEqual(parseOptions(['--project', 'McpServer', '--base', 'HEAD~1']), { project: 'McpServer', base: 'HEAD~1' });
});

test('failed test exit preserves TRX failure counts and stops the gate', t => {
  // Arrange
  const root = fixture(t);

  // Act
  const report = runQuality({ root, ...quiet, run(command, args) {
    const result = successfulRun(command, args);
    if (command === 'dotnet' && args[0] === 'test') {
      const directory = args[args.indexOf('--results-directory') + 1];
      writeFileSync(join(directory, 'results.trx'), trx({ passed: 2, failed: 1 }).replace('outcome="Completed"', 'outcome="Failed"'));
      return { status: 1 };
    }
    return result;
  } });

  // Assert
  assert.equal(report.status, 'FAIL');
  assert.equal(report.tests.failed, 1);
  assert.equal(report.tests.passed, 2);
  assert.equal(report.steps.at(-1)!.name, 'test:Gateway');
});
