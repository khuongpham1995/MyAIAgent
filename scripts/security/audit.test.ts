/**
 * MyAIAgent | Security Tooling · Tests
 *
 * Exercises audit provenance, source binding, budgets, independent finding records, and honest partial reports with local fixtures.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { cpSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { canonicalCoverageId, checkAudit, finishAgent, initAudit, metadataFor, registerCandidate, reportAudit, reserveAgent, vendorDirectory, verifyVendor, writeAuditFile } from './audit.ts';

function fixture() {
  const root = realpathSync(mkdtempSync(join(tmpdir(), 'myaiagent-audit-test-')));
  const git = (args: string[]) => {
    const result = spawnSync('git', args, { cwd: root, encoding: 'utf8', env: { ...process.env, GIT_CONFIG_GLOBAL: '/dev/null', GIT_CONFIG_SYSTEM: '/dev/null' } });
    assert.equal(result.status, 0, result.stderr);
  };
  writeFileSync(join(root, '.gitignore'), 'artifacts/\n');
  writeFileSync(join(root, 'source.txt'), 'fixture source\n');
  git(['init', '-q']); git(['add', '--', '.gitignore', 'source.txt']);
  git(['-c', 'user.name=Audit Test', '-c', 'user.email=audit@example.invalid', 'commit', '-qm', 'fixture']);
  return { root, run: initAudit(root), cleanup: () => rmSync(root, { recursive: true, force: true }) };
}

function unit(owner: string, fingerprints: string[] = []) {
  const refs = { surface: 'source.txt#input', boundary: 'source.txt#owner', subsystem: 'profile/quick/all-in-scope-subsystems', attack_class: 'ATTACK-CLASSES.md#Access control' };
  return {
    coverage_id: canonicalCoverageId(refs), canonical_refs: refs, surface: 'Fixture input', boundary: 'Ownership check', subsystem: 'All in-scope subsystems',
    attack_class: 'Access control', starting_paths: ['source.txt'], ordinary_attack_class_block: 'ATTACK-CLASSES.md#Access control', selected_companion_blocks: [], excluded_blocks: [],
    prior_status: 'none', attempts: [], wave: 1, status: fingerprints.length ? 'candidate' : 'covered', agent_id: owner,
    reviewed_paths: ['source.txt'], local_checks: [{ agent_id: owner, reviewed_paths: ['source.txt'], invariant: 'Only an owner accesses a fixture resource.', method: 'source', result: 'Inspected the source-visible ownership path.', artifact: null }], result_fingerprints: fingerprints, unresolved: [],
  };
}

function record() {
  return { verdict: 'needs_validation', fingerprint: 'fixture-owner-boundary', title: 'Fixture ownership behavior needs runtime evidence',
    description: 'The source trace needs one runtime fact.', claimed_root_cause: 'The generated framework behavior is not established by source inspection.',
    trace: [{ kind: 'entrypoint', file: 'source.txt', line: 1, scope: 'fixture', description: 'Input enters the fixture.' }],
    evidence: [{ file: 'source.txt', line: 1, description: 'The source establishes the input boundary.' }], blockers: ['Runtime behavior was not executed in this source-only audit.'],
    validation_plan: { local: 'Use an isolated offline fixture with two dummy owners.', deployment: 'No live probing is proposed.' } };
}

function reconnaissance(run: string, root: string) {
  for (let i = 0; i < 4; i++) {
    reserveAgent(run, 'reconnaissance', `recon_${i}`, root);
    finishAgent(run, `recon_${i}`, `native/recon/${i}`, { facts: [{ topic: 'fixture', description: 'Source inspection only.', file: 'source.txt', line: 1 }], starting_paths: ['source.txt'], limits: [] }, root);
  }
  writeAuditFile(run, 'architecture.md', '# Fixture architecture\n\nThe fixture models a source input crossing an ownership boundary. This is a partial source-only inspection with no prior audit evidence.\n', root);
}

function hunterResult(candidates: unknown[] = [], owner = 'hunter') {
  const { coverage_id, reviewed_paths, local_checks } = unit(owner);
  return { candidates, coverage_ids: [coverage_id], reviewed_paths, local_checks, units: [{ coverage_id, reviewed_paths, local_checks }] };
}

function prepare(run: string, root: string, fingerprints: string[] = []) {
  reconnaissance(run, root);
  reserveAgent(run, 'hunter', 'hunter', root);
  finishAgent(run, 'hunter', 'native/hunter', hunterResult(fingerprints.map(fingerprint => ({ fingerprint }))), root);
  for (const fingerprint of fingerprints) registerCandidate(run, fingerprint, 'hunter', root);
  writeAuditFile(run, 'coverage-ledger.json', [unit('hunter', fingerprints)], root);
}

function criticResult(run: string, root: string, changes: Record<string, unknown> = {}) {
  return { input_digest: metadataFor(run, root).critic_input_digest, missing_units: [], reassign_ids: [], resolved_prior_leads: [], stop: true, ...changes };
}

function completeCritic(run: string, root: string, changes: Record<string, unknown> = {}) {
  reserveAgent(run, 'critic', 'critic', root);
  finishAgent(run, 'critic', 'native/critic', criticResult(run, root, changes), root);
}

function baseline(run: string, root: string) {
  prepare(run, root);
  completeCritic(run, root);
}

function gap() {
  const base = unit('hunter');
  const canonical_refs = { ...base.canonical_refs, surface: 'source.txt#additional-input' };
  return { surface: 'Additional fixture input', boundary: base.boundary, subsystem: base.subsystem,
    attack_class: base.attack_class, canonical_refs, ordinary_attack_class_block: base.ordinary_attack_class_block,
    starting_paths: ['source.txt'], selected_companion_blocks: [], excluded_blocks: [], reason: 'source.txt:1 needs a separate boundary check.' };
}

test('TypeScript ports retain every upstream validator test case', () => {
  // Arrange
  const cases = (path: string) => [...readFileSync(path, 'utf8').matchAll(/\btest\("([^"\n]+)"/g)].map(match => match[1]);

  // Act & Assert
  verifyVendor();
  for (const name of ['validate-findings', 'validate-coverage-ledger']) {
    assert.deepEqual(cases(join(vendorDirectory, name + '.test.cjs')), cases(join(vendorDirectory, '../../../scripts/security', name + '.test.ts')));
  }
});

test('vendor provenance rejects changes and links in the retained upstream snapshot', () => {
  // Arrange
  const directory = realpathSync(mkdtempSync(join(tmpdir(), 'audit-vendor-test-')));
  const target = join(directory, 'vendor'); cpSync(vendorDirectory, target, { recursive: true });

  // Act & Assert
  try {
    writeFileSync(join(target, 'validate-findings.cjs'), '// changed');
    assert.throws(() => verifyVendor(target), /checksum/);
    const manifest = JSON.parse(readFileSync(join(target, 'source.json'), 'utf8'));
    manifest.files['validate-findings.cjs'] = createHash('sha256').update('// changed').digest('hex');
    writeFileSync(join(target, 'source.json'), JSON.stringify(manifest));
    assert.throws(() => verifyVendor(target), /pinned revision/);
    writeFileSync(join(target, 'source.json'), readFileSync(join(vendorDirectory, 'source.json')));
    rmSync(join(target, 'validate-findings.cjs')); symlinkSync(join(vendorDirectory, 'validate-findings.cjs'), join(target, 'validate-findings.cjs'));
    assert.throws(() => verifyVendor(target), /links|ELOOP/);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('initial evidence cannot claim completion or clean security', () => {
  // Arrange
  const f = fixture();

  // Act
  try {
    const result = reportAudit(f.run, f.root);

    // Assert
    assert.equal(result.complete, false);
    assert.equal(metadataFor(f.run, f.root).run_status, 'incomplete');
    const report = readFileSync(join(f.run, 'REPORT.md'), 'utf8');
    assert.match(report, /Partial source-only quick audit/);
    assert.match(report, /required\\_agents\\_incomplete/);
    assert.match(report, /not a security clearance/);
  } finally { f.cleanup(); }
});

test('a complete workflow is partial coverage and reports remain bound to content', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);

  // Act & Assert
  try {
    assert.equal(reportAudit(f.run, f.root).complete, true);
    assert.equal(checkAudit(f.run, f.root).complete, true);
    const report = readFileSync(join(f.run, 'REPORT.md'), 'utf8');
    assert.match(report, /Scope: src, tests,/);
    assert.match(report, /Directory\.Build\.props/);
    assert.match(report, /Directory\.Packages\.props/);
    assert.match(report, /no prior audit ledger/);
    assert.match(report, /covered: 1; candidate: 0/);
    assert.match(report, /Final coverage critic/);
    writeFileSync(join(f.run, 'REPORT.md'), 'All code is secure.');
    assert.ok(checkAudit(f.run, f.root).reasons.includes('report_content_changed'));
  } finally { f.cleanup(); }
});

test('source edits invalidate the audit and the rendered workflow status', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);

  // Act
  try {
    writeFileSync(join(f.root, 'source.txt'), 'changed source');
    const result = reportAudit(f.run, f.root);

    // Assert
    assert.equal(result.complete, false);
    assert.ok(result.reasons.includes('source_changed'));
    assert.equal(metadataFor(f.run, f.root).run_status, 'incomplete');
  } finally { f.cleanup(); }
});

test('budgets reserve critics and verifiers and limit concurrent calls', () => {
  // Arrange
  const f = fixture();

  // Act & Assert
  try {
    assert.throws(() => initAudit(f.root, 6), /budget/);
    reconnaissance(f.run, f.root);
    reserveAgent(f.run, 'hunter', 'one', f.root); reserveAgent(f.run, 'hunter', 'two', f.root);
    assert.throws(() => reserveAgent(f.run, 'hunter', 'three', f.root), /concurrency/);
    finishAgent(f.run, 'one', 'native/one', hunterResult([], 'one'), f.root); finishAgent(f.run, 'two', 'native/two', hunterResult([], 'two'), f.root);
    for (let i = 3; i <= 6; i++) { reserveAgent(f.run, 'hunter', `hunter_${i}`, f.root); finishAgent(f.run, `hunter_${i}`, `native/${i}`, hunterResult([], `hunter_${i}`), f.root); }
    assert.throws(() => reserveAgent(f.run, 'hunter', 'seventh', f.root), /budget/);
    writeAuditFile(f.run, 'coverage-ledger.json', [unit('one')], f.root);
    reserveAgent(f.run, 'critic', 'critic', f.root);
    assert.throws(() => finishAgent(f.run, 'critic', 'native/one', {}, f.root), /identity|completion/);
  } finally { f.cleanup(); }
});

test('phase reservations and final validation reject an early coverage critic', () => {
  // Arrange
  const f = fixture();

  // Act & Assert
  try {
    assert.throws(() => reserveAgent(f.run, 'hunter', 'early_hunter', f.root), /phase order/);
    reconnaissance(f.run, f.root);
    assert.throws(() => reserveAgent(f.run, 'critic', 'early_critic', f.root), /phase order/);
    reserveAgent(f.run, 'hunter', 'hunter', f.root); finishAgent(f.run, 'hunter', 'native/hunter', hunterResult(), f.root);
    assert.throws(() => reserveAgent(f.run, 'critic', 'critic', f.root), /finalized/);
    writeAuditFile(f.run, 'coverage-ledger.json', [unit('hunter')], f.root);
    completeCritic(f.run, f.root);
    assert.throws(() => reserveAgent(f.run, 'hunter', 'late_hunter', f.root), /phase order/);
    const meta = metadataFor(f.run, f.root); [meta.agents[4], meta.agents[5]] = [meta.agents[5], meta.agents[4]];
    writeFileSync(join(f.run, 'run-metadata.json'), JSON.stringify(meta));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('invalid_phase_order'));
  } finally { f.cleanup(); }
});

test('completion requires architecture and actual returned source checks', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);

  // Act & Assert
  try {
    rmSync(join(f.run, 'architecture.md'));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('architecture_incomplete'));
    writeFileSync(join(f.run, 'architecture.md'), '# Architecture\n\nThe fixture source crosses an ownership boundary. This source-only pass has no previous audit evidence and does not execute applications.\n');
    const deferred = { ...unit('hunter'), status: 'deferred', agent_id: null, reviewed_paths: [], local_checks: [], unresolved: ['Budget deferred.'] };
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify([deferred]));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('coverage_not_started'));
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify([unit('hunter')]));
    writeFileSync(join(f.run, 'agents/hunter/result.json'), JSON.stringify({ candidates: [] }));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('coverage_evidence_mismatch'));
  } finally { f.cleanup(); }
});

test('archived attempts retain source-only and identity checks', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root);
  const original = unit('hunter');
  completeCritic(f.run, f.root, { stop: false, reassign_ids: [original.coverage_id], reassignment_reasons: { [original.coverage_id]: 'Final critic requests deeper source inspection in a later run.' } });

  // Act & Assert
  try {
    assert.equal(checkAudit(f.run, f.root).complete, true);
    const [deferred] = JSON.parse(readFileSync(join(f.run, 'coverage-ledger.json'), 'utf8'));
    const archive = deferred.attempts[0];
    archive.local_checks[0].method = 'local'; archive.local_checks[0].artifact = 'agents/hunter/artifacts/result.txt' as never;
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify([deferred]));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('target_execution_not_permitted'));
  } finally { f.cleanup(); }
});

test('checks cannot certify a different coverage unit and archived leads retain unit links', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);
  const pending = fixture(); prepare(pending.run, pending.root, [record().fingerprint]);

  // Act & Assert
  try {
    const different = unit('hunter'); different.canonical_refs.surface = 'source.txt#another-input';
    different.coverage_id = canonicalCoverageId(different.canonical_refs);
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify([different]));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('coverage_evidence_mismatch'));
    const multi = hunterResult(); multi.coverage_ids.push(different.coverage_id);
    writeFileSync(join(f.run, 'agents/hunter/result.json'), JSON.stringify(multi));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('coverage_evidence_mismatch'));
    const original = unit('hunter', [record().fingerprint]);
    completeCritic(pending.run, pending.root, { stop: false, reassign_ids: [original.coverage_id], reassignment_reasons: { [original.coverage_id]: 'A later run must complete independent validation.' } });
    assert.equal(reportAudit(pending.run, pending.root).complete, false);
    assert.ok(readFileSync(join(pending.run, 'NEEDS-VALIDATION.md'), 'utf8').includes(original.coverage_id));
  } finally { f.cleanup(); pending.cleanup(); }
});

test('a candidate requires an exact independent verifier record before inclusion in findings', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root, [record().fingerprint]); completeCritic(f.run, f.root);

  // Act & Assert
  try {
    assert.ok(checkAudit(f.run, f.root).pending.includes(record().fingerprint));
    writeAuditFile(f.run, 'findings.json', [record()], f.root);
    assert.throws(() => reportAudit(f.run, f.root), /independent/);
    reserveAgent(f.run, 'verifier', 'verifier', f.root);
    finishAgent(f.run, 'verifier', 'native/verifier', { fingerprint: record().fingerprint, decision: 'needs_validation', record: record() }, f.root);
    assert.equal(reportAudit(f.run, f.root).complete, true);
    assert.match(readFileSync(join(f.run, 'NEEDS-VALIDATION.md'), 'utf8'), /Runtime behavior/);
    assert.match(readFileSync(join(f.run, 'REPORT.md'), 'utf8'), /Exact blockers/);
    assert.doesNotMatch(readFileSync(join(f.run, 'FINDINGS-DETAIL.md'), 'utf8'), /Fixture ownership behavior/);
  } finally { f.cleanup(); }
});

test('malformed findings and ledgers are rejected without generating clean reports', () => {
  // Arrange
  const f = fixture();

  // Act & Assert
  try {
    assert.throws(() => writeAuditFile(f.run, 'findings.json', [{ ...record(), severity: 'high' }], f.root), /Invalid findings/);
    assert.throws(() => writeAuditFile(f.run, 'coverage-ledger.json', [{}], f.root), /Invalid coverage/);
    writeFileSync(join(f.run, 'findings.json'), '[{"verdict":"confirmed"}]');
    assert.throws(() => reportAudit(f.run, f.root), /Invalid findings/);
  } finally { f.cleanup(); }
});

test('evidence output paths cannot escape or follow a symlink', () => {
  // Arrange
  const f = fixture(); const outside = realpathSync(mkdtempSync(join(tmpdir(), 'audit-outside-')));

  // Act & Assert
  try {
    assert.throws(() => checkAudit(outside, f.root), /outside|direct/);
    rmSync(join(f.run, 'findings.json')); symlinkSync(join(outside, 'secret'), join(f.run, 'findings.json'));
    assert.throws(() => checkAudit(f.run, f.root), /links/);
    assert.throws(() => writeAuditFile(f.run, 'findings.json', [], f.root), /links/);
  } finally { f.cleanup(); rmSync(outside, { recursive: true, force: true }); }
});

test('unregistered hunter leads prevent workflow completion', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);
  writeFileSync(join(f.run, 'agents/hunter/result.json'), JSON.stringify(hunterResult([{ fingerprint: 'unregistered-lead' }])));

  // Act & Assert
  try { assert.ok(reportAudit(f.run, f.root).reasons.includes('unregistered_hunter_candidate')); }
  finally { f.cleanup(); }
});

test('source evidence must identify maintained files and existing lines', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root, [record().fingerprint]);

  // Act & Assert
  try {
    const invalid = unit('hunter'); invalid.reviewed_paths = ['missing.txt']; invalid.local_checks[0].reviewed_paths = ['missing.txt'];
    writeAuditFile(f.run, 'coverage-ledger.json', [invalid], f.root);
    assert.throws(() => checkAudit(f.run, f.root), /maintained source/);
    writeAuditFile(f.run, 'coverage-ledger.json', [unit('hunter', [record().fingerprint])], f.root);
    completeCritic(f.run, f.root);
    const invalidLine = record(); invalidLine.evidence[0].line = 10000;
    reserveAgent(f.run, 'verifier', 'verifier', f.root);
    finishAgent(f.run, 'verifier', 'native/verifier', { fingerprint: invalidLine.fingerprint, decision: invalidLine.verdict, record: invalidLine }, f.root);
    writeAuditFile(f.run, 'findings.json', [invalidLine], f.root);
    assert.throws(() => checkAudit(f.run, f.root), /source line/);
  } finally { f.cleanup(); }
});

test('empty role results cannot satisfy reconnaissance or final criticism', () => {
  // Arrange
  const recon = fixture(); const critic = fixture(); prepare(critic.run, critic.root);

  // Act & Assert
  try {
    reserveAgent(recon.run, 'reconnaissance', 'empty', recon.root);
    assert.throws(() => finishAgent(recon.run, 'empty', 'native/empty', {}, recon.root), /role-specific/);
    assert.equal(metadataFor(recon.run, recon.root).agents[0].status, 'reserved');
    reserveAgent(critic.run, 'critic', 'critic', critic.root);
    assert.throws(() => finishAgent(critic.run, 'critic', 'native/critic', {}, critic.root), /role-specific/);
    // Simulate an ignored record edited outside the helper; evaluation must recheck its content.
    const meta = metadataFor(critic.run, critic.root); const call = meta.agents.at(-1)!;
    call.status = 'complete'; call.native_id = 'native/critic';
    writeFileSync(join(critic.run, 'run-metadata.json'), JSON.stringify(meta));
    mkdirSync(join(critic.run, 'agents/critic'));
    writeFileSync(join(critic.run, 'agents/critic/result.json'), '{}', { flag: 'w' });
    assert.ok(reportAudit(critic.run, critic.root).reasons.includes('invalid_agent_result'));
  } finally { recon.cleanup(); critic.cleanup(); }
});

test('critic completion is bound to the frozen finalized evidence snapshot', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root);

  // Act & Assert
  try {
    writeAuditFile(f.run, 'coverage-ledger.json', [], f.root);
    assert.throws(() => reserveAgent(f.run, 'critic', 'critic', f.root), /finalized/);
    writeAuditFile(f.run, 'coverage-ledger.json', [unit('hunter')], f.root);
    reserveAgent(f.run, 'critic', 'critic', f.root);
    assert.equal(metadataFor(f.run, f.root).critic_input_digest,
      createHash('sha256').update(readFileSync(join(f.run, 'critic-input.json'))).digest('hex'));
    assert.throws(() => finishAgent(f.run, 'critic', 'native/critic', criticResult(f.run, f.root, { input_digest: '0'.repeat(64) }), f.root), /snapshot/);
    assert.throws(() => writeAuditFile(f.run, 'architecture.md', 'Replacement map', f.root), /frozen/);
    writeFileSync(join(f.run, 'architecture.md'), readFileSync(join(f.run, 'architecture.md'), 'utf8') + '\nChanged during criticism.');
    assert.throws(() => finishAgent(f.run, 'critic', 'native/critic', criticResult(f.run, f.root), f.root), /snapshot/);
    assert.equal(metadataFor(f.run, f.root).agents.at(-1)!.status, 'reserved');
  } finally { f.cleanup(); }
});

test('the helper applies only the final critic decisions and freezes reconciliation', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root);

  // Act
  try {
    completeCritic(f.run, f.root, { stop: false, missing_units: [gap()] });
    const ledger = JSON.parse(readFileSync(join(f.run, 'coverage-ledger.json'), 'utf8'));

    // Assert
    assert.equal(ledger.length, 2);
    assert.equal(ledger.find((item: { surface: string }) => item.surface === gap().surface).status, 'deferred');
    assert.equal(checkAudit(f.run, f.root).complete, true);
    assert.throws(() => writeAuditFile(f.run, 'coverage-ledger.json', ledger, f.root), /frozen/);
    assert.throws(() => registerCandidate(f.run, 'late-candidate', 'hunter', f.root), /frozen/);
    ledger.find((item: { status: string }) => item.status === 'covered').surface = 'Unreviewed coverage claim';
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify(ledger));
    assert.ok(checkAudit(f.run, f.root).reasons.includes('critic_evidence_changed'));
  } finally { f.cleanup(); }
});

test('valid coverage changes cannot leave old reports passing verification', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root); reportAudit(f.run, f.root);
  const originalReport = readFileSync(join(f.run, 'REPORT.md'), 'utf8');

  // Act
  try {
    const extra = gap(); const base = unit('hunter');
    const deferred = { ...base, ...extra, coverage_id: canonicalCoverageId(extra.canonical_refs), status: 'deferred',
      agent_id: null, reviewed_paths: [], local_checks: [], unresolved: [extra.reason] };
    const ledger = [base, deferred].sort((a, b) => a.coverage_id < b.coverage_id ? -1 : 1);
    writeFileSync(join(f.run, 'coverage-ledger.json'), JSON.stringify(ledger));
    const checked = checkAudit(f.run, f.root);

    // Assert
    assert.equal(readFileSync(join(f.run, 'REPORT.md'), 'utf8'), originalReport);
    assert.equal(checked.complete, false);
    assert.ok(checked.reasons.includes('report_inputs_changed'));
  } finally { f.cleanup(); }
});

for (const input of ['architecture.md', 'findings.json', 'agents/recon_0/result.json', 'agents/critic/result.json', 'critic-input.json', 'run-metadata.json']) {
  test(`rendered reports are bound to ${input}`, () => {
    // Arrange
    const f = fixture(); baseline(f.run, f.root); reportAudit(f.run, f.root);

    // Act
    try {
      const path = join(f.run, input);
      if (input === 'run-metadata.json') {
        const meta = metadataFor(f.run, f.root); meta.run_status = 'in_progress';
        writeFileSync(path, JSON.stringify(meta));
      } else writeFileSync(path, readFileSync(path, 'utf8') + '\n');
      const checked = checkAudit(f.run, f.root);

      // Assert
      assert.equal(checked.complete, false);
      assert.ok(checked.reasons.includes('report_inputs_changed'));
    } finally { f.cleanup(); }
  });
}

test('changed but independently matched finding records invalidate the report inputs', () => {
  // Arrange
  const f = fixture(); prepare(f.run, f.root, [record().fingerprint]); completeCritic(f.run, f.root);
  reserveAgent(f.run, 'verifier', 'verifier', f.root);
  finishAgent(f.run, 'verifier', 'native/verifier', { fingerprint: record().fingerprint, decision: 'needs_validation', record: record() }, f.root);
  writeAuditFile(f.run, 'findings.json', [record()], f.root); reportAudit(f.run, f.root);

  // Act
  try {
    const changed = { ...record(), description: 'A changed, valid description that is absent from the retained reports.' };
    writeFileSync(join(f.run, 'findings.json'), JSON.stringify([changed]));
    writeFileSync(join(f.run, 'agents/verifier/result.json'), JSON.stringify({ fingerprint: changed.fingerprint, decision: changed.verdict, record: changed }));
    const checked = checkAudit(f.run, f.root);

    // Assert
    assert.deepEqual(checked.reasons, ['report_inputs_changed']);
    assert.equal(checked.complete, false);
  } finally { f.cleanup(); }
});

test('restoring source cannot silently turn an incomplete report into a passing check', () => {
  // Arrange
  const f = fixture(); baseline(f.run, f.root);
  const source = readFileSync(join(f.root, 'source.txt'));

  // Act
  try {
    writeFileSync(join(f.root, 'source.txt'), 'Changed while the audit was running.');
    assert.equal(reportAudit(f.run, f.root).complete, false);
    const retainedReport = readFileSync(join(f.run, 'REPORT.md'), 'utf8');
    writeFileSync(join(f.root, 'source.txt'), source);
    const checked = checkAudit(f.run, f.root);

    // Assert
    assert.equal(metadataFor(f.run, f.root).run_status, 'incomplete');
    assert.equal(readFileSync(join(f.run, 'REPORT.md'), 'utf8'), retainedReport);
    assert.equal(checked.complete, false);
    assert.ok(checked.reasons.includes('report_status_changed'));
  } finally { f.cleanup(); }
});
