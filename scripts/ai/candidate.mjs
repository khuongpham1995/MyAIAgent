#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, readlinkSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const hash = value => createHash('sha256').update(value).digest('hex');
export const readJson = path => JSON.parse(readFileSync(path, 'utf8'));

export function git(root, args) {
  const result = spawnSync('git', ['--no-optional-locks', ...args], {
    cwd: root, encoding: 'utf8', shell: false, maxBuffer: 32 * 1024 * 1024,
  });
  if (result.status !== 0 || result.error || result.signal) {
    throw new Error(`git ${args[0]} failed: ${result.error?.message ?? result.stderr.trim()}`);
  }
  return result.stdout;
}
const paths = output => output.split('\0').filter(Boolean);

function fileState(root, path) {
  // Never follow a replaced parent directory outside the repository.
  let parent = root;
  for (const segment of path.split('/').slice(0, -1)) {
    parent = resolve(parent, segment);
    const stat = lstatSync(parent, { throwIfNoEntry: false });
    if (stat && !stat.isDirectory()) return { path, mode: 'deleted', sha256: null };
  }
  const fullPath = resolve(root, path);
  const stat = lstatSync(fullPath, { throwIfNoEntry: false });
  if (!stat) return { path, mode: 'deleted', sha256: null };
  if (stat.isSymbolicLink()) return { path, mode: '120000', sha256: hash(readlinkSync(fullPath)) };
  if (!stat.isFile()) throw new Error(`Unsupported candidate entry (including submodules): ${path}`);
  return { path, mode: stat.mode & 0o111 ? '100755' : '100644', sha256: hash(readFileSync(fullPath)) };
}

export function captureCandidate(root = repoRoot, base = 'HEAD') {
  if (git(root, ['ls-files', '--unmerged', '-z'])) throw new Error('Resolve index conflicts before capturing a candidate.');
  const flags = paths(git(root, ['ls-files', '-v', '-z']));
  if (flags.some(entry => entry[0] === 'S' || entry[0] === entry[0].toLowerCase())) {
    throw new Error('Clear skip-worktree/assume-unchanged flags before capturing a candidate.');
  }
  const head = git(root, ['rev-parse', '--verify', 'HEAD^{commit}']).trim();
  const baseCommit = git(root, ['rev-parse', '--verify', '--end-of-options', `${base}^{commit}`]).trim();
  const changed = [...new Set([
    ...paths(git(root, ['diff', '--name-only', '-z', '--no-renames', baseCommit, '--'])),
    ...paths(git(root, ['diff', '--cached', '--name-only', '-z', '--no-renames', baseCommit, '--'])),
    ...paths(git(root, ['ls-files', '--others', '--exclude-standard', '-z'])),
  ])].sort();
  const manifest = { version: 1, head, base: baseCommit, files: changed.map(path => fileState(root, path)) };
  // The digest represents final content, modes and deletions, independent of staging.
  return { ...manifest, digest: hash(JSON.stringify(manifest)) };
}

export function assertCurrent(candidate, root = repoRoot) {
  const current = captureCandidate(root, candidate.base);
  if (current.digest !== candidate.digest) throw new Error('Candidate changed; rerun quality and both reviews.');
  return current;
}

export function checkEvidence(reportPath, { root = repoRoot, reviews = [], staged = false } = {}) {
  const report = readJson(reportPath);
  if (report.version !== 1 || report.status !== 'PASS' || report.scope !== 'full') {
    throw new Error('A completed PASS from the full quality gate is required.');
  }
  if (!report.tests || report.tests.total <= 0 || report.tests.failed !== 0) {
    throw new Error('Missing or failing test evidence.');
  }
  const current = assertCurrent(report.candidate, root);
  if (reviews.length) {
    const expected = new Set(['code-reviewer', 'security-reviewer']);
    for (const path of reviews) {
      const review = readJson(path);
      if (!expected.delete(review.reviewer) || review.verdict !== 'PASS' ||
          review.candidateDigest !== current.digest || review.qualitySha256 !== hash(readFileSync(reportPath)) ||
          !review.summary?.trim() || !Array.isArray(review.changes) || !Array.isArray(review.risks)) {
        throw new Error(`Missing, stale or non-passing review: ${path}`);
      }
    }
    if (expected.size) throw new Error('Both distinct reviewer reports are required.');
  }
  if (staged) {
    if (reviews.length !== 2) throw new Error('Commit gate requires both reviewer reports.');
    if (git(root, ['diff', '--name-only', '-z', '--']).length ||
        git(root, ['ls-files', '--others', '--exclude-standard', '-z']).length) {
      throw new Error('Stage the complete candidate with explicit paths; unstaged/untracked changes remain.');
    }
    if (!git(root, ['diff', '--cached', '--name-only', '-z', '--']).length) throw new Error('No staged changes.');
    git(root, ['diff', '--cached', '--check']);
  }
  return { status: 'PASS', candidateDigest: current.digest, qualitySha256: hash(readFileSync(reportPath)) };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const [command, reportPath, ...rest] = process.argv.slice(2);
    const staged = rest.at(-1) === '--staged';
    const reviews = staged ? rest.slice(0, -1) : rest;
    if (command !== 'check' || !reportPath || ![0, 2].includes(reviews.length) || reviews.some(p => p.startsWith('--'))) {
      throw new Error('Usage: node scripts/ai/candidate.mjs check <quality.json> [<code-review.json> <security-review.json>] [--staged]');
    }
    console.log(JSON.stringify(checkEvidence(reportPath, { reviews, staged }), null, 2));
  } catch (error) {
    console.error(`BLOCKED: ${error.message}`);
    process.exitCode = 1;
  }
}
