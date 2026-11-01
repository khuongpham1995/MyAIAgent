/**
 * MyAIAgent | Developer Tooling · AI Workflow · Tests
 *
 * Checks shared assistant configuration, canonical skill links, workflow policy, and safe repository automation.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, lstatSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
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

test('the separate audit skill has native picker metadata and a canonical Claude link', () => {
  // Arrange
  const directory = join(sharedSkills, 'ai-audit');
  const skill = readFileSync(join(directory, 'SKILL.md'), 'utf8');
  const picker = readFileSync(join(directory, 'agents/openai.yaml'), 'utf8');

  // Act & Assert
  assert.equal(frontmatter(join(directory, 'SKILL.md')).name, 'ai-audit');
  assert.match(picker, /display_name: "AI Audit"/);
  assert.match(picker, /\$ai-audit/);
  assert.match(skill, /source-only/);
  assert.equal(realpathSync(join(repoRoot, '.claude/skills/ai-audit')), realpathSync(directory));
  assert.ok(!stageSkills.includes('ai-audit'));
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

test('required decisions use a permitted interactive Question before plain-text fallback', () => {
  // Arrange
  const workflow = readFileSync(join(repoRoot, '.agents/docs/workflow.md'), 'utf8');
  const agreements = readFileSync(join(repoRoot, 'AGENTS.md'), 'utf8');

  // Act
  const policy = workflow.split('## Questions and approvals\n')[1]?.split('\n## Plan')[0];

  // Assert
  assert.ok(policy, 'Canonical Question policy is missing.');
  assert.match(policy, /interactive Question UI/);
  assert.match(policy, /request_user_input_async/);
  assert.match(policy, /plain-text questions only when no permitted interactive tool is available/);
  assert.match(policy, /preselected\s+option, timeout, or missing answer is not consent/);
  assert.match(agreements, /interactive Question policy in `.agents\/docs\/workflow\.md`/);
});

test('native preflight reuses same-session model confirmation without overriding host state', () => {
  // Arrange
  const setup = readFileSync(join(repoRoot, '.agents/docs/setup.md'), 'utf8');

  // Act
  const policy = setup.split('## Native mode/model preflight\n')[1]?.split('\n## Claude Code')[0];

  // Assert
  assert.ok(policy, 'Native preflight policy is missing.');
  assert.match(policy, /Reuse an explicit model confirmation from the same session/);
  assert.match(policy, /no later evidence contradicts/);
  assert.match(policy, /Host-reported mode\/model state takes precedence/);
  assert.match(policy, /cannot override a known mode mismatch/);
  assert.match(policy, /without repeating the same Question or manual\s+instructions/);
  assert.match(policy, /do not\s+claim that submitting the Question changed the active mode or model/);
});

test('Question policy preserves authorization and delivery gates', () => {
  // Arrange
  const workflow = readFileSync(join(repoRoot, '.agents/docs/workflow.md'), 'utf8');

  // Act
  const questionPolicy = workflow.split('## Questions and approvals\n')[1]?.split('\n## Plan')[0];
  const deliveryPolicy = workflow.split('## Commit and optional PR\n')[1];

  // Assert
  assert.match(questionPolicy, /stage transition alone does not\s+require approval/);
  assert.match(questionPolicy, /does not change native mode/);
  assert.match(questionPolicy, /replace quality\/review gates/);
  assert.match(deliveryPolicy, /Only a direct user invocation of Commit or an explicit user request to commit/);
  assert.match(deliveryPolicy, /Wait for an explicit answer before staging or committing/);
  assert.match(deliveryPolicy, /--staged/);
});

test('quality service map covers every runnable test project in the solution', async () => {
  // Arrange
  const { services, testProject } = await import('./quality.ts');
  const solution = readFileSync(join(repoRoot, 'src/MyAIAgent.slnx'), 'utf8');
  const projects = [...solution.matchAll(/Path="([^"]+\.csproj)"/g)]
    .map(match => relative(repoRoot, resolve(repoRoot, 'src', match[1])).replaceAll('\\', '/'));
  const runnable = projects.filter(path => /PackageReference Include="Microsoft.NET.Test.Sdk"/.test(readFileSync(join(repoRoot, path), 'utf8')));

  // Act & Assert
  assert.deepEqual(services.map(testProject).sort(), runnable.sort());
  assert.ok(runnable.every(path => path.startsWith('tests/')), 'Runnable tests belong outside production src.');
});
