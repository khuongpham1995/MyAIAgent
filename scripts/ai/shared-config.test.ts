/**
 * MyAIAgent | Developer Tooling · AI Workflow · Tests
 *
 * Checks shared assistant configuration, canonical skill links, workflow policy, and safe repository automation.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, lstatSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { repoRoot } from './quality.ts';

function markdownFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return markdownFiles(path);
    return entry.isFile() && entry.name.endsWith('.md') ? [path] : [];
  });
}

function frontmatter(path: string): Record<string, string> {
  const match = readFileSync(path, 'utf8').match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n/);
  assert.ok(match, `${path}: missing frontmatter`);
  return Object.fromEntries(match[1].split(/\r?\n/).filter(line => /^[\w-]+:/.test(line))
    .map(line => [line.slice(0, line.indexOf(':')), line.slice(line.indexOf(':') + 1).trim()]));
}

const aiConfigPaths = ['.agents', '.claude', '.codex', '.github', 'scripts/ai',
  'AGENTS.md', 'CLAUDE.md', 'README.md'];

// Names only: a failed content assertion would print the whole file into the log.
function aiConfigFilesMatching(pattern: RegExp, { except = [] }: { except?: string[] } = {}): string[] {
  const result = spawnSync('git', ['ls-files', '--cached', '--others', '--exclude-standard',
    '--', ...aiConfigPaths], { cwd: repoRoot, encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  return result.stdout.split('\n').filter(Boolean).filter(path => {
    const absolute = join(repoRoot, path);
    // A tracked file deleted in the worktree has nothing left to scan.
    if (except.includes(path) || !existsSync(absolute) || !lstatSync(absolute).isFile()) return false;
    return pattern.test(readFileSync(absolute, 'utf8'));
  });
}

const sharedSkills = join(repoRoot, '.agents/skills');
const stageSkills = ['ai-plan', 'ai-build', 'ai-verify', 'ai-commit'];
const testFile = 'scripts/ai/shared-config.test.ts';

test('Claude discovers each canonical skill through a supported folder symlink', () => {
  // Arrange
  const adapter = join(repoRoot, '.claude/skills');

  // Act & Assert
  assert.ok(lstatSync(adapter).isDirectory());
  assert.deepEqual(readdirSync(adapter).sort(), readdirSync(sharedSkills).sort());
  for (const name of readdirSync(sharedSkills)) {
    assert.ok(lstatSync(join(adapter, name)).isSymbolicLink());
    assert.equal(realpathSync(join(adapter, name)), realpathSync(join(sharedSkills, name)));
  }
  // Keep native commands (especially /plan) and bundled /verify available.
  for (const name of ['plan', 'build', 'verify', 'commit']) {
    assert.ok(!existsSync(join(adapter, name)), name);
  }
});

test('every shared skill declares a name matching its directory', () => {
  // Arrange
  const names = readdirSync(sharedSkills);

  // Act & Assert
  for (const stage of stageSkills) assert.ok(names.includes(stage), stage);
  for (const name of names) {
    const fields = frontmatter(join(sharedSkills, name, 'SKILL.md'));
    assert.equal(fields.name, name);
    assert.ok(fields.description, `${name}: description`);
  }
});

test('the commit skill cannot run without the user naming it', () => {
  // Arrange
  const fields = frontmatter(join(sharedSkills, 'ai-commit/SKILL.md'));

  // Act & Assert
  assert.equal(fields['disable-model-invocation'], 'true');
  const policy = readFileSync(join(sharedSkills, 'ai-commit/agents/openai.yaml'), 'utf8');
  assert.ok(/^policy:\r?\n\s+allow_implicit_invocation: false\s*$/m.test(policy), 'ai-commit/agents/openai.yaml');
});

test('Claude rule discovery points to the canonical shared rules without copies', () => {
  // Arrange
  const adapter = join(repoRoot, '.claude/rules');
  const shared = join(repoRoot, '.agents/rules');

  // Act & Assert
  assert.deepEqual(readdirSync(adapter), ['shared']);
  assert.ok(lstatSync(join(adapter, 'shared')).isSymbolicLink());
  assert.equal(realpathSync(join(adapter, 'shared')), realpathSync(shared));
  assert.ok(markdownFiles(shared).length > 0);
});

test('references to shared skills, rules and docs resolve', () => {
  // Arrange
  const files = [...markdownFiles(join(repoRoot, '.agents')),
    ...markdownFiles(join(repoRoot, '.claude/agents')),
    ...readdirSync(join(repoRoot, '.codex/agents')).map(name => join(repoRoot, '.codex/agents', name)),
    join(repoRoot, 'AGENTS.md'), join(repoRoot, 'CLAUDE.md'), join(repoRoot, 'README.md')];
  let checked = 0;

  // Act & Assert
  for (const path of files) {
    for (const match of readFileSync(path, 'utf8').matchAll(/\.agents\/(?:skills|rules|docs)\/[\w./*-]+\.md/g)) {
      if (match[0].includes('*')) continue;
      assert.ok(lstatSync(resolve(repoRoot, match[0])).isFile(), `${path}: ${match[0]}`);
      checked += 1;
    }
  }
  assert.ok(checked > 0);
});

test('no file points at the locations that were consolidated into .agents', () => {
  // Arrange
  const stale = /docs\/ai\/|\.claude\/commands|ai-workflow|\.claude\/README/;

  // Act & Assert
  assert.deepEqual(aiConfigFilesMatching(stale, { except: [testFile] }), []);
});

test('the delivery options are defined only in the workflow policy', () => {
  // Arrange
  const options = /Commit only|Commit \+ push/i;

  // Act & Assert
  assert.deepEqual(aiConfigFilesMatching(options, { except: [testFile] }), ['.agents/docs/workflow.md']);
});

test('quality service map covers every runnable test project in the solution', async () => {
  // Arrange
  const { services, testProject } = await import('./quality.ts');
  const solution = readFileSync(join(repoRoot, 'src/MyAIAgent.slnx'), 'utf8');
  const projects = [...solution.matchAll(/Path="([^"]+\.csproj)"/g)].map(match => `src/${match[1]}`);
  const runnable = projects.filter(path => /PackageReference Include="Microsoft.NET.Test.Sdk"/.test(readFileSync(join(repoRoot, path), 'utf8')));

  // Act & Assert
  assert.deepEqual(services.map(testProject).sort(), runnable.sort());
});
