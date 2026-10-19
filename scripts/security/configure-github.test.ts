import test from 'node:test';
import assert from 'node:assert/strict';
import { configureGithub, githubApi, type GithubApi, type Environment, type EnvironmentPayload, type DeploymentPolicy } from './configure-github.ts';
import type { ProcessRunner } from './process.ts';

type CapturedPayload = EnvironmentPayload & { required_status_checks: { contexts: string[] }; enforce_admins: boolean };
function fixture({ protection = null, environment = null, policies = [] }:
  { protection?: object | null; environment?: Environment | null; policies?: DeploymentPolicy[] } = {}) {
  const writes: { method: string; path: string; payload: CapturedPayload }[] = [];
  const lines: string[] = [];
  const api: GithubApi = (method, path, payload = null) => {
    if (method !== 'GET') {
      writes.push({ method, path, payload: payload as CapturedPayload });
      return {};
    }
    if (path === 'repos/owner/repository') {
      return { visibility: 'public', permissions: { admin: true }, allow_squash_merge: true,
        default_branch: 'main', owner: { type: 'User', id: 123 } };
    }
    // The first hardening PR must be protected before main publishes the new secrets job.
    if (path.endsWith('/check-runs')) return { check_runs: [{ name: 'quality' }] };
    if (path.endsWith('/protection')) return protection;
    if (path.endsWith('/deployment-branch-policies')) return { branch_policies: policies };
    if (/\/environments\/(staging|production)$/.test(path)) return environment;
    throw new Error('Unexpected GitHub read: ' + path);
  };
  return { writes, lines, api, output: (line: string) => { lines.push(line); } };
}

test('dry run requires quality and secrets during initial rollout without mutations', () => {
  const context = fixture();

  configureGithub(['owner/repository'], context);

  const operations = context.lines.filter(line => line.startsWith('{')).map(line => JSON.parse(line));
  const rule = operations.find(operation => operation.path.endsWith('/branches/main/protection')).configuration;
  assert.deepEqual(rule.required_status_checks, { strict: true, contexts: ['quality', 'secrets'] });
  assert.deepEqual(context.writes, []);
});

test('explicit apply enforces both checks and production approval during bootstrap', () => {
  const context = fixture();

  configureGithub(['owner/repository', '--apply'], context);

  const rule = context.writes.find(operation => operation.path.endsWith('/branches/main/protection'))!.payload;
  assert.deepEqual(rule.required_status_checks.contexts, ['quality', 'secrets']);
  assert.equal(rule.enforce_admins, true);
  const production = context.writes.find(operation => operation.path.endsWith('/environments/production'))!.payload;
  assert.deepEqual(production.reviewers, [{ type: 'User', id: 123 }]);
});

test('unrecognized deployment policy aborts before any mutation', () => {
  const context = fixture({ policies: [{ name: 'feature/*', type: 'branch' }] });

  assert.throws(() => configureGithub(['owner/repository', '--apply'], context), /no settings were changed/);

  assert.deepEqual(context.writes, []);
});

test('existing production environment without reviewers gains owner approval and keeps its wait timer', () => {
  const context = fixture({ environment: { protection_rules: [{ type: 'wait_timer', wait_timer: 10 }] } });

  configureGithub(['owner/repository', '--apply'], context);

  const production = context.writes.find(operation => operation.path.endsWith('/environments/production'))!.payload;
  assert.deepEqual(production.reviewers, [{ type: 'User', id: 123 }]);
  assert.equal(production.prevent_self_review, false);
  assert.equal(production.wait_timer, 10);
  const staging = context.writes.find(operation => operation.path.endsWith('/environments/staging'))!.payload;
  assert.equal(staging.reviewers, undefined);
});

test('existing protection and stronger environment review requirements are preserved', () => {
  const context = fixture({ protection: { enforce_admins: { enabled: true } },
    environment: { protection_rules: [
      { type: 'required_reviewers', reviewers: [{ type: 'User', reviewer: { id: 456 } }], prevent_self_review: true },
      { type: 'wait_timer', wait_timer: 10 },
    ] }, policies: [{ name: 'main', type: 'branch' }] });

  configureGithub(['owner/repository', '--apply'], context);

  assert.equal(context.writes.some(operation => operation.path.includes('/branches/')), false);
  const production = context.writes.find(operation => operation.path.endsWith('/environments/production'))!.payload;
  assert.deepEqual(production.reviewers, [{ type: 'User', id: 456 }]);
  assert.equal(production.prevent_self_review, true);
  assert.equal(production.wait_timer, 10);
});

test('unknown flags and invalid repositories do not make API calls', () => {
  for (const args of [['owner/repository', '--force'], ['owner/repository;exit'], []]) {
    assert.throws(() => configureGithub(args, { api() { assert.fail('API must not run'); } }), /Usage:/);
  }
});

test('GitHub calls use argv and structured JSON input without exposing failed responses', () => {
  const payload = { text: 'a value with "quotes" and $(text)' };
  const credential = ['sensitive', 'response', 'value'].join('-');
  const run: ProcessRunner = (command, args, options) => {
    assert.equal(command, 'gh');
    assert.equal(options.shell, false);
    assert.equal(options.input, JSON.stringify(payload));
    assert.deepEqual(args.slice(-2), ['--input', '-']);
    return { status: 1, stderr: credential, stdout: credential };
  };

  assert.throws(() => githubApi('PATCH', 'repos/owner/repository', payload, false, run), error => {
    assert.ok(error instanceof Error);
    assert.equal(error.message.includes(credential), false);
    return /no credential output/.test(error.message);
  });
  assert.equal(githubApi('GET', 'missing', null, true, () => ({ status: 1, stderr: '(HTTP 404)' })), null);
  assert.throws(() => githubApi('GET', 'invalid', null, false,
    () => ({ status: 0, stdout: credential })), /invalid JSON; no response content/);
});
