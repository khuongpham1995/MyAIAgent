/**
 * MyAIAgent | Developer Tooling · AI Workflow · Tests
 *
 * Exercises evidence freshness, file manifests, staging completeness, and refusal of mismatched review records.
 */

import test, { type TestContext } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, readFileSync, renameSync, symlinkSync, unlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { assertCurrent, captureCandidate, checkEvidence, git, hash } from './candidate.ts';
import { runQuality } from './quality.ts';
import { commit, fixture, successfulRun } from './test-fixtures.ts';

function evidence(t: TestContext) {
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
  // Arrange
  const root = fixture(t);
  const name = 'space $x;\nfile.txt';
  writeFileSync(join(root, name), 'new');
  symlinkSync('example.cs', join(root, 'link'));

  // Act
  const first = captureCandidate(root);

  // Assert
  assert.equal(first.files.find(f => f.path === 'link')!.mode, '120000');

  // Arrange
  writeFileSync(join(root, name), 'changed');

  // Act & Assert
  assert.throws(() => assertCurrent(first, root), /changed/);

  // Arrange
  const second = captureCandidate(root);
  chmodSync(join(root, 'example.cs'), 0o755);

  // Act & Assert
  assert.notEqual(captureCandidate(root).digest, second.digest);

  // Arrange
  unlinkSync(join(root, 'example.cs'));

  // Act & Assert
  assert.equal(captureCandidate(root).files.find(f => f.path === 'example.cs')!.mode, 'deleted');
});
test('staging same final content preserves digest; stale index cannot pass commit gate', t => {
  // Arrange
  const { root, report, reportPath, reviews } = evidence(t);

  // Act & Assert
  assert.throws(() => checkEvidence(reportPath, { root, reviews, staged: true }), /unstaged/);

  // Arrange
  git(root, ['add', '--', 'example.cs']);

  // Act & Assert
  assert.equal(captureCandidate(root).digest, report.candidate.digest);
  assert.equal(checkEvidence(reportPath, { root, reviews, staged: true }).status, 'PASS');

  // Arrange
  writeFileSync(join(root, 'example.cs'), 'other');
  git(root, ['add', '--', 'example.cs']);
  writeFileSync(join(root, 'example.cs'), 'candidate\n');

  // Act & Assert
  assert.throws(() => checkEvidence(reportPath, { root, reviews, staged: true }), /unstaged/);
});
test('new untracked files or HEAD movement invalidate evidence', t => {
  // Arrange
  const { root, reportPath } = evidence(t);
  writeFileSync(join(root, 'extra.txt'), 'unreviewed');

  // Act & Assert
  assert.throws(() => checkEvidence(reportPath, { root }), /changed/);

  // Arrange
  unlinkSync(join(root, 'extra.txt'));
  git(root, ['add', '--', 'example.cs']);
  commit(root);

  // Act & Assert
  assert.throws(() => checkEvidence(reportPath, { root }), /changed/);
});
test('explicit base captures committed branch changes and renames', t => {
  // Arrange
  const root = fixture(t);
  const base = git(root, ['rev-parse', 'HEAD']).trim();
  renameSync(join(root, 'example.cs'), join(root, 'renamed.cs'));
  git(root, ['add', '--', 'example.cs', 'renamed.cs']);
  commit(root);

  // Act & Assert
  assert.equal(captureCandidate(root).files.length, 0);

  // Act
  const candidate = captureCandidate(root, base);

  // Assert
  assert.equal(candidate.base, base);
  assert.deepEqual(candidate.files.map(f => f.path), ['example.cs', 'renamed.cs']);
});
test('missing, duplicate, stale, failed reviews and changed quality reports are blocked', t => {
  // Arrange
  const { root, reportPath, reviews } = evidence(t);

  // Act & Assert
  assert.equal(checkEvidence(reportPath, { root, reviews }).status, 'PASS');
  assert.throws(() => checkEvidence(reportPath, { root, reviews: [reviews[0]] }), /Both/);
  assert.throws(() => checkEvidence(reportPath, { root, reviews: [reviews[0], reviews[0]] }), /review/);
  const original = readFileSync(reviews[1], 'utf8');
  for (const update of [{ verdict: 'BLOCKED' }, { candidateDigest: 'old' }, { qualitySha256: 'old' }]) {
    // Arrange
    writeFileSync(reviews[1], JSON.stringify({ ...JSON.parse(original), ...update }));

    // Act & Assert
    assert.throws(() => checkEvidence(reportPath, { root, reviews }), /review/);
  }

  // Arrange
  writeFileSync(reviews[1], original);
  writeFileSync(reportPath, readFileSync(reportPath, 'utf8') + '\n');

  // Act & Assert
  assert.throws(() => checkEvidence(reportPath, { root, reviews }), /review/);
});
test('partial, failed, interrupted or zero-test evidence is not final evidence', t => {
  // Arrange
  const { root, reportPath, report } = evidence(t);

  for (const update of [{ scope: 'targeted' }, { status: 'FAIL' }, { status: 'RUNNING' }, { tests: { total: 0, failed: 0 } }]) {
    // Arrange
    writeFileSync(reportPath, JSON.stringify({ ...report, ...update }));

    // Act & Assert
    assert.throws(() => checkEvidence(reportPath, { root }));
  }
});
test('replacing tracked directory with symlink records child deletions without following it', t => {
  // Arrange
  const root = fixture(t);
  mkdirSync(join(root, 'nested'));
  writeFileSync(join(root, 'nested', 'file'), 'tracked');
  git(root, ['add', '--', 'nested/file']);
  commit(root);
  renameSync(join(root, 'nested'), join(root, 'moved'));
  symlinkSync('moved', join(root, 'nested'));

  // Act
  const candidate = captureCandidate(root);

  // Assert
  assert.equal(candidate.files.find(f => f.path === 'nested/file')!.mode, 'deleted');
  assert.equal(candidate.files.find(f => f.path === 'nested')!.mode, '120000');

  // Arrange
  git(root, ['add', '--', 'nested', 'moved']);

  // Act & Assert
  assert.equal(captureCandidate(root).digest, candidate.digest);
});

for (const flag of ['--assume-unchanged', '--skip-worktree']) {
  test(`hidden tracked changes are rejected: ${flag}`, t => {
    // Arrange
    const root = fixture(t);
    git(root, ['update-index', flag, '--', 'example.cs']);

    // Act & Assert
    assert.throws(() => captureCandidate(root), /flags/);
  });
}

for (const kind of ['file', 'symlink']) {
  test(`replacing a tracked ${kind} with a directory captures children and survives staging`, t => {
    // Arrange
    const root = fixture(t);
    if (kind === 'symlink') symlinkSync('example.cs', join(root, 'adapter'));
    else writeFileSync(join(root, 'adapter'), 'old');
    git(root, ['add', '--', 'adapter']);
    commit(root);
    unlinkSync(join(root, 'adapter'));
    mkdirSync(join(root, 'adapter'));
    writeFileSync(join(root, 'adapter', 'child'), 'new');

    // Act
    const candidate = captureCandidate(root);

    // Assert
    assert.equal(candidate.files.find(f => f.path === 'adapter')!.mode, 'deleted');
    assert.equal(candidate.files.find(f => f.path === 'adapter/child')!.sha256, hash('new'));

    // Arrange
    git(root, ['add', '--', 'adapter']);

    // Act & Assert
    assert.equal(captureCandidate(root).digest, candidate.digest);

    // Arrange
    writeFileSync(join(root, 'adapter', 'child'), 'unreviewed');

    // Act & Assert
    assert.throws(() => assertCurrent(candidate, root), /changed/);
  });
}

test('a staged gitlink cannot masquerade as a deleted baseline file', t => {
  // Arrange
  const root = fixture(t);
  const head = git(root, ['rev-parse', 'HEAD']).trim();
  unlinkSync(join(root, 'example.cs'));
  mkdirSync(join(root, 'example.cs'));
  git(root, ['update-index', '--add', '--cacheinfo', `160000,${head},example.cs`]);

  // Act & Assert
  assert.throws(() => captureCandidate(root), /submodules/);
});
