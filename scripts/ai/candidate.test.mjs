import test from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, readFileSync, renameSync, symlinkSync, unlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { assertCurrent, captureCandidate, checkEvidence, git, hash } from './candidate.mjs';
import { runQuality } from './quality.mjs';
import { commit, fixture, successfulRun } from './test-fixtures.mjs';

function evidence(t) {
  const root = fixture(t);
  const report = runQuality({ root, run: successfulRun, output() {} });
  const reportPath = join(report.directory, 'quality.json');
  const reviews = ['code-reviewer', 'security-reviewer'].map(reviewer => {
    const path = join(report.directory, `${reviewer}.json`);
    writeFileSync(path, JSON.stringify({ reviewer, verdict: 'PASS', candidateDigest: report.candidate.digest,
      qualitySha256: hash(readFileSync(reportPath)), summary: 'Inspected fixture', changes: [], risks: [] }));
    return path;
  });
  return { root, report, reportPath, reviews };
}
test('manifest includes untracked content, deletions, modes, symlinks and unusual paths', t => {
  const root = fixture(t);
  const name = 'space $x;\nfile.txt';
  writeFileSync(join(root, name), 'new');
  symlinkSync('example.cs', join(root, 'link'));
  const first = captureCandidate(root);
  assert.equal(first.files.find(f => f.path === 'link').mode, '120000');
  writeFileSync(join(root, name), 'changed');
  assert.throws(() => assertCurrent(first, root), /changed/);
  const second = captureCandidate(root);
  chmodSync(join(root, 'example.cs'), 0o755);
  assert.notEqual(captureCandidate(root).digest, second.digest);
  unlinkSync(join(root, 'example.cs'));
  assert.equal(captureCandidate(root).files.find(f => f.path === 'example.cs').mode, 'deleted');
});
test('staging same final content preserves digest; stale index cannot pass commit gate', t => {
  const { root, report, reportPath, reviews } = evidence(t);
  assert.throws(() => checkEvidence(reportPath, { root, reviews, staged: true }), /unstaged/);
  git(root, ['add', '--', 'example.cs']);
  assert.equal(captureCandidate(root).digest, report.candidate.digest);
  assert.equal(checkEvidence(reportPath, { root, reviews, staged: true }).status, 'PASS');
  writeFileSync(join(root, 'example.cs'), 'other');
  git(root, ['add', '--', 'example.cs']);
  writeFileSync(join(root, 'example.cs'), 'candidate\n');
  assert.throws(() => checkEvidence(reportPath, { root, reviews, staged: true }), /unstaged/);
});
test('new untracked files or HEAD movement invalidate evidence', t => {
  const { root, reportPath } = evidence(t);
  writeFileSync(join(root, 'extra.txt'), 'unreviewed');
  assert.throws(() => checkEvidence(reportPath, { root }), /changed/);
  unlinkSync(join(root, 'extra.txt'));
  git(root, ['add', '--', 'example.cs']);
  commit(root);
  assert.throws(() => checkEvidence(reportPath, { root }), /changed/);
});
test('explicit base captures committed branch changes and renames', t => {
  const root = fixture(t);
  const base = git(root, ['rev-parse', 'HEAD']).trim();
  renameSync(join(root, 'example.cs'), join(root, 'renamed.cs'));
  git(root, ['add', '--', 'example.cs', 'renamed.cs']);
  commit(root);
  assert.equal(captureCandidate(root).files.length, 0);
  const candidate = captureCandidate(root, base);
  assert.equal(candidate.base, base);
  assert.deepEqual(candidate.files.map(f => f.path), ['example.cs', 'renamed.cs']);
});
test('missing, duplicate, stale, failed reviews and changed quality reports are blocked', t => {
  const { root, reportPath, reviews } = evidence(t);
  assert.equal(checkEvidence(reportPath, { root, reviews }).status, 'PASS');
  assert.throws(() => checkEvidence(reportPath, { root, reviews: [reviews[0]] }), /Both/);
  assert.throws(() => checkEvidence(reportPath, { root, reviews: [reviews[0], reviews[0]] }), /review/);
  const original = readFileSync(reviews[1], 'utf8');
  for (const update of [{ verdict: 'BLOCKED' }, { candidateDigest: 'old' }, { qualitySha256: 'old' }]) {
    writeFileSync(reviews[1], JSON.stringify({ ...JSON.parse(original), ...update }));
    assert.throws(() => checkEvidence(reportPath, { root, reviews }), /review/);
  }
  writeFileSync(reviews[1], original);
  writeFileSync(reportPath, readFileSync(reportPath, 'utf8') + '\n');
  assert.throws(() => checkEvidence(reportPath, { root, reviews }), /review/);
});
test('partial, failed, interrupted or zero-test evidence is not final evidence', t => {
  const { root, reportPath, report } = evidence(t);
  for (const update of [{ scope: 'targeted' }, { status: 'FAIL' }, { status: 'RUNNING' }, { tests: { total: 0, failed: 0 } }]) {
    writeFileSync(reportPath, JSON.stringify({ ...report, ...update }));
    assert.throws(() => checkEvidence(reportPath, { root }));
  }
});
test('replacing tracked directory with symlink records child deletions without following it', t => {
  const root = fixture(t);
  mkdirSync(join(root, 'nested'));
  writeFileSync(join(root, 'nested', 'file'), 'tracked');
  git(root, ['add', '--', 'nested/file']);
  commit(root);
  renameSync(join(root, 'nested'), join(root, 'moved'));
  symlinkSync('moved', join(root, 'nested'));
  const candidate = captureCandidate(root);
  assert.equal(candidate.files.find(f => f.path === 'nested/file').mode, 'deleted');
  assert.equal(candidate.files.find(f => f.path === 'nested').mode, '120000');
  git(root, ['add', '--', 'nested', 'moved']);
  assert.equal(captureCandidate(root).digest, candidate.digest);
});

for (const flag of ['--assume-unchanged', '--skip-worktree']) {
  test(`hidden tracked changes are rejected: ${flag}`, t => {
    const root = fixture(t);
    git(root, ['update-index', flag, '--', 'example.cs']);
    assert.throws(() => captureCandidate(root), /flags/);
  });
}
