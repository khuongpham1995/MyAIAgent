import test from 'node:test';
import assert from 'node:assert/strict';
import { lstatSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { repoRoot } from './quality.mjs';

function markdownFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return markdownFiles(path);
    return entry.isFile() && entry.name.endsWith('.md') ? [path] : [];
  });
}

test('both assistants discover every shared skill from the same physical directory', () => {
  const shared = join(repoRoot, '.agents/skills');
  const adapter = join(repoRoot, '.claude/skills');
  const names = readdirSync(shared).sort();
  assert.deepEqual(readdirSync(adapter).sort(), names);
  for (const name of names) {
    assert.ok(lstatSync(join(adapter, name)).isSymbolicLink(), name);
    assert.equal(realpathSync(join(adapter, name)), realpathSync(join(shared, name)));
    assert.ok(lstatSync(join(shared, name, 'SKILL.md')).isFile(), name);
  }
});

test('Claude rule discovery points to the canonical shared rules without copies', () => {
  const adapter = join(repoRoot, '.claude/rules');
  const shared = join(repoRoot, '.agents/rules');
  assert.deepEqual(readdirSync(adapter), ['shared']);
  assert.ok(lstatSync(join(adapter, 'shared')).isSymbolicLink());
  assert.equal(realpathSync(join(adapter, 'shared')), realpathSync(shared));
  assert.ok(markdownFiles(shared).length > 0);
});

test('canonical skill and rule references resolve after relocation', () => {
  const files = [...markdownFiles(join(repoRoot, '.agents')),
    ...markdownFiles(join(repoRoot, 'docs/ai'))];
  for (const path of files) {
    for (const match of readFileSync(path, 'utf8').matchAll(/`(\.agents\/(?:skills|rules)\/[^`]+\.md)`/g)) {
      if (match[1].includes('*')) continue;
      assert.ok(lstatSync(resolve(repoRoot, match[1])).isFile(), `${path}: ${match[1]}`);
    }
  }
});

test('quality service map covers every runnable test project in the solution', async () => {
  const { services, testProject } = await import('./quality.mjs');
  const solution = readFileSync(join(repoRoot, 'src/MyAIAgent.slnx'), 'utf8');
  const projects = [...solution.matchAll(/Path="([^"]+\.csproj)"/g)].map(match => `src/${match[1]}`);
  const runnable = projects.filter(path => /PackageReference Include="Microsoft.NET.Test.Sdk"/.test(readFileSync(join(repoRoot, path), 'utf8')));
  assert.deepEqual(services.map(testProject).sort(), runnable.sort());
});
