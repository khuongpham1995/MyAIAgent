import test, { type TestContext } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { copyWorktree, installScanner, runScan } from './scan-secrets.ts';
import type { ProcessRunner } from './process.ts';

function temporary(t: TestContext): string {
  const root = mkdtempSync(join(tmpdir(), 'myaiagent-scanner-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  return root;
}

test('scanner findings fail the scan while printed fields omit credential values', t => {
  const root = temporary(t);
  const report = join(root, 'report.json');
  const credential = ['sensitive', 'fixture', 'value'].join('-');
  const lines: string[] = [];
  const run: ProcessRunner = (binary, args, options) => {
    assert.equal(options.shell, false);
    assert.ok(args.includes('--redact=100'));
    assert.ok(args.includes('--ignore-gitleaks-allow'));
    assert.ok(args.includes('--log-opts=--all'));
    writeFileSync(report, JSON.stringify([{ RuleID: 'test-rule', File: 'example.txt', StartLine: 1,
      Commit: 'abc', Secret: credential, Match: credential }]));
    return { status: 1, stdout: credential, stderr: credential };
  };

  assert.equal(runScan('scanner', root, report, 'git', 'config', 'ignore', { run, output: line => lines.push(line) }), true);

  assert.equal(lines.join('\n').includes(credential), false);
  assert.deepEqual(JSON.parse(lines[1]), { RuleID: 'test-rule', File: 'example.txt', StartLine: 1, Commit: 'abc' });
});

test('missing and invalid reports cannot pass even when scanner exits zero', t => {
  const root = temporary(t);
  const report = join(root, 'report.json');
  const options = { run: () => ({ status: 0 }), output() {} };

  assert.throws(() => runScan('scanner', root, report, 'dir', 'config', 'ignore', options), /report is missing/);
  writeFileSync(report, 'invalid report content');
  assert.throws(() => runScan('scanner', root, report, 'dir', 'config', 'ignore', options), /report is invalid/);
  writeFileSync(report, '[]');
  assert.equal(runScan('scanner', root, report, 'dir', 'config', 'ignore', options), false);
  assert.equal(runScan('scanner', root, report, 'dir', 'config', 'ignore',
    { ...options, run: () => ({ status: 1 }) }), true);
});

test('bad checksums stop execution and delete the untrusted archive', t => {
  const root = temporary(t);
  const tools = join(root, 'artifacts/tools/gitleaks/8.30.1');
  mkdirSync(tools, { recursive: true });
  const archive = join(tools, 'linux_x64.tar.gz');
  writeFileSync(archive, 'not the official release');

  assert.throws(() => installScanner(root, { platform: 'linux', arch: 'x64',
    run() { assert.fail('No command may run before a cached archive is verified'); } }), /checksum mismatch/);

  assert.equal(existsSync(archive), false);
});

test('worktree snapshot includes filenames with spaces and skips symlinks and deleted files', t => {
  const root = temporary(t);
  const worktree = join(root, 'worktree');
  mkdirSync(worktree);
  writeFileSync(join(root, 'file with spaces.txt'), 'example');
  symlinkSync(join(root, 'file with spaces.txt'), join(root, 'link'));
  const outside = temporary(t);
  writeFileSync(join(outside, 'outside.txt'), 'outside checkout');
  symlinkSync(outside, join(root, 'linked-directory'));

  copyWorktree(root, worktree, (command, args, options) => {
    assert.equal(command, 'git');
    assert.ok(args.includes('-z'));
    assert.equal(options.cwd, root);
    return { status: 0, stdout: 'file with spaces.txt\0link\0deleted.txt\0linked-directory/outside.txt\0' };
  });

  assert.equal(readFileSync(join(worktree, 'file with spaces.txt'), 'utf8'), 'example');
  assert.equal(existsSync(join(worktree, 'link')), false);
  assert.equal(existsSync(join(worktree, 'deleted.txt')), false);
  assert.equal(existsSync(join(worktree, 'linked-directory/outside.txt')), false);
});
