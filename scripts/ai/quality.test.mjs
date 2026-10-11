import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { parseOptions, runQuality, services, testProject } from './quality.mjs';
import { fixture, successfulRun, trx } from './test-fixtures.mjs';

const quiet = { output() {} };
test('full gate binds candidate and all four suites, runs task format using safe argv', t => {
  const root = fixture(t);
  const calls = [];
  const report = runQuality({ root, ...quiet, run(command, args, options) {
    calls.push({ command, args, options });
    return successfulRun(command, args);
  } });
  assert.equal(report.status, 'PASS');
  assert.equal(report.scope, 'full');
  assert.deepEqual(calls.map(c => c.args[0]), ['--test', 'restore', 'build', 'test', 'test', 'test', 'test', 'format']);
  assert.equal(report.tests.total, 12);
  assert.equal(report.tests.passed, 12);
  assert.deepEqual(report.tests.suites.map(s => s.service), services);
  assert.equal(report.candidate.digest.length, 64);
  assert.deepEqual(calls.at(-1).args.slice(-2), ['--include', 'example.cs']);
  for (const call of calls) {
    assert.equal(call.options.cwd, root);
    assert.equal(call.options.shell, false);
  }
  assert.equal(calls[1].args[1], 'src/MyAIAgent.slnx');
  assert.ok(calls[2].args.includes('--no-restore'));
  assert.ok(calls[3].args.includes('--no-build'));
  assert.ok(calls.at(-1).args.includes('--verify-no-changes'));
  assert.deepEqual(JSON.parse(readFileSync(join(report.directory, 'quality.json'))), report);
});

for (const failureAt of [0, 1, 2, 3, 4, 5, 6, 7]) {
  test(`gate ${failureAt + 1} failure stops subsequent gates and persists failure`, t => {
    const root = fixture(t);
    let calls = 0;
    const report = runQuality({ root, ...quiet, run(command, args) {
      return calls++ === failureAt ? { status: 7 } : successfulRun(command, args);
    } });
    assert.equal(calls, failureAt + 1);
    assert.equal(report.status, 'FAIL');
    assert.equal(report.steps.at(-1).exitCode, 7);
    assert.equal(JSON.parse(readFileSync(join(report.directory, 'quality.json'))).status, 'FAIL');
  });
}
for (const result of [{ status: null, error: new Error('node not found') }, { status: null, signal: 'SIGTERM' }]) {
  test(`missing or interrupted tool cannot pass: ${result.signal ?? 'ENOENT'}`, t => {
    const report = runQuality({ root: fixture(t), ...quiet, run: () => result });
    assert.equal(report.status, 'FAIL');
    assert.equal(report.steps.length, 1);
  });
}
test('missing TRX cannot pass even when dotnet exits zero', t => {
  const report = runQuality({ root: fixture(t), ...quiet, run: () => ({ status: 0 }) });
  assert.equal(report.status, 'FAIL');
  assert.match(report.error, /Expected one fresh TRX/);
});
test('targeted run uses owning test project and is explicitly partial', t => {
  const report = runQuality({ root: fixture(t), project: 'Orchestrator', ...quiet, run: successfulRun });
  assert.equal(report.status, 'PASS');
  assert.equal(report.scope, 'targeted');
  assert.equal(report.steps[0].command[2], testProject('Orchestrator'));
  assert.equal(report.tests.suites.length, 1);
});
test('configuration-only or deleted C# candidate never falls back to full format', t => {
  const root = fixture(t);
  unlinkSync(join(root, 'example.cs'));
  const report = runQuality({ root, ...quiet, run: successfulRun });
  assert.equal(report.status, 'PASS');
  assert.equal(report.steps.at(-1).status, 'NOT_APPLICABLE');
  assert.deepEqual(report.format.include, []);
});
test('edits during checks invalidate evidence', t => {
  const root = fixture(t);
  const report = runQuality({ root, ...quiet, run(command, args) {
    writeFileSync(join(root, 'new.txt'), 'concurrent edit');
    return successfulRun(command, args);
  } });
  assert.equal(report.status, 'FAIL');
  assert.match(report.error, /Candidate changed/);
});
test('invalid, duplicate or missing CLI arguments fail closed', () => {
  for (const args of [['--project', 'Unknown'], ['--project'], ['--filter', 'x'], ['--base', 'HEAD', '--base', 'x']]) {
    assert.throws(() => parseOptions(args));
  }
  assert.deepEqual(parseOptions(['--project', 'McpServer', '--base', 'HEAD~1']), { project: 'McpServer', base: 'HEAD~1' });
});

test('failed test exit preserves TRX failure counts and stops the gate', t => {
  const root = fixture(t);
  const report = runQuality({ root, ...quiet, run(command, args) {
    const result = successfulRun(command, args);
    if (command === 'dotnet' && args[0] === 'test') {
      const directory = args[args.indexOf('--results-directory') + 1];
      writeFileSync(join(directory, 'results.trx'), trx({ passed: 2, failed: 1 }).replace('outcome="Completed"', 'outcome="Failed"'));
      return { status: 1 };
    }
    return result;
  } });
  assert.equal(report.status, 'FAIL');
  assert.equal(report.tests.failed, 1);
  assert.equal(report.tests.passed, 2);
  assert.equal(report.steps.at(-1).name, 'test:MainSubSys');
});
