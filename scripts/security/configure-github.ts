#!/usr/bin/env node
/**
 * MyAIAgent | Security Tooling
 *
 * Inspects repository protections and applies the declared policy only when explicitly requested.
 */

import { runProcess, type ProcessRunner } from './process.ts';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export type GithubApi = (method: string, path: string, payload?: unknown, missingOk?: boolean) => unknown;
export interface EnvironmentPayload {
  deployment_branch_policy: { protected_branches: boolean; custom_branch_policies: boolean };
  reviewers?: { type: string; id: number }[];
  prevent_self_review?: boolean;
  wait_timer?: number;
}
export interface Environment {
  protection_rules?: (
    | { type: 'required_reviewers'; reviewers: { type: string; reviewer: { id: number } }[]; prevent_self_review?: boolean }
    | { type: 'wait_timer'; wait_timer: number }
    | { type: 'branch_policy' | 'custom_deployment_protection_rule' }
  )[];
}
export interface DeploymentPolicy { name: string; type: string }
interface RepositoryMetadata {
  visibility: string;
  permissions?: { admin?: boolean };
  allow_squash_merge?: boolean;
  allow_rebase_merge?: boolean;
  default_branch: string;
  owner: { type: string; id: number };
}
type Operation = [method: string, path: string, configuration: object | null];

export function githubApi(method: string, path: string, payload: unknown = null, missingOk = false, run: ProcessRunner = runProcess): unknown {
  const args = ['api', '--method', method, path, '-H', 'X-GitHub-Api-Version: 2022-11-28'];
  if (payload !== null) args.push('--input', '-');
  const result = run('gh', args, {
    input: payload === null ? undefined : JSON.stringify(payload), encoding: 'utf8', shell: false,
  });
  if (result.status !== 0 || result.error || result.signal) {
    if (missingOk && result.status !== 0 && result.stderr?.toString().includes('(HTTP 404)')) return null;
    throw new Error(`GitHub ${method} ${path} failed; no credential output was printed.`);
  }
  try {
    const content = result.stdout?.toString().trim();
    return content ? JSON.parse(content) : {};
  } catch {
    throw new Error('GitHub returned invalid JSON; no response content was printed.');
  }
}

export function configureGithub(args: string[], { api = githubApi, output = console.log }: { api?: GithubApi; output?: (line: string) => void } = {}): void {
  const [repository, ...options] = args;
  if (!repository || !/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository)
      || options.length > 1 || options.some(option => option !== '--apply')) {
    throw new Error('Usage: npm run security:configure -- owner/repository [--apply]');
  }
  const root = `repos/${repository}`;
  const metadata = api('GET', root) as RepositoryMetadata;
  if (metadata.visibility !== 'public' || !metadata.permissions?.admin) {
    throw new Error('This script requires administrative access to the intended public repository.');
  }
  if (!metadata.allow_squash_merge && !metadata.allow_rebase_merge) {
    throw new Error('Enable squash or rebase merging before requiring linear history.');
  }
  const branch = encodeURIComponent(metadata.default_branch);
  const checks = (api('GET', `${root}/commits/${branch}/check-runs`) as { check_runs: { name: string }[] }).check_runs;
  if (!checks.some(check => check.name === 'quality')) {
    throw new Error('The default branch must already publish a quality check before requiring it.');
  }
  const operations: Operation[] = [
    ['PATCH', root, { security_and_analysis: {
      secret_scanning: { status: 'enabled' }, secret_scanning_push_protection: { status: 'enabled' },
    } }],
    ['PUT', `${root}/vulnerability-alerts`, null],
    ['PUT', `${root}/automated-security-fixes`, null],
    ['PUT', `${root}/private-vulnerability-reporting`, null],
  ];
  const protection = api('GET', `${root}/branches/${branch}/protection`, null, true);
  if (protection === null) {
    operations.push(['PUT', `${root}/branches/${branch}/protection`, {
      required_status_checks: { strict: true, contexts: ['quality', 'secrets'] },
      enforce_admins: true,
      // A solo maintainer can merge their own PR after checks without an unavailable second reviewer.
      required_pull_request_reviews: {
        dismiss_stale_reviews: true, require_code_owner_reviews: false, required_approving_review_count: 0,
      },
      restrictions: null,
      required_linear_history: true,
      allow_force_pushes: false,
      allow_deletions: false,
      required_conversation_resolution: true,
    }]);
  } else {
    output('Existing branch protection preserved; review its strength separately.');
  }
  const owner = metadata.owner;
  if (owner.type !== 'User') {
    throw new Error('Configure organization environment reviewers explicitly instead of using the solo-maintainer policy.');
  }
  for (const name of ['staging', 'production']) {
    const environment = api('GET', `${root}/environments/${name}`, null, true) as Environment | null;
    const payload: EnvironmentPayload = { deployment_branch_policy: { protected_branches: false, custom_branch_policies: true } };
    if (environment) {
      for (const rule of environment.protection_rules ?? []) {
        if (rule.type === 'required_reviewers') {
          payload.reviewers = rule.reviewers.map(reviewer => ({ type: reviewer.type, id: reviewer.reviewer.id }));
          payload.prevent_self_review = rule.prevent_self_review ?? false;
        } else if (rule.type === 'wait_timer') {
          payload.wait_timer = rule.wait_timer;
        }
      }
    }
    if (name === 'production' && !payload.reviewers?.length) {
      payload.reviewers = [{ type: 'User', id: owner.id }];
      payload.prevent_self_review ??= false;
    }
    operations.push(['PUT', `${root}/environments/${name}`, payload]);
    const policies = api('GET', `${root}/environments/${name}/deployment-branch-policies`, null, true) as { branch_policies: DeploymentPolicy[] } | null;
    const current = policies?.branch_policies ?? [];
    if (current.some(policy => policy.name !== metadata.default_branch || policy.type !== 'branch')) {
      throw new Error('Existing environment branch policies require manual review; no settings were changed.');
    }
    if (!current.length) {
      operations.push(['POST', `${root}/environments/${name}/deployment-branch-policies`,
        { name: metadata.default_branch, type: 'branch' }]);
    }
  }
  for (const [method, path, configuration] of operations) output(JSON.stringify({ method, path, configuration }));
  if (!options.includes('--apply')) {
    output('Inspection only. Use --apply to apply this protection plan.');
    return;
  }
  for (const [method, path, configuration] of operations) {
    api(method, path, configuration);
    output(`Applied ${method} ${path}`);
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    configureGithub(process.argv.slice(2));
  } catch (error) {
    console.error(error instanceof Error ? error.message : 'GitHub configuration failed.');
    process.exitCode = 1;
  }
}
