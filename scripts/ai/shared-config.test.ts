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

test('Build worker adapters follow the shared contract and two-worker limit', () => {
  const contract = readFileSync(join(repoRoot, '.agents/docs/build-workers.md'), 'utf8');
  const workflow = readFileSync(join(repoRoot, '.agents/docs/workflow.md'), 'utf8');
  const codex = readFileSync(join(repoRoot, '.codex/agents/build-worker.toml'), 'utf8');
  const claude = readFileSync(join(repoRoot, '.claude/agents/build-worker.md'), 'utf8');
  const config = readFileSync(join(repoRoot, '.codex/config.toml'), 'utf8');
  const claudeSettings = JSON.parse(readFileSync(join(repoRoot, '.claude/settings.json'), 'utf8'));

  assert.match(contract, /disjoint file ownership/);
  assert.match(contract, /must not spawn agents, stage or commit, push/);
  assert.match(workflow, /up\s+to two Build subagents concurrently/);
  assert.match(config, /max_concurrent_threads_per_session = 2/);
  assert.equal(claudeSettings.env.CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS, '2');
  assert.equal(claudeSettings.env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH, '1');
  assert.match(codex, /name = "build-worker"/);
  assert.match(codex, /model = "gpt-6-sol"/);
  assert.match(codex, /\.agents\/docs\/build-workers\.md/);
  assert.equal(frontmatter(join(repoRoot, '.claude/agents/build-worker.md')).model, 'sonnet');
  assert.match(claude, /\.agents\/docs\/build-workers\.md/);
});

const teamDirectory = join(repoRoot, '.agents/docs/team');
const personaSections = ['Identity', 'Domain knowledge', 'Working experience',
  'Personality and tone', 'Ownership', 'Report'];
const readOnlyAgents = ['tech-lead', 'code-reviewer', 'security-reviewer'];

function names(directory: string, extension: string): string[] {
  return readdirSync(directory).filter(name => name.endsWith(extension))
    .map(name => name.slice(0, -extension.length)).sort();
}

interface RosterRow { agent: string; track: string; stage: string; access: string }

function rosterRows(): RosterRow[] {
  const roster = readFileSync(join(repoRoot, '.agents/docs/team.md'), 'utf8');
  return [...roster.matchAll(/^\| `([\w-]+)` \|(.+)\|$/gm)].map(match => {
    const cells = match[2].split('|').map(cell => cell.trim());
    assert.equal(cells.length, 5, `${match[1]}: roster row needs persona, track, stage, owns and access`);
    return { agent: match[1], track: cells[1], stage: cells[2], access: cells[4] };
  });
}

test('every agent has one persona, both client adapters and a roster row', () => {
  // Arrange
  const personas = names(teamDirectory, '.md');

  // Act & Assert
  assert.deepEqual(names(join(repoRoot, '.claude/agents'), '.md'), personas);
  assert.deepEqual(names(join(repoRoot, '.codex/agents'), '.toml'), personas);
  for (const name of personas) {
    const reference = `.agents/docs/team/${name}.md`;
    const claude = readFileSync(join(repoRoot, '.claude/agents', `${name}.md`), 'utf8');
    const codex = readFileSync(join(repoRoot, '.codex/agents', `${name}.toml`), 'utf8');
    assert.equal(frontmatter(join(repoRoot, '.claude/agents', `${name}.md`)).name, name);
    assert.match(codex, new RegExp(`^name = "${name}"$`, 'm'));
    assert.ok(claude.includes(reference), `${name}: Claude adapter must load its persona`);
    assert.ok(codex.includes(reference), `${name}: Codex adapter must load its persona`);
  }
  assert.deepEqual(rosterRows().map(row => row.agent).sort(), personas);
});

test('the roster separates leads, SDEs, the SDET and read-only reviewers', () => {
  // Arrange
  const rows = rosterRows();

  // Act
  const byAgent = new Map(rows.map(row => [row.agent, row]));

  // Assert
  assert.equal(byAgent.get('test-engineer')?.track, 'SDET');
  for (const row of rows.filter(row => row.track === 'SDE')) {
    const persona = readFileSync(join(teamDirectory, `${row.agent}.md`), 'utf8');
    assert.match(persona, /\(SDET\) owns the tests for a behavior, implement against them without editing\s+them/,
      `${row.agent}: SDEs must not edit SDET-owned tests`);
  }
  for (const row of rows) {
    const readOnly = readOnlyAgents.includes(row.agent);
    assert.equal(row.access, readOnly ? 'Read-only' : 'Assigned files', `${row.agent}: access`);
    if (!readOnly) {
      assert.equal(row.stage, 'Build', `${row.agent}: stage`);
      assert.ok(['SDE', 'SDET'].includes(row.track), `${row.agent}: track`);
    }
  }
  const contract = readFileSync(join(repoRoot, '.agents/docs/build-workers.md'), 'utf8');
  assert.match(contract, /## Test-first order/);
  assert.match(contract, /before the SDE for that\s+behavior, never concurrently/);
});

test('every persona defines its role, knowledge, lessons, voice, ownership and report', () => {
  // Arrange
  const personas = names(teamDirectory, '.md');

  // Act & Assert
  assert.ok(personas.length > 0);
  for (const name of personas) {
    const persona = readFileSync(join(teamDirectory, `${name}.md`), 'utf8');
    const headings = [...persona.matchAll(/^## (.+)$/gm)].map(match => match[1].trim());
    assert.deepEqual(headings, personaSections, `${name}: persona sections`);
    assert.match(persona, /^# \S.+ — .+$/m, `${name}: title names the persona and role`);
  }
});

test('agent adapters use only the reviewed configuration keys', () => {
  // Arrange
  const claudeKeys = ['name', 'description', 'model', 'tools', 'permissionMode', 'skills', 'memory'];
  const codexKeys = ['name', 'description', 'model', 'model_reasoning_effort', 'sandbox_mode',
    'developer_instructions'];

  for (const name of names(teamDirectory, '.md')) {
    // Act
    const claude = Object.keys(frontmatter(join(repoRoot, '.claude/agents', `${name}.md`)));
    const codex = readFileSync(join(repoRoot, '.codex/agents', `${name}.toml`), 'utf8')
      .replace(/"""[\s\S]*?"""/g, '""').split(/\r?\n/)
      .filter(line => line.trim() && !line.trim().startsWith('#'))
      .map(line => line.match(/^([\w-]+)\s*=/)?.[1] ?? line.trim());

    // Assert
    assert.deepEqual(claude.filter(key => !claudeKeys.includes(key)), [], `${name}: Claude keys`);
    assert.deepEqual(codex.filter(key => !codexKeys.includes(key)), [], `${name}: Codex keys`);
  }
});

test('Claude agents preload real skills and only Build agents keep ignored local memory', () => {
  // Arrange
  const ignored = readFileSync(join(repoRoot, '.gitignore'), 'utf8');

  for (const name of names(teamDirectory, '.md')) {
    // Act
    const fields = frontmatter(join(repoRoot, '.claude/agents', `${name}.md`));
    const skills = fields.skills === undefined ? [] : fields.skills.match(/^\[(.*)\]$/)?.[1]
      .split(',').map(skill => skill.trim()).filter(Boolean);

    // Assert
    assert.ok(skills, `${name}: skills must be an inline list`);
    for (const skill of skills) {
      const skillFile = join(sharedSkills, skill, 'SKILL.md');
      assert.ok(existsSync(skillFile), `${name}: unknown skill ${skill}`);
      assert.notEqual(frontmatter(skillFile)['disable-model-invocation'], 'true', `${name}: ${skill} cannot be preloaded`);
    }
    assert.equal(fields.memory, readOnlyAgents.includes(name) ? undefined : 'local',
      `${name}: memory grants writes, so only Build agents keep it, locally`);
  }
  assert.match(ignored, /^\.claude\/agent-memory-local\/$/m);
});

test('personas stop at the criteria and reviews receive facts, not conclusions', () => {
  // Arrange
  const review = readFileSync(join(repoRoot, '.agents/docs/review.md'), 'utf8');
  const workflow = readFileSync(join(repoRoot, '.agents/docs/workflow.md'), 'utf8');

  // Act
  const buildAgents = rosterRows().filter(row => row.stage === 'Build').map(row => row.agent);

  // Assert
  assert.match(review, /## Blind review inputs/);
  assert.match(review, /Owner conclusions and reassurances[\s\S]*?are\s+not evidence/);
  assert.match(workflow.split('## Verify')[1].split('\n## ')[0],
    /Send facts, not conclusions: follow\s+\*\*Blind review inputs\*\*/);
  for (const name of names(teamDirectory, '.md')) {
    const personality = readFileSync(join(teamDirectory, `${name}.md`), 'utf8')
      .split('## Personality and tone')[1].split('\n## ')[0];
    const behaviors = personality.split(/^- /m).slice(1);
    assert.ok(behaviors.length > 0, `${name}: personality is a list of behaviors`);
    for (const behavior of behaviors) {
      assert.match(behavior, /\bbecause\b/, `${name}: each behavior states its reason`);
    }
    if (buildAgents.includes(name)) assert.match(personality, /\bstop\b/i, `${name}: Build personas state when to stop`);
    if (readOnlyAgents.includes(name)) assert.match(personality, /\bframing\b/, `${name}: reviewers resist framing`);
  }
});

test('agent memory is untrusted, local and never authority', () => {
  for (const doc of ['.agents/docs/build-workers.md', '.agents/docs/team-maintenance.md']) {
    // Arrange
    const text = readFileSync(join(repoRoot, doc), 'utf8');

    // Act & Assert
    assert.match(text, /\.claude\/agent-memory-local\//, doc);
    assert.match(text, /untrusted[\s\S]{0,200}?never\s+instructions\s+or\s+authority/, doc);
  }
});

test('every persona lesson cites a source that exists', () => {
  const listing = spawnSync('git', ['ls-files', '--cached', '--others', '--exclude-standard'], { cwd: repoRoot, encoding: 'utf8' });
  assert.equal(listing.status, 0, listing.stderr);
  const tracked = listing.stdout.split('\n').filter(Boolean);

  for (const name of names(teamDirectory, '.md')) {
    // Arrange
    const lines = readFileSync(join(teamDirectory, `${name}.md`), 'utf8')
      .split('## Working experience')[1].split('\n## ')[0].split(/\r?\n/);

    // Act: a lesson is a bullet plus its indented continuation lines; any other line ends it.
    const lessons: string[][] = [];
    for (const line of lines) {
      if (line.startsWith('- ')) lessons.push([line]);
      else if (line.startsWith('  ') && lessons.length) lessons[lessons.length - 1].push(line);
      else if (lessons.length && lessons[lessons.length - 1].length) lessons.push([]);
    }
    const bullets = lessons.filter(lesson => lesson.length);

    // Assert
    assert.ok(bullets.length >= 3 && bullets.length <= 5, `${name}: 3–5 working-experience principles`);
    for (const lesson of bullets) {
      const sources = lesson.filter(line => /^\s+Source: /.test(line));
      const source = lesson[lesson.length - 1].match(/^ {2}Source: (.+)\.$/)?.[1];
      assert.ok(source && sources.length === 1, `${name}: "${lesson[0].slice(0, 50)}" must end with one Source line`);
      for (const item of source.split('; ')) {
        const path = item.match(/^`([^`]+)`$/)?.[1];
        const commit = item.match(/^commit ([0-9a-f]{7,40})$/)?.[1];
        if (path) {
          const matches = tracked.filter(file => file === path || file.endsWith(`/${path}`) || file.endsWith(`.${path}`));
          assert.equal(matches.length, 1, `${name}: source ${path} must match exactly one file`);
        } else if (commit) {
          const result = spawnSync('git', ['cat-file', '-e', `${commit}^{commit}`], { cwd: repoRoot });
          assert.equal(result.status, 0, `${name}: unknown commit ${commit}`);
        } else {
          assert.match(item, /^.+ review, \d{4}-\d{2}-\d{2}$/, `${name}: source "${item}"`);
        }
      }
    }
  }
});

test('reviewers disprove findings first and agent-loaded docs stay within budget', () => {
  // Arrange
  const review = readFileSync(join(repoRoot, '.agents/docs/review.md'), 'utf8');

  // Act
  const loaded = ['.agents/docs/team.md', '.agents/docs/review.md', '.agents/docs/build-workers.md',
    ...names(teamDirectory, '.md').map(name => `.agents/docs/team/${name}.md`)];

  // Assert
  assert.match(review, /Try to disprove each finding first/);
  assert.match(review, /\*\*Needs validation\*\*[\s\S]*?do not count as\s+findings/);
  assert.match(review, /confirmed finding[\s\S]*?never moves to this list/);
  assert.match(review, /unresolved trust-boundary lead blocks PASS \(return\s+\*\*NEEDS FIXES\*\*\)/);
  assert.match(review, /\*\*Needs validation\*\* list without severity/);
  for (const name of ['code-reviewer', 'security-reviewer']) {
    assert.match(readFileSync(join(teamDirectory, `${name}.md`), 'utf8'), /disprove[\s\S]*?Needs validation/, name);
  }
  // Every spawn loads these files; growth needs a deliberate budget change.
  for (const path of loaded) {
    assert.ok(readFileSync(join(repoRoot, path), 'utf8').length <= 6000, `${path}: over the 6000-character budget`);
  }
});

test('the team shares one goal and the lead critiques both plan and result', () => {
  // Arrange
  const read = (path: string) => readFileSync(join(repoRoot, path), 'utf8');
  const team = read('.agents/docs/team.md');
  const verify = read('.agents/docs/workflow.md').split('## Verify')[1].split('\n## ')[0];
  const lead = read('.agents/docs/team/tech-lead.md');

  // Act
  const leadRow = rosterRows().find(row => row.agent === 'tech-lead');

  // Assert
  assert.match(team, /## Shared goal[\s\S]*?non-goals[\s\S]*?`Goal: <one line> — served \| at risk`/);
  assert.match(read('.agents/docs/build-workers.md'), /goal, non-goals, exact owned paths/);
  assert.match(read('.agents/docs/review.md'), /Non-goals limit the change, not the review/);
  assert.equal(leadRow?.stage, 'Plan; after Verify');
  assert.match(team, /draft path last/);
  assert.match(verify, /Result review: once both reviewers return PASS[\s\S]*?cannot\s+clear\s+or\s+downgrade\s+a\s+reviewer\s+finding[\s\S]*?`lead-review\.json`/);
  assert.match(read('.agents/skills/ai-verify/SKILL.md'), /After both PASS, run the `tech-lead` result\s+review alone/);
  assert.match(read('.agents/docs/evidence.md'), /security-review\.json artifacts\/ai\/<run>\/lead-review\.json --staged/);
  assert.match(lead, /\*\*Independent findings\*\*[\s\S]*?APPROVE\/REVISE[\s\S]*?ALIGNED\/REVISE/);
  assert.match(verify, /relaunch\s+`tech-lead` on the same candidate for a fresh record\. Never edit a returned verdict\./);
  assert.match(verify, /owner's draft Summary \/ Changes \/ Risks as a path it opens last/);
  for (const path of ['.agents/docs/review.md', '.agents/docs/build-workers.md']) {
    assert.match(read(path), /`Goal: <one line> — served \| at risk`/, `${path}: Goal line`);
  }
  assert.match(read('.agents/skills/ai-plan/SKILL.md'), /Send `tech-lead` the goal before the draft/);
  assert.match(read('.agents/docs/setup.md'), /`tech-lead` reviews\s+plans and, after both reviewers, the final result/);
  assert.match(read('AGENTS.md'), /tech-lead result review alone; commit needs its ALIGNED record/);
});

test('plan and review personas stay read-only in both clients', () => {
  for (const name of readOnlyAgents) {
    // Arrange
    const fields = frontmatter(join(repoRoot, '.claude/agents', `${name}.md`));
    const codex = readFileSync(join(repoRoot, '.codex/agents', `${name}.toml`), 'utf8');

    // Act
    const tools = fields.tools.split(',').map(tool => tool.trim()).sort();

    // Assert
    assert.deepEqual(tools, ['Bash', 'Glob', 'Grep', 'Read'], `${name}: tools`);
    assert.equal(fields.permissionMode, 'plan', `${name}: permission mode`);
    assert.match(codex, /^sandbox_mode = "read-only"$/m, `${name}: sandbox`);
  }
  assert.equal(frontmatter(join(repoRoot, '.claude/agents/tech-lead.md')).model, 'opus');
  assert.match(readFileSync(join(repoRoot, '.codex/agents/tech-lead.toml'), 'utf8'),
    /^model = "gpt-6-astra"$/m);
});

test('Build agents use the execution model and the shared build contract', () => {
  // Arrange
  const buildAgents = names(teamDirectory, '.md').filter(name => !readOnlyAgents.includes(name));

  // Act & Assert
  assert.ok(buildAgents.includes('build-worker'));
  for (const name of buildAgents) {
    const claude = readFileSync(join(repoRoot, '.claude/agents', `${name}.md`), 'utf8');
    const codex = readFileSync(join(repoRoot, '.codex/agents', `${name}.toml`), 'utf8');
    const fields = frontmatter(join(repoRoot, '.claude/agents', `${name}.md`));
    assert.equal(fields.model, 'sonnet', name);
    assert.equal(fields.permissionMode, 'default', `${name}: Build agents ask before widening`);
    assert.equal(fields.tools, undefined, `${name}: Build agents keep the default tool set`);
    assert.match(codex, /^model = "gpt-6-sol"$/m, name);
    assert.doesNotMatch(codex, /^(sandbox_mode|approval_policy)\s*=/m, `${name}: Codex inherits project sandbox`);
    assert.ok(claude.includes('.agents/docs/build-workers.md'), `${name}: Claude build contract`);
    assert.ok(codex.includes('.agents/docs/build-workers.md'), `${name}: Codex build contract`);
  }
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
