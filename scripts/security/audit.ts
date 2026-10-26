#!/usr/bin/env node
/**
 * MyAIAgent | Security Tooling
 *
 * Binds source-only audit evidence to a candidate, checks independent agent records, and renders partial-coverage reports.
 */

import { constants, closeSync, existsSync, fstatSync, lstatSync, mkdirSync, mkdtempSync, openSync, readFileSync, readdirSync, readSync, realpathSync, renameSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import { assertCurrent, captureCandidate, git, hash, repoRoot } from '../ai/candidate.ts';
import type { Candidate } from '../ai/types.ts';
import * as findingsValidator from './validate-findings.ts';
import * as coverageValidator from './validate-coverage-ledger.ts';

export const revision = 'c1c8a8c1471069fb0e188eeaff69b8e8db6564a8';
// Change only alongside an intentional upstream revision/provenance review.
const manifestSha256 = '4fca2690ed76c036928636ec41c108a5068bcdcbea323040cd36187fbb9d2cc8';
export const vendorDirectory = resolve(repoRoot, '.agents/vendor/security-audit');
const maximumBytes = 5 * 1024 * 1024;
const roles = ['reconnaissance', 'hunter', 'critic', 'verifier'] as const;
type Role = typeof roles[number];
type ObjectData = Record<string, unknown>;
export interface AgentCall { id: string; role: Role; native_id: string | null; status: 'reserved' | 'complete' }
export interface AuditMetadata {
  version: 1; run_id: string; repo: string; target: string; source_ref: Candidate; profile: 'quick'; scope_paths: string[];
  budget: number; execution_policy: 'source-only'; run_status: 'in_progress' | 'complete' | 'incomplete';
  incomplete_reason: string | null; agents: AgentCall[]; candidates: { fingerprint: string; finder_id: string }[];
  selected_companions: string[]; prior_runs: string[];
  reports?: Record<string, string>;
  report_inputs_digest?: string;
  critic_input_digest?: string;
}
interface CriticInput { metadata: ObjectData; architecture: string; ledger: ObjectData[]; findings: ObjectData[]; results: Record<string, string> }
interface Validator { validateDocument(value: unknown): string[] }
interface CoverageValidator extends Validator { preflightJsonText(text: string): void; canonicalCoverageId(refs: ObjectData): string }
let validators: { findings: Validator; coverage: CoverageValidator } | undefined;

function object(value: unknown): ObjectData {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Expected an object.');
  return value as ObjectData;
}
function safeId(value: unknown): value is string {
  return typeof value === 'string' && /^[a-z0-9][a-z0-9_-]{0,63}$/.test(value)
    && !/^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/.test(value);
}
function text(value: unknown): value is string { return typeof value === 'string' && value.length > 0 && value.length <= 4096 && !/[\p{Cc}\p{Cf}]/u.test(value); }
const digest = (value: unknown): value is string => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);
const coverageId = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 65536 && !/[\p{Cc}\p{Cf}]/u.test(value);
const same = (left: unknown, right: unknown) => JSON.stringify(left) === JSON.stringify(right);
function textList(value: unknown): value is string[] { return Array.isArray(value) && value.length <= 1000 && value.every(text); }

function validateResult(role: Role, value: unknown): ObjectData {
  const result = object(value);
  let valid = false;
  if (role === 'reconnaissance') {
    valid = Array.isArray(result.facts) && result.facts.length > 0 && result.facts.length <= 1000
      && result.facts.every(value => {
        const fact = object(value);
        return text(fact.topic) && text(fact.description) && coverageValidator.isSafeRelativePath(fact.file)
          && Number.isInteger(fact.line) && Number(fact.line) > 0;
      }) && textList(result.starting_paths) && result.starting_paths.length > 0 && textList(result.limits);
  } else if (role === 'critic') {
    valid = digest(result.input_digest) && Array.isArray(result.missing_units) && result.missing_units.length <= 1000
      && Array.isArray(result.reassign_ids) && result.reassign_ids.length <= 1000 && result.reassign_ids.every(coverageId)
      && textList(result.resolved_prior_leads) && typeof result.stop === 'boolean'
      && result.stop === (result.missing_units.length === 0 && result.reassign_ids.length === 0)
      && new Set(result.reassign_ids).size === result.reassign_ids.length;
    if (valid) {
      const reasons = result.reassignment_reasons === undefined ? {} : object(result.reassignment_reasons);
      valid = (result.reassign_ids as string[]).every(id => text(reasons[id]));
      for (const found of result.missing_units as unknown[]) deferredCriticUnit(object(found));
    }
  } else if (role === 'hunter') {
    valid = Array.isArray(result.units) && result.units.length > 0 && result.units.length <= 1000
      && result.units.every(value => {
        const unit = object(value);
        return coverageId(unit.coverage_id) && textList(unit.reviewed_paths) && unit.reviewed_paths.length > 0
          && Array.isArray(unit.local_checks) && unit.local_checks.length > 0;
      }) && Array.isArray(result.candidates) && result.candidates.length <= 1000
      && result.candidates.every(value => text(object(value).fingerprint));
  } else {
    const record = object(result.record);
    valid = text(result.fingerprint) && result.fingerprint === record.fingerprint && result.decision === record.verdict
      && validation().findings.validateDocument([record]).length === 0;
  }
  if (!valid) throw new Error(`Invalid ${role} result: required role-specific evidence is missing or malformed.`);
  return result;
}

function deferredCriticUnit(found: ObjectData): ObjectData {
  if (!text(found.reason)) throw new Error('Critic gap requires a source-backed reason.');
  const refs = object(found.canonical_refs);
  const unit = {
    coverage_id: canonicalCoverageId(refs), canonical_refs: refs, surface: found.surface, boundary: found.boundary,
    subsystem: found.subsystem, attack_class: found.attack_class, starting_paths: found.starting_paths,
    ordinary_attack_class_block: found.ordinary_attack_class_block, selected_companion_blocks: found.selected_companion_blocks,
    excluded_blocks: found.excluded_blocks, prior_status: 'none', attempts: [], wave: 1,
    status: found.status === 'out_of_scope' ? 'out_of_scope' : 'deferred', agent_id: null, reviewed_paths: [], local_checks: [],
    result_fingerprints: [], unresolved: [`quick_profile_final_critic: ${found.reason}`],
  };
  if (validation().coverage.validateDocument([unit]).length) throw new Error('Invalid critic coverage gap.');
  return unit;
}

function reconciledLedger(input: CriticInput, result: ObjectData): ObjectData[] {
  const ledger = structuredClone(input.ledger);
  const reasons = result.reassignment_reasons === undefined ? {} : object(result.reassignment_reasons);
  for (const id of result.reassign_ids as string[]) {
    const unit = ledger.find(unit => unit.coverage_id === id);
    if (!unit || !['covered', 'candidate', 'blocked'].includes(String(unit.status))) throw new Error('Critic reassignment must identify a reviewed unit.');
    const { wave, status, agent_id, reviewed_paths, local_checks, result_fingerprints, unresolved } = unit;
    (unit.attempts as unknown[]).push({ wave, status, agent_id, reviewed_paths, local_checks, result_fingerprints, unresolved, reassignment_reason: reasons[id] });
    Object.assign(unit, { wave: Number(wave) + 1, status: 'deferred', agent_id: null, reviewed_paths: [], local_checks: [],
      result_fingerprints: [], unresolved: [`quick_profile_final_critic: ${reasons[id]}`] });
  }
  for (const found of result.missing_units as ObjectData[]) {
    const unit = deferredCriticUnit(found);
    if (ledger.some(existing => existing.coverage_id === unit.coverage_id)) throw new Error('Duplicate critic coverage identity.');
    ledger.push(unit);
  }
  ledger.sort((a, b) => String(a.coverage_id) < String(b.coverage_id) ? -1 : String(a.coverage_id) > String(b.coverage_id) ? 1 : 0);
  if (validation().coverage.validateDocument(ledger).length) throw new Error('Invalid critic reconciliation.');
  return ledger;
}

function criticInput(run: string, meta: AuditMetadata, root: string): CriticInput {
  const { run_status, incomplete_reason, critic_input_digest, reports, report_inputs_digest, agents, ...stable } = meta;
  const reviewed = agents.filter(agent => ['reconnaissance', 'hunter'].includes(agent.role));
  const read = (name: string) => readBounded(confined(realpathSync(root), join(run, name)));
  return { metadata: { ...stable, agents: reviewed }, architecture: read('architecture.md'),
    ledger: JSON.parse(read('coverage-ledger.json')) as ObjectData[], findings: JSON.parse(read('findings.json')) as ObjectData[],
    results: Object.fromEntries(reviewed.map(agent => [agent.id, hash(read(`agents/${agent.id}/result.json`))])) };
}

function readCriticInput(run: string, meta: AuditMetadata, root: string): CriticInput {
  const raw = readBounded(confined(realpathSync(root), join(run, 'critic-input.json')));
  if (!digest(meta.critic_input_digest) || hash(raw) !== meta.critic_input_digest) throw new Error('Critic input snapshot changed or is missing.');
  validation().coverage.preflightJsonText(raw);
  return object(JSON.parse(raw)) as unknown as CriticInput;
}

function reportInputsDigest(run: string, meta: AuditMetadata, root: string): string {
  const { report_inputs_digest, ...metadata } = meta;
  const files = ['architecture.md', 'coverage-ledger.json', 'findings.json', 'critic-input.json',
    ...meta.agents.map(agent => `agents/${agent.id}/result.json`)];
  const inputs = Object.fromEntries(files.map(name => {
    const path = confined(realpathSync(root), join(run, name));
    return [name, existsSync(path) ? hash(readBounded(path)) : null];
  }));
  return hash(JSON.stringify({ metadata, inputs }));
}

function confined(root: string, path: string): string {
  const full = resolve(path);
  const rel = relative(root, full);
  if (!rel || isAbsolute(rel) || rel.split(/[\\/]/).includes('..')) throw new Error('Path is outside the audit root.');
  let current = root;
  for (const part of rel.split('/')) {
    current = join(current, part);
    const stat = lstatSync(current, { throwIfNoEntry: false });
    if (stat?.isSymbolicLink() || (stat && !stat.isDirectory() && !stat.isFile())) throw new Error('Audit paths must be ordinary files or directories without links.');
  }
  return full;
}

function readBounded(path: string): string {
  const descriptor = openSync(path, constants.O_RDONLY | constants.O_NOFOLLOW | constants.O_NONBLOCK);
  try {
    const before = fstatSync(descriptor);
    if (!before.isFile() || before.nlink !== 1 || before.size > maximumBytes) throw new Error('Audit input is not a bounded regular file.');
    const data = Buffer.alloc(maximumBytes + 1);
    let length = 0;
    while (length < data.length) {
      const count = readSync(descriptor, data, length, data.length - length, null);
      if (count === 0) break;
      length += count;
    }
    const after = fstatSync(descriptor);
    if (length > maximumBytes || length !== before.size || after.size !== before.size || after.nlink !== 1 || after.ino !== before.ino)
      throw new Error('Audit input changed or exceeded its bound.');
    return new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(data.subarray(0, length));
  } finally { closeSync(descriptor); }
}

export function verifyVendor(directory = vendorDirectory): void {
  const root = realpathSync(directory);
  if (root !== resolve(directory)) throw new Error('Vendor directory cannot be a symlink.');
  const manifestText = readBounded(join(root, 'source.json'));
  if (hash(manifestText) !== manifestSha256) throw new Error('Audit vendor manifest does not match the pinned revision.');
  const manifest = object(JSON.parse(manifestText));
  if (manifest.revision !== revision || manifest.repository !== 'https://github.com/cloudflare/security-audit-skill' || manifest.license !== 'MIT')
    throw new Error('Unexpected audit vendor provenance.');
  const files = object(manifest.files);
  const names = Object.keys(files).sort();
  if (!names.includes('LICENSE') || !names.includes('report-schema.json') || names.length !== 21
    || JSON.stringify(readdirSync(root).filter(name => name !== 'source.json').sort()) !== JSON.stringify(names))
    throw new Error('Audit vendor inventory changed.');
  for (const name of names) {
    if (name !== name.split(/[\\/]/).pop() || name === '.' || name === '..' || !/^[a-zA-Z0-9._-]+$/.test(name)
      || hash(readBounded(confined(root, join(root, name)))) !== files[name]) throw new Error('Audit vendor checksum mismatch.');
  }
}

function validation() {
  verifyVendor();
  if (!validators) {
    const schema = JSON.parse(readBounded(join(vendorDirectory, 'report-schema.json'))) as unknown;
    validators = { findings: { validateDocument: value => findingsValidator.validateDocument(value, schema) },
      coverage: coverageValidator };
  }
  return validators;
}

function readJson(path: string): unknown {
  const value = readBounded(path);
  validation().coverage.preflightJsonText(value);
  return JSON.parse(value) as unknown;
}

function write(path: string, value: string): void {
  if (Buffer.byteLength(value) > maximumBytes) throw new Error('Audit output exceeds its bound.');
  const existing = lstatSync(path, { throwIfNoEntry: false });
  if (existing && (!existing.isFile() || existing.nlink !== 1)) throw new Error('Audit output cannot replace a linked or special file.');
  const temporary = join(dirname(path), `.audit-${randomUUID()}.tmp`);
  writeFileSync(temporary, value, { flag: 'wx', mode: 0o600 });
  try { renameSync(temporary, path); } finally { if (existsSync(temporary)) unlinkSync(temporary); }
}
function writeJson(path: string, value: unknown) { write(path, JSON.stringify(value, null, 2) + '\n'); }

function runPath(run: string, root: string): string {
  const target = realpathSync(root);
  const parent = confined(target, join(target, 'artifacts/security-audit'));
  const full = confined(target, resolve(run));
  if (dirname(full) !== parent || !lstatSync(full).isDirectory()) throw new Error('Run must be a direct audit output directory.');
  if (git(target, ['check-ignore', '--no-index', '--', relative(target, full) + '/run-metadata.json']).trim() === '')
    throw new Error('Audit output must be ignored by Git.');
  return full;
}

export function initAudit(root = repoRoot, budget = 16): string {
  if (!Number.isInteger(budget) || budget < 7 || budget > 16) throw new Error('Quick audit requires a budget between 7 and 16 agent invocations.');
  validation();
  root = realpathSync(root);
  const source = captureCandidate(root);
  const parent = confined(root, join(root, 'artifacts/security-audit'));
  if (!git(root, ['check-ignore', '--no-index', '--', 'artifacts/security-audit/probe']).trim()) throw new Error('Audit output must be ignored.');
  mkdirSync(parent, { recursive: true, mode: 0o700 });
  const run = mkdtempSync(join(parent, new Date().toISOString().replaceAll(':', '-') + '-'));
  mkdirSync(join(run, 'agents'), { mode: 0o700 });
  const metadata: AuditMetadata = {
    version: 1, run_id: run.split('/').pop()!, repo: 'MyAIAgent', target: root, source_ref: source, profile: 'quick',
    scope_paths: ['src', 'scripts', 'infra', '.github', '.agents/docs', '.agents/rules', '.agents/skills', '.codex', '.claude', 'AGENTS.md', 'CLAUDE.md'],
    budget, execution_policy: 'source-only', run_status: 'in_progress', incomplete_reason: null, agents: [], candidates: [],
    selected_companions: ['AI-AND-LLM.md', 'WEB-PROTOCOL-AND-AUTH.md', 'SUPPLY-CHAIN-AND-RELEASE.md', 'CLOUD-AND-DEPLOYMENT.md',
      'PROTOCOLS-RPC-AND-MESSAGING.md', 'RESOURCE-EXHAUSTION-AND-AVAILABILITY.md', 'DATA-ISOLATION-AND-LIFECYCLE.md'],
    prior_runs: readdirSync(parent).filter(name => name !== run.split('/').pop()).sort(),
  };
  writeJson(join(run, 'run-metadata.json'), metadata);
  writeJson(join(run, 'coverage-ledger.json'), []);
  writeJson(join(run, 'findings.json'), []);
  write(join(run, 'architecture.md'), '# Source audit architecture\n\nReconnaissance is pending; no coverage is claimed.\n');
  return run;
}

export function metadataFor(run: string, root = repoRoot): AuditMetadata {
  run = runPath(run, root);
  const data = object(readJson(join(run, 'run-metadata.json')));
  if (data.version !== 1 || data.target !== realpathSync(root) || data.profile !== 'quick' || data.execution_policy !== 'source-only'
    || !['in_progress', 'complete', 'incomplete'].includes(String(data.run_status))
    || !Number.isInteger(data.budget) || Number(data.budget) < 7 || Number(data.budget) > 16
    || !Array.isArray(data.agents) || !Array.isArray(data.candidates)) throw new Error('Invalid audit metadata.');
  object(data.source_ref);
  const ids = new Set<string>(); const native = new Set<string>();
  for (const value of data.agents) {
    const agent = object(value);
    if (!safeId(agent.id) || ids.has(agent.id) || !roles.includes(agent.role as Role)
      || !['reserved', 'complete'].includes(String(agent.status))
      || (agent.status === 'complete' && (!text(agent.native_id) || native.has(agent.native_id)))) throw new Error('Invalid or reused agent identity.');
    ids.add(agent.id);
    if (text(agent.native_id)) native.add(agent.native_id);
  }
  for (const value of data.candidates) {
    const candidate = object(value);
    if (!text(candidate.fingerprint) || !/^[A-Za-z0-9][A-Za-z0-9._:/@+-]*$/.test(candidate.fingerprint) || !safeId(candidate.finder_id))
      throw new Error('Invalid candidate registry.');
  }
  return data as unknown as AuditMetadata;
}

export function reserveAgent(run: string, role: Role, id: string, root = repoRoot): void {
  const meta = metadataFor(run, root);
  assertCurrent(meta.source_ref, root);
  if (meta.run_status !== 'in_progress' || !roles.includes(role) || !safeId(id) || meta.agents.some(agent => agent.id === id)) throw new Error('Invalid agent reservation.');
  const verifierReserve = Math.max(1, Math.floor((meta.budget - 5) / 2));
  const quota = { reconnaissance: 4, hunter: meta.budget - 5 - verifierReserve, critic: 1, verifier: verifierReserve };
  const completed = (expected: Role) => meta.agents.filter(agent => agent.role === expected && agent.status === 'complete').length;
  if ((role !== 'reconnaissance' && completed('reconnaissance') !== 4)
    || (role === 'reconnaissance' && meta.agents.some(agent => agent.role !== 'reconnaissance'))
    || (role === 'hunter' && meta.agents.some(agent => ['critic', 'verifier'].includes(agent.role)))
    || (role === 'critic' && (completed('hunter') < 1 || meta.agents.some(agent => agent.status !== 'complete')))
    || (role === 'verifier' && completed('critic') !== 1)) throw new Error('Audit phase order requires reconnaissance, hunters, a final critic, then verifiers.');
  if (meta.agents.length >= meta.budget || meta.agents.filter(agent => agent.status === 'reserved').length >= 2
    || meta.agents.filter(agent => agent.role === role).length >= quota[role]) throw new Error('Audit agent budget or concurrency limit reached.');
  if (role === 'critic') {
    const current = evaluateAudit(run, root, false);
    if (current.reasons.some(reason => !['required_agents_incomplete', 'candidate_validation_incomplete'].includes(reason))
      || current.findings.length > 0) throw new Error('Critic requires finalized architecture, coverage, and registered hunter evidence.');
    const input = JSON.stringify(criticInput(run, meta, root), null, 2) + '\n';
    write(confined(realpathSync(root), join(run, 'critic-input.json')), input);
    meta.critic_input_digest = hash(input);
  }
  meta.agents.push({ id, role, native_id: null, status: 'reserved' });
  writeJson(join(run, 'run-metadata.json'), meta);
}

export function finishAgent(run: string, id: string, nativeId: string, result: unknown, root = repoRoot): void {
  const meta = metadataFor(run, root);
  assertCurrent(meta.source_ref, root);
  const agent = meta.agents.find(agent => agent.id === id);
  if (meta.run_status !== 'in_progress' || !agent || agent.status !== 'reserved' || !text(nativeId)
    || meta.agents.some(agent => agent.native_id === nativeId)) throw new Error('Invalid native agent completion.');
  const output = validateResult(agent.role, result);
  let finalLedger: ObjectData[] | undefined;
  if (agent.role === 'critic') {
    const input = readCriticInput(run, meta, root);
    if (output.input_digest !== meta.critic_input_digest || !same(input, criticInput(run, meta, root)))
      throw new Error('Critic result does not match its frozen input snapshot.');
    finalLedger = reconciledLedger(input, output);
  }
  const directory = confined(realpathSync(root), join(run, 'agents', id));
  mkdirSync(directory, { mode: 0o700 });
  writeJson(join(directory, 'result.json'), result);
  if (finalLedger) writeJson(join(run, 'coverage-ledger.json'), finalLedger);
  agent.native_id = nativeId; agent.status = 'complete';
  writeJson(join(run, 'run-metadata.json'), meta);
}

export function registerCandidate(run: string, fingerprint: string, finderId: string, root = repoRoot): void {
  const meta = metadataFor(run, root);
  assertCurrent(meta.source_ref, root);
  if (meta.agents.some(agent => agent.role === 'critic')) throw new Error('Candidate registry is frozen for the critic.');
  const finder = meta.agents.find(agent => agent.id === finderId && agent.status === 'complete' && agent.role === 'hunter');
  if (!finder || !/^[A-Za-z0-9][A-Za-z0-9._:/@+-]*$/.test(fingerprint) || meta.candidates.some(candidate => candidate.fingerprint === fingerprint)) throw new Error('Invalid or duplicate audit candidate.');
  const result = object(readJson(confined(realpathSync(root), join(run, 'agents', finderId, 'result.json'))));
  if (!Array.isArray(result.candidates) || !result.candidates.some(value => object(value).fingerprint === fingerprint)) throw new Error('Candidate was not returned by its finder.');
  meta.candidates.push({ fingerprint, finder_id: finderId });
  writeJson(join(run, 'run-metadata.json'), meta);
}

export function writeAuditFile(run: string, name: 'architecture.md' | 'coverage-ledger.json' | 'findings.json', value: unknown, root = repoRoot): void {
  const meta = metadataFor(run, root);
  assertCurrent(meta.source_ref, root);
  if (meta.run_status !== 'in_progress' || !['architecture.md', 'coverage-ledger.json', 'findings.json'].includes(name)) throw new Error('Audit run is not writable.');
  const critic = meta.agents.find(agent => agent.role === 'critic');
  if (critic && (critic.status !== 'complete' || name !== 'findings.json')) throw new Error('Critic evidence is frozen; only verified findings may follow reconciliation.');
  const full = confined(realpathSync(root), join(run, name));
  if (name === 'architecture.md') { if (typeof value !== 'string') throw new Error('Expected architecture text.'); write(full, value); }
  else {
    const errors = (name === 'findings.json' ? validation().findings : validation().coverage).validateDocument(value);
    if (errors.length) throw new Error(`Invalid ${name}: ${errors.length} validation errors.`);
    writeJson(full, value);
  }
}

export function canonicalCoverageId(refs: ObjectData): string { return validation().coverage.canonicalCoverageId(refs); }

function evaluateAudit(run: string, root: string, checkReports: boolean) {
  const meta = metadataFor(run, root);
  const reasons: string[] = [];
  try { assertCurrent(meta.source_ref, root); } catch { reasons.push('source_changed'); }
  const ledgerValue = readJson(confined(realpathSync(root), join(run, 'coverage-ledger.json')));
  const findingsValue = readJson(confined(realpathSync(root), join(run, 'findings.json')));
  if (validation().coverage.validateDocument(ledgerValue).length || validation().findings.validateDocument(findingsValue).length)
    throw new Error('Invalid findings or coverage ledger.');
  const ledger = (ledgerValue as unknown[]).map(object);
  const findings = (findingsValue as unknown[]).map(object);
  const resultFor = (id: string) => object(readJson(confined(realpathSync(root), join(run, 'agents', id, 'result.json'))));
  const agents = meta.agents.filter(agent => agent.status === 'complete');
  for (const agent of agents) {
    try { validateResult(agent.role, resultFor(agent.id)); } catch { reasons.push('invalid_agent_result'); }
  }
  const critic = agents.find(agent => agent.role === 'critic');
  if (critic) {
    try {
      const input = readCriticInput(run, meta, root);
      const current = criticInput(run, meta, root);
      const output = validateResult('critic', resultFor(critic.id));
      if (output.input_digest !== meta.critic_input_digest || !same(input.metadata, current.metadata)
        || input.architecture !== current.architecture || !same(input.results, current.results)
        || !same(ledger, reconciledLedger(input, output))) reasons.push('critic_evidence_changed');
    } catch { reasons.push('critic_evidence_changed'); }
  }
  const phase = (role: Role) => roles.indexOf(role);
  if (meta.agents.some((agent, index) => index > 0 && phase(agent.role) < phase(meta.agents[index - 1].role))) reasons.push('invalid_phase_order');
  const architecturePath = confined(realpathSync(root), join(run, 'architecture.md'));
  if (!existsSync(architecturePath) || readBounded(architecturePath).trim().length < 80
    || readBounded(architecturePath).includes('Reconnaissance is pending; no coverage is claimed.')) reasons.push('architecture_incomplete');
  for (const hunter of agents.filter(agent => agent.role === 'hunter')) {
    const returned = resultFor(hunter.id).candidates;
    if (!Array.isArray(returned) || returned.some(value => !meta.candidates.some(candidate => candidate.fingerprint === object(value).fingerprint)))
      reasons.push('unregistered_hunter_candidate');
  }
  const count = (role: Role) => agents.filter(agent => agent.role === role).length;
  const reserve = Math.max(1, Math.floor((meta.budget - 5) / 2));
  if (meta.agents.length > meta.budget || count('reconnaissance') > 4 || count('hunter') > meta.budget - 5 - reserve || count('critic') > 1 || count('verifier') > reserve)
    reasons.push('agent_budget_exceeded');
  if (meta.agents.some(agent => agent.status !== 'complete') || count('reconnaissance') !== 4 || count('hunter') < 1 || count('critic') !== 1)
    reasons.push('required_agents_incomplete');
  if (ledger.length === 0 || ledger.some(unit => ['planned', 'in_progress'].includes(String(unit.status)))) reasons.push('coverage_not_finalized');
  const assignments = ledger.flatMap<ObjectData>(unit => [...(unit.attempts as ObjectData[]).map(attempt => ({ ...attempt, coverage_id: unit.coverage_id })), unit]);
  if (!assignments.some(unit => (unit.local_checks as ObjectData[]).length > 0)) reasons.push('coverage_not_started');
  for (const unit of assignments) {
    if (unit.agent_id !== null && !agents.some(agent => agent.id === unit.agent_id && ['hunter', 'verifier'].includes(agent.role))) reasons.push('unregistered_coverage_owner');
    for (const check of unit.local_checks as ObjectData[]) {
      if (check.method !== 'source' || check.artifact !== null) reasons.push('target_execution_not_permitted');
      if (!agents.some(agent => agent.id === check.agent_id && ['hunter', 'verifier'].includes(agent.role))) reasons.push('unregistered_coverage_owner');
      else {
        const output = resultFor(String(check.agent_id));
        const matches = Array.isArray(output.units) ? output.units.map(object).filter(value => value.coverage_id === unit.coverage_id) : [];
        const evidence = matches.length === 1 ? matches[0] : {};
        if (!Array.isArray(evidence.local_checks) || !evidence.local_checks.some(value => JSON.stringify(value) === JSON.stringify(check))
          || !Array.isArray(evidence.reviewed_paths) || !(check.reviewed_paths as string[]).every(file => (evidence.reviewed_paths as string[]).includes(file)))
          reasons.push('coverage_evidence_mismatch');
      }
    }
    for (const fingerprint of unit.result_fingerprints as string[])
      if (!meta.candidates.some(candidate => candidate.fingerprint === fingerprint)) reasons.push('unregistered_candidate');
  }
  const candidateIds = new Set(meta.candidates.map(candidate => candidate.fingerprint));
  if (candidateIds.size !== meta.candidates.length) reasons.push('duplicate_candidate');
  const pending: string[] = [];
  for (const candidate of meta.candidates) {
    const finder = agents.find(agent => agent.id === candidate.finder_id && agent.role === 'hunter');
    const record = findings.find(record => record.fingerprint === candidate.fingerprint);
    const verifiers = agents.filter(agent => agent.role === 'verifier').filter(agent => {
      const output = resultFor(agent.id);
      return output.fingerprint === candidate.fingerprint && output.decision === object(output.record).verdict
        && JSON.stringify(output.record) === JSON.stringify(record);
    });
    if (!finder || !Array.isArray(resultFor(finder.id).candidates)
      || !(resultFor(finder.id).candidates as ObjectData[]).some(value => value.fingerprint === candidate.fingerprint)) reasons.push('invalid_finder');
    if (!record || verifiers.length !== 1 || verifiers[0].native_id === finder?.native_id) {
      if (record) throw new Error('Finding has no independent matching verifier record.');
      pending.push(candidate.fingerprint);
    }
    if (!assignments.some(unit => (unit.result_fingerprints as string[]).includes(candidate.fingerprint)
      && (unit.local_checks as ObjectData[]).some(check => check.agent_id === candidate.finder_id))) reasons.push('candidate_missing_coverage');
  }
  if (pending.length) reasons.push('candidate_validation_incomplete');
  if (findings.some(finding => !candidateIds.has(String(finding.fingerprint)))) reasons.push('finding_missing_candidate');
  if (!reasons.includes('source_changed')) {
    const inventory = new Set(git(root, ['ls-files', '--cached', '--others', '--exclude-standard', '-z']).split('\0').filter(Boolean));
    const checkSource = (file: unknown, line?: unknown) => {
      if (typeof file !== 'string' || !inventory.has(file) || file.startsWith('.agents/vendor/')) throw new Error('Audit source reference is outside maintained source.');
      const contents = readBounded(confined(realpathSync(root), join(root, file)));
      if (line !== undefined && (!Number.isInteger(line) || Number(line) < 1 || Number(line) > contents.split('\n').length)) throw new Error('Invalid audit source line.');
    };
    for (const unit of assignments) for (const file of unit.reviewed_paths as string[]) checkSource(file);
    for (const finding of findings) for (const entry of [...finding.trace as ObjectData[], ...finding.evidence as ObjectData[]]) checkSource(entry.file, entry.line);
    for (const agent of agents.filter(agent => agent.role === 'reconnaissance')) {
      const facts = resultFor(agent.id).facts;
      if (Array.isArray(facts)) for (const fact of facts) { const entry = object(fact); checkSource(entry.file, entry.line); }
    }
  }
  if (checkReports && (meta.run_status !== 'in_progress' || ['REPORT.md', 'FINDINGS-DETAIL.md', 'NEEDS-VALIDATION.md']
    .some(name => existsSync(confined(realpathSync(root), join(run, name)))))) {
    const expectedStatus = reasons.length === 0 ? 'complete' : 'incomplete';
    const expectedReason = reasons.length ? [...new Set(reasons)].join(',') : null;
    if (meta.run_status !== expectedStatus || meta.incomplete_reason !== expectedReason) reasons.push('report_status_changed');
    if (meta.report_inputs_digest !== reportInputsDigest(run, meta, root)) reasons.push('report_inputs_changed');
    for (const name of ['REPORT.md', 'FINDINGS-DETAIL.md', 'NEEDS-VALIDATION.md']) {
      const path = confined(realpathSync(root), join(run, name));
      if (!existsSync(path) || meta.reports?.[name] !== hash(readBounded(path))) reasons.push('report_content_changed');
    }
  }
  return { meta, ledger, findings, reasons: [...new Set(reasons)], pending, complete: reasons.length === 0 };
}

export function checkAudit(run: string, root = repoRoot) { return evaluateAudit(run, root, true); }

const markdown = (value: unknown) => String(value).replace(/[\p{Cc}\p{Cf}]/gu, ' ').replace(/[&<>]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[character]!).replace(/([\\`*_[\]{}|#])/g, '\\$1');
const jsonBlock = (value: unknown) => {
  const content = JSON.stringify(value, null, 2);
  const fence = '`'.repeat(Math.max(3, ...[...content.matchAll(/`+/g)].map(match => match[0].length + 1)));
  return `${fence}json\n${content}\n${fence}\n`;
};

export function reportAudit(run: string, root = repoRoot) {
  const checked = evaluateAudit(run, root, false);
  const { meta, findings, ledger, reasons, pending } = checked;
  meta.run_status = checked.complete ? 'complete' : 'incomplete';
  meta.incomplete_reason = reasons.length ? reasons.join(',') : null;
  const resultFor = (agent: AgentCall) => object(readJson(confined(realpathSync(root), join(run, 'agents', agent.id, 'result.json'))));
  const returned = meta.agents.filter(agent => agent.status === 'complete');
  const notes = (field: string) => returned.filter(agent => agent.role === 'hunter').flatMap(agent => {
    const value = resultFor(agent)[field]; return Array.isArray(value) ? value.map(markdown) : [];
  }).map(value => `- ${value}`).join('\n') || 'None recorded.';
  const pendingUnits = pending.map(fingerprint => ({ fingerprint, units: ledger.filter(unit => [unit, ...unit.attempts as ObjectData[]].some(attempt => (attempt.result_fingerprints as string[]).includes(fingerprint))).map(unit => unit.coverage_id) }));
  const confirmed = findings.filter(record => record.verdict === 'confirmed');
  const needsValidation = findings.filter(record => record.verdict === 'needs_validation');
  const traceSummary = (record: ObjectData) => (record.trace as ObjectData[]).map(entry => `${entry.file}:${entry.line}`).join(' → ');
  const overview = `# Security audit report\n\n**Partial source-only quick audit.** Workflow status: **${meta.run_status}**. This is not a security clearance.\n\n`
    + (pending.length ? `**Candidate validation is incomplete:** ${markdown(JSON.stringify(pendingUnits))}.\n\n` : '')
    + `Source: ${markdown(meta.source_ref.head)}; worktree digest: ${markdown(meta.source_ref.digest)}.\n\n`
    + `Scope: ${meta.scope_paths.map(markdown).join(', ')}. See [architecture](architecture.md) and [coverage ledger](coverage-ledger.json) for boundaries and source checks.\n\n`
    + `Prior run directories: ${meta.prior_runs.length ? meta.prior_runs.map(markdown).join(', ') + '. Actual compatible inputs and any carried/revalidated root causes are recorded in architecture.md.' : 'None; no prior audit ledger was available.'}\n\n`
    + `Agent invocations: ${meta.agents.length}/${meta.budget}. Recorded policy: source-only; application execution and live service access are prohibited.\n\n`
    + `Confirmed: ${findings.filter(record => record.verdict === 'confirmed').length}; needs validation: ${findings.filter(record => record.verdict === 'needs_validation').length}; rejected: ${findings.filter(record => record.verdict === 'rejected').length}.\n\n`
    + `Coverage units: ${ledger.length}; covered: ${ledger.filter(unit => unit.status === 'covered').length}; candidate: ${ledger.filter(unit => unit.status === 'candidate').length}; deferred: ${ledger.filter(unit => unit.status === 'deferred').length}; blocked: ${ledger.filter(unit => unit.status === 'blocked').length}; out of scope: ${ledger.filter(unit => unit.status === 'out_of_scope').length}.\n\n`
    + `Incompleteness: ${reasons.length ? reasons.map(markdown).join(', ') : 'None recorded; quick coverage remains partial.'}\n\n`
    + `## Confirmed findings\n\n${confirmed.length ? '| Severity | Title | Boundary trace | Observed result |\n| --- | --- | --- | --- |\n' + confirmed.map(record => `| ${markdown(object(record.severity).overall_severity)} | ${markdown(record.title)} | ${markdown(traceSummary(record))} | ${markdown(object(record.execution).observed_result)} |`).join('\n') : 'No confirmed findings. This does not establish absence of vulnerabilities.'}\n\n`
    + confirmed.map(record => `### ${markdown(record.title)}\n\n${jsonBlock(record)}`).join('\n')
    + `## Needs validation\n\n${needsValidation.length ? '| Lead | Source trace | Exact blockers | Safe next steps |\n| --- | --- | --- | --- |\n' + needsValidation.map(record => `| ${markdown(record.title)} | ${markdown(traceSummary(record))} | ${markdown(JSON.stringify(record.blockers))} | ${markdown(JSON.stringify(record.validation_plan))} |`).join('\n') : 'None independently recorded.'}\n\n`
    + `## Positive source patterns\n\n${notes('positive_patterns')}\n\n## Hardening notes\n\n${notes('hardening')}\n\n`
    + `## Final coverage critic\n\n${returned.filter(agent => agent.role === 'critic').map(agent => jsonBlock(resultFor(agent))).join('\n') || 'The required final critic did not complete.'}\n\n`
    + `## Coverage gaps\n\n${ledger.filter(unit => ['deferred', 'blocked', 'out_of_scope'].includes(String(unit.status))).map(unit => `- ${markdown(unit.coverage_id)}: ${markdown(JSON.stringify(unit.unresolved))}`).join('\n') || 'No gap units recorded; a quick pass still does not exhaust the target.'}\n`;
  write(confined(realpathSync(root), join(run, 'REPORT.md')), overview);
  const detailed = confirmed.filter(record => ['medium', 'high', 'critical'].includes(String(object(record.severity).overall_severity)));
  write(confined(realpathSync(root), join(run, 'FINDINGS-DETAIL.md')), '# Confirmed findings: medium and higher\n\nDerived from independently checked findings.json; source-only evidence has the limits stated in each record. Rejected records remain only in findings.json; unresolved records appear in NEEDS-VALIDATION.md.\n\n' + (detailed.map(jsonBlock).join('\n') || 'No confirmed medium-or-higher findings recorded.\n'));
  write(confined(realpathSync(root), join(run, 'NEEDS-VALIDATION.md')), '# Validation still needed\n\nRuntime or deployment-dependent claims were not exercised.\n\n' + needsValidation.map(jsonBlock).join('\n') + '\nUnvalidated candidates (not finding records):\n\n' + (pendingUnits.length ? jsonBlock(pendingUnits) : 'None recorded.') + '\n');
  meta.reports = Object.fromEntries(['REPORT.md', 'FINDINGS-DETAIL.md', 'NEEDS-VALIDATION.md'].map(name => [name, hash(readBounded(join(run, name)))]));
  meta.report_inputs_digest = reportInputsDigest(run, meta, root);
  writeJson(join(run, 'run-metadata.json'), meta);
  return checked;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const [operation, run, extra] = process.argv.slice(2);
    if (operation === 'init' && (!run || run === '--max-agents') && (run ? extra !== undefined && process.argv.length === 5 : !extra))
      console.log(initAudit(repoRoot, extra ? Number(extra) : 16));
    else if ((operation === 'check' || operation === 'report') && run && !extra) {
      const result = operation === 'check' ? checkAudit(run) : reportAudit(run);
      console.log(JSON.stringify({ workflow: result.complete ? 'complete' : 'incomplete', profile: 'quick', coverage: 'partial', reasons: result.reasons, pending: result.pending }, null, 2));
      process.exitCode = result.complete ? 0 : 2;
    } else throw new Error('Usage: npm run security:audit -- init [--max-agents 7..16] | check <run> | report <run>');
  } catch (error) { console.error(`Audit could not complete: ${error instanceof Error ? error.message : 'Invalid evidence.'}`); process.exitCode = 1; }
}
