#!/usr/bin/env node
/**
 * MyAIAgent | Security Tooling
 *
 * Installs a checksum-verified scanner and scans available Git history and worktree content with redacted reports.
 */

import type { SpawnSyncOptions } from 'node:child_process';
import { runProcess, type ProcessRunner } from './process.ts';
import { createHash } from 'node:crypto';
import { chmodSync, copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const version = '8.30.1';
// Official release checksums; archives are verified before tar or Gitleaks reads them.
const checksums: Record<string, readonly [string, string]> = {
  'darwin-arm64': ['darwin_arm64', 'b40ab0ae55c505963e365f271a8d3846efbc170aa17f2607f13df610a9aeb6a5'],
  'darwin-x64': ['darwin_x64', 'dfe101a4db2255fc85120ac7f3d25e4342c3c20cf749f2c20a18081af1952709'],
  'linux-x64': ['linux_x64', '551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb'],
  'linux-arm64': ['linux_arm64', 'e4a487ee7ccd7d3a7f7ec08657610aa3606637dab924210b3aee62570fb4b080'],
};

function checked(command: string, args: string[], options: SpawnSyncOptions = {}, run: ProcessRunner = runProcess): string | Buffer {
  const result = run(command, args, { shell: false, maxBuffer: 64 * 1024 * 1024, ...options });
  if (result.status !== 0 || result.error || result.signal) {
    throw new Error(`${command} failed; no command output was printed.`);
  }
  if (result.stdout === undefined) throw new Error(`${command} returned no output.`);
  return result.stdout;
}

export function installScanner(root: string, { platform = process.platform, arch = process.arch, run = runProcess }:
  { platform?: string; arch?: string; run?: ProcessRunner } = {}): string {
  const release = checksums[`${platform}-${arch}`];
  if (!release) throw new Error('The secret scanner supports macOS and Linux on x64 or arm64.');
  const [archivePlatform, checksum] = release;
  const tools = join(root, 'artifacts/tools/gitleaks', version);
  mkdirSync(tools, { recursive: true });
  const archive = join(tools, `${archivePlatform}.tar.gz`);
  if (!existsSync(archive)) {
    const url = `https://github.com/gitleaks/gitleaks/releases/download/v${version}/gitleaks_${version}_${archivePlatform}.tar.gz`;
    checked('curl', ['--fail', '--location', '--proto', '=https', '--proto-redir', '=https',
      '--silent', '--show-error', '--max-time', '60', '--output', archive, url], {}, run);
  }
  if (createHash('sha256').update(readFileSync(archive)).digest('hex') !== checksum) {
    rmSync(archive);
    throw new Error('Gitleaks archive checksum mismatch; nothing was executed.');
  }
  const member = checked('tar', ['-tvzf', archive, 'gitleaks'], { encoding: 'utf8' }, run).toString().trim();
  if (!member.startsWith('-') || member.split('\n').length !== 1) throw new Error('Invalid Gitleaks archive member.');
  // Extract only the regular executable to stdout; archive paths are never written by tar.
  const content = checked('tar', ['-xOzf', archive, 'gitleaks'], {}, run);
  const binary = join(tools, 'gitleaks');
  writeFileSync(binary, content);
  chmodSync(binary, 0o700);
  return binary;
}

export function runScan(binary: string, root: string, report: string, kind: 'git' | 'dir', config: string, ignore: string,
  { run = runProcess, output = console.log }: { run?: ProcessRunner; output?: (line: string) => void } = {}): boolean {
  const args = [kind, '--redact=100', '--no-banner', '--log-level=error', '--ignore-gitleaks-allow',
    '--config', config, '--gitleaks-ignore-path', ignore, '--report-format=json', '--report-path', report];
  if (kind === 'git') args.push('--log-opts=--all');
  const result = run(binary, [...args, root], { shell: false, maxBuffer: 64 * 1024 * 1024 });
  if ((result.status !== 0 && result.status !== 1) || result.error || result.signal) {
    throw new Error(`${kind} scanner failed; no credential output was printed.`);
  }
  if (!existsSync(report)) throw new Error('Scanner report is missing; scan cannot pass.');
  let findings: Record<string, unknown>[];
  try {
    const parsed: unknown = JSON.parse(readFileSync(report, 'utf8'));
    if (!Array.isArray(parsed) || parsed.some(value => !value || typeof value !== 'object' || Array.isArray(value))) throw new Error();
    findings = parsed;
  } catch {
    throw new Error('Scanner report is invalid; no report content was printed.');
  }
  output(`${kind}: ${findings.length} potential secret(s); redacted report: ${report}`);
  for (const finding of findings) {
    // Never print Secret, Match, entropy data, or arbitrary scanner logs.
    output(JSON.stringify(Object.fromEntries(['RuleID', 'File', 'StartLine', 'Commit'].map(key => [key, finding[key]]))));
  }
  return result.status === 1 || findings.length > 0;
}

export function copyWorktree(root: string, destination: string, run: ProcessRunner = runProcess): void {
  const physicalRoot = realpathSync(root);
  const paths = checked('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' }, run).toString();
  for (const relative of new Set(paths.split('\0').filter(Boolean))) {
    if (isAbsolute(relative) || relative.split('/').includes('..')) throw new Error('Invalid worktree path.');
    const source = join(root, relative);
    if (!existsSync(source) || !lstatSync(source).isFile()) continue;
    // A replaced parent directory must not make the snapshot read files outside the checkout.
    if (realpathSync(source) !== join(physicalRoot, relative)) continue;
    const target = join(destination, relative);
    mkdirSync(dirname(target), { recursive: true });
    copyFileSync(source, target);
  }
}

export function scanSecrets(): number {
  const root = checked('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).toString().trim();
  const binary = installScanner(root);
  const reportsParent = join(root, 'artifacts/security');
  mkdirSync(reportsParent, { recursive: true });
  const reports = mkdtempSync(join(reportsParent, new Date().toISOString().replaceAll(':', '-') + '-'));
  const temporary = mkdtempSync(join(tmpdir(), 'myaiagent-secret-scan-'));
  try {
    const config = join(temporary, 'scanner.toml');
    writeFileSync(config, 'title = "MyAIAgent secret protection"\n[extend]\nuseDefault = true\n');
    const ignore = join(temporary, 'ignore');
    // Exact historical fingerprints for reviewed JWT test fixtures; no future commit is exempted.
    const fixtureCommit = '5e41834bde7cde94dbcd9ca8327108dd800f53a6';
    const fixtures: [string, number][] = [
      ['src/MyAIAgent.Gateway.Tests/Unit/Presentation/PublicChatSettingsTests.cs', 10],
      ['src/MyAIAgent.Gateway.Tests/Integration/Presentation/PublicChatEndpointsTests.cs', 83],
      ['src/MyAIAgent.Gateway.Tests/Integration/Presentation/PublicChatTestApp.cs', 23],
    ];
    writeFileSync(ignore, fixtures.map(([path, line]) => `${fixtureCommit}:${path}:generic-api-key:${line}\n`).join(''));
    const historyFound = runScan(binary, root, join(reports, 'history.json'), 'git', config, ignore);
    const worktree = join(temporary, 'worktree');
    mkdirSync(worktree);
    copyWorktree(root, worktree);
    const worktreeFound = runScan(binary, worktree, join(reports, 'worktree.json'), 'dir', config, ignore);
    return historyFound || worktreeFound ? 1 : 0;
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    process.exitCode = scanSecrets();
  } catch (error) {
    console.error(`Secret scan could not complete: ${error instanceof Error ? error.message : 'Unexpected failure.'}`);
    process.exitCode = 2;
  }
}
