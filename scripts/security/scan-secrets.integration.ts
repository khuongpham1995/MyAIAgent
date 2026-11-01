/**
 * MyAIAgent | Security Tooling · Integration Tests
 *
 * Exercises the checksum exception against pinned Gitleaks using disposable file and Git fixtures before repository scanning.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { installScanner, runScan, scannerConfiguration } from './scan-secrets.ts';

const binary = installScanner(process.cwd());
const manifest = '.agents/vendor/security-audit/source.json';
const checksum = createHash('sha256').update(readFileSync('.agents/vendor/security-audit/WEB-PROTOCOL-AND-AUTH.md')).digest('hex');
const different = createHash('sha256').update('synthetic scanner regression marker').digest('hex');

for (const kind of ['dir', 'git'] as const) {
  test(`${kind}: the checksum exception preserves detection for different values, paths and rules`, t => {
    // Arrange
    const temporary = mkdtempSync(join(tmpdir(), 'myaiagent-policy-regression-'));
    t.after(() => rmSync(temporary, { recursive: true, force: true }));
    const target = join(temporary, 'checkout');
    mkdirSync(join(target, '.agents/vendor/security-audit'), { recursive: true });
    const config = join(temporary, 'scanner.toml');
    writeFileSync(config, scannerConfiguration(target));
    const ignore = join(temporary, 'ignore');
    writeFileSync(ignore, '');
    const emptyTemplate = join(temporary, 'empty-template');
    mkdirSync(emptyTemplate);
    const git = (...args: string[]) => execFileSync('git', ['-c', 'core.hooksPath=/dev/null', ...args], {
      cwd: target, stdio: 'pipe', timeout: 10_000,
      env: { PATH: process.env.PATH, GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null', GIT_TERMINAL_PROMPT: '0' },
    });
    if (kind === 'git') {
      git('init', '--quiet', `--template=${emptyTemplate}`);
      git('config', 'user.email', 'fixture@example.invalid');
      git('config', 'user.name', 'Policy regression');
    }
    const commit = () => {
      if (kind === 'git') {
        git('add', '--all');
        git('commit', '--quiet', '-m', 'Add synthetic policy fixture');
      }
    };
    writeFileSync(join(target, manifest), JSON.stringify({ 'WEB-PROTOCOL-AND-AUTH.md': checksum }));
    commit();

    // Act & Assert
    assert.equal(runScan(binary, target, join(temporary, 'clean.json'), kind, config, ignore, { output() {} }), false);

    // Arrange
    writeFileSync(join(target, manifest), JSON.stringify({
      'WEB-PROTOCOL-AND-AUTH.md': checksum,
      api_key: different,
      github_token: ['ghp', different.slice(0, 36)].join('_'),
    }));
    writeFileSync(join(target, 'other.json'), JSON.stringify({ 'WEB-PROTOCOL-AND-AUTH.md': checksum }));
    mkdirSync(join(target, 'nested', '.agents/vendor/security-audit'), { recursive: true });
    writeFileSync(join(target, 'nested', manifest), JSON.stringify({ 'WEB-PROTOCOL-AND-AUTH.md': checksum }));
    commit();
    const report = join(temporary, 'findings.json');

    // Act
    const found = runScan(binary, target, report, kind, config, ignore, { output() {} });
    const findings = JSON.parse(readFileSync(report, 'utf8')) as { RuleID: string; File: string }[];

    // Assert
    assert.equal(found, true);
    assert.ok(findings.some(finding => finding.RuleID === 'generic-api-key' && finding.File.endsWith(manifest)));
    assert.ok(findings.some(finding => finding.RuleID === 'generic-api-key' && finding.File.endsWith('other.json')));
    assert.ok(findings.some(finding => finding.RuleID === 'github-pat' && finding.File.endsWith(manifest)));
    assert.ok(findings.some(finding => finding.RuleID === 'generic-api-key' && finding.File.endsWith(`nested/${manifest}`)));
    assert.equal(findings.length, 4);
  });
}
