#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertCurrent, captureCandidate, repoRoot } from './candidate.mjs';
import { readTrx } from './trx.mjs';
export { repoRoot } from './candidate.mjs';

export const services = ['MainSubSys', 'Orchestrator', 'KernelMemory', 'McpServer'];
export const testProject = service => `src/MyAIAgent.${service}.Tests/MyAIAgent.${service}.Tests.csproj`;
const solution = 'src/MyAIAgent.slnx';

export function parseOptions(args) {
  const options = {};
  while (args.length) {
    const key = args.shift();
    if (!['--project', '--base'].includes(key) || !args.length || args[0].startsWith('--') || options[key.slice(2)]) {
      throw new Error('Usage: node scripts/ai/quality.mjs [--project MainSubSys|Orchestrator|KernelMemory|McpServer] [--base <merge-base>]');
    }
    options[key.slice(2)] = args.shift();
  }
  if (options.project && !services.includes(options.project)) throw new Error(`Unknown service: ${options.project}`);
  return options;
}

export function runQuality({ root = repoRoot, run = spawnSync, output = console.log, project, base = 'HEAD' } = {}) {
  if (project && !services.includes(project)) throw new Error(`Unknown service: ${project}`);
  const candidate = captureCandidate(root, base);
  const parent = resolve(root, 'artifacts/ai');
  mkdirSync(parent, { recursive: true });
  const directory = mkdtempSync(resolve(parent, new Date().toISOString().replaceAll(':', '-') + '-'));
  const report = { version: 1, status: 'RUNNING', scope: project ? 'targeted' : 'full', project: project ?? null,
    solution, directory, candidate, tests: { total: 0, passed: 0, failed: 0, skipped: 0, suites: [] }, steps: [] };
  const reportPath = resolve(directory, 'quality.json');
  const save = () => writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  save();
  function step(name, command, args, allowFailure = false) {
    output(`\n[${name}] ${command} ${args.join(' ')}`);
    const result = run(command, args, { cwd: root, stdio: 'inherit', shell: false });
    const passed = result.status === 0 && !result.error && !result.signal;
    report.steps.push({ name, command: [command, ...args], status: passed ? 'PASS' : 'FAIL',
      exitCode: result.status ?? null, error: result.error?.message ?? null, signal: result.signal ?? null });
    save();
    if (!passed && !allowFailure) throw new Error(`${name} failed.`);
    return passed;
  }
  try {
    const target = project ? testProject(project) : solution;
    // Tooling tests form part of final evidence, including the shared config contracts.
    if (!project) step('workflow-tests', 'node', ['--test', ...readdirSync(resolve(repoRoot, 'scripts/ai')).filter(name => name.endsWith('.test.mjs')).sort().map(name => `scripts/ai/${name}`)]);
    step('restore', 'dotnet', ['restore', target, '--verbosity', 'minimal']);
    step('build', 'dotnet', ['build', target, '--no-restore', '--verbosity', 'minimal']);
    for (const service of project ? [project] : services) {
      const results = resolve(directory, 'tests', service);
      mkdirSync(results, { recursive: true });
      const passed = step(`test:${service}`, 'dotnet', ['test', testProject(service), '--no-build', '--no-restore', '--verbosity', 'minimal',
        '--logger', 'trx', '--results-directory', results], true);
      const suite = { service, ...readTrx(results) };
      report.tests.suites.push(suite);
      for (const key of ['total', 'passed', 'failed', 'skipped']) report.tests[key] += suite[key];
      save();
      if (!passed || suite.failed || !['Completed', 'Passed'].includes(suite.outcome)) throw new Error(`Tests failed: ${service}`);
    }
    const include = candidate.files.filter(file => file.path.endsWith('.cs') && ['100644', '100755'].includes(file.mode)).map(file => file.path);
    report.format = { scope: 'candidate C# files', include };
    if (project) {
      report.format.scope = 'deferred to full gate';
      report.steps.push({ name: 'format', status: 'NOT_RUN', reason: 'Targeted iteration; run full quality before review.' });
    } else if (include.length) {
      step('format', 'dotnet', ['format', solution, '--no-restore', '--verify-no-changes', '--verbosity', 'minimal', '--include', ...include]);
    } else {
      report.steps.push({ name: 'format', status: 'NOT_APPLICABLE', reason: 'No added or modified C# files in candidate.' });
    }
    assertCurrent(candidate, root);
    report.status = 'PASS';
  } catch (error) {
    report.status = 'FAIL';
    report.error = error.message;
  }
  save();
  output(`\nQuality (${report.scope}): ${report.status}. Report: ${reportPath}`);
  return report;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    process.exitCode = runQuality(parseOptions(process.argv.slice(2))).status === 'PASS' ? 0 : 1;
  } catch (error) {
    console.error(`Quality blocked: ${error.message}`);
    process.exitCode = 1;
  }
}
