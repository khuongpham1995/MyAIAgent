/**
 * MyAIAgent | Deployment Tooling · Tests
 *
 * Checks deployment argument validation, safe parameter encoding, and cleanup using a simulated Azure CLI.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, join, resolve } from 'node:path';
import { deployContainerApps } from './aca.ts';

function configuration(): NodeJS.ProcessEnv {
  return Object.fromEntries(['RESOURCE_GROUP', 'ACR_NAME', 'ACR_RESOURCE_GROUP', 'APP_ENVIRONMENT_NAME', 'KEY_VAULT_NAME',
    'GATEWAY_IDENTITY_NAME', 'ORCHESTRATOR_IDENTITY_NAME', 'KERNEL_MEMORY_IDENTITY_NAME', 'MCP_IDENTITY_NAME',
    'APP_CONFIG_ENDPOINT', 'COSMOS_ENDPOINT', 'ENTRA_TENANT_ID', 'ENTRA_AUDIENCE', 'AZURE_OPENAI_ENDPOINT',
    'AZURE_OPENAI_DEPLOYMENT', 'AZURE_EMBEDDING_DEPLOYMENT', 'AZURE_TEXT_DEPLOYMENT', 'SEARCH_ENDPOINT']
    .map(name => [name, 'example']));
}

test('deployment encodes identity parameters as JSON without interpreting shell characters', t => {
  // Arrange
  const root = mkdtempSync(join(tmpdir(), 'myaiagent-deploy-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const tools = join(root, 'tools');
  mkdirSync(tools);
  const captured = join(root, 'parameters.json');
  const fakeAzure = join(tools, 'az');
  writeFileSync(fakeAzure, `#!/usr/bin/env node
const fs = require('node:fs');
const parameters = process.argv.find(value => value.startsWith('@'));
if (!parameters) process.exit(1);
fs.writeFileSync(process.env.CAPTURED_PARAMETERS, fs.readFileSync(parameters.slice(1)));
fs.writeFileSync(process.env.CAPTURED_PATH, parameters.slice(1));
console.log('example.test');
`);
  chmodSync(fakeAzure, 0o700);
  const output = join(root, 'github-output');
  const capturedPath = join(root, 'parameter-path');
  const env: NodeJS.ProcessEnv = { ...configuration(), PATH: tools + delimiter + process.env.PATH, CAPTURED_PARAMETERS: captured,
    CAPTURED_PATH: capturedPath, GITHUB_OUTPUT: output };
  env.GATEWAY_IDENTITY_NAME = 'gateway with "quotes"';
  env.ORCHESTRATOR_IDENTITY_NAME = 'orchestrator\nsecond line';
  env.KERNEL_MEMORY_IDENTITY_NAME = 'literal $(text)';
  env.MCP_IDENTITY_NAME = 'literal `text`';

  // Act
  const result = spawnSync(process.execPath, ['--import', 'tsx', resolve('scripts/deploy/aca.ts'), 'staging', 'gateway', 'orchestrator', 'memory', 'mcp'],
    { env, encoding: 'utf8', shell: false });

  // Assert
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout.trim(), 'example.test');
  const parameters = JSON.parse(readFileSync(captured, 'utf8'));
  assert.deepEqual(parameters.identityNames, { value: {
    gateway: env.GATEWAY_IDENTITY_NAME, orchestrator: env.ORCHESTRATOR_IDENTITY_NAME,
    kernelmemory: env.KERNEL_MEMORY_IDENTITY_NAME, mcpserver: env.MCP_IDENTITY_NAME,
  } });
  assert.equal(Object.hasOwn(parameters, 'googleSearchEngineId'), false);
  assert.deepEqual(parameters.stage, { value: 'staging' });
  assert.deepEqual(parameters.imageGateway, { value: 'gateway' });
  assert.deepEqual(parameters.publicChatIssuer, { value: '' });
  assert.equal(readFileSync(output, 'utf8'), 'fqdn=example.test\n');
  assert.equal(existsSync(readFileSync(capturedPath, 'utf8')), false);
});

test('invalid deployment arguments and missing configuration never invoke Azure', () => {
  // Arrange
  const run = () => { assert.fail('Azure must not run'); };

  // Act & Assert
  assert.throws(() => deployContainerApps(['invalid', 'a', 'b', 'c', 'd'], { env: {}, run }), /Usage:/);
  assert.throws(() => deployContainerApps(['staging', 'a', 'b', 'c', 'd'], { env: {}, run }), /RESOURCE_GROUP is required/);
});

test('deployment CLI rejects missing arguments with the original usage exit code', () => {
  // Arrange & Act
  const result = spawnSync(process.execPath, ['--import', 'tsx', resolve('scripts/deploy/aca.ts')], { encoding: 'utf8' });

  // Assert
  assert.equal(result.status, 2);
  assert.match(result.stderr, /Usage:/);
});

test('failed Azure invocation removes temporary parameters and does not reveal command output', () => {
  // Arrange
  let parameterFile = '';
  const privateOutput = ['private', 'azure', 'response'].join('-');

  // Act & Assert
  assert.throws(() => deployContainerApps(['production', 'a', 'b', 'c', 'd'], {
    env: configuration(), run(command, args, options) {
      assert.equal(command, 'az');
      assert.equal(options.shell, false);
      parameterFile = args.find(value => value.startsWith('@'))!.slice(1);
      assert.equal(existsSync(parameterFile), true);
      return { status: 1, stderr: privateOutput };
    },
  }), error => {
    assert.ok(error instanceof Error);
    assert.equal(error.message.includes(privateOutput), false);
    return /Azure deployment failed/.test(error.message);
  });

  assert.equal(existsSync(parameterFile), false);
});

test('untrusted deployment output cannot inject a GitHub output line', t => {
  // Arrange
  const root = mkdtempSync(join(tmpdir(), 'myaiagent-output-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const output = join(root, 'output');

  // Act & Assert
  assert.throws(() => deployContainerApps(['staging', 'a', 'b', 'c', 'd'], {
    env: { ...configuration(), GITHUB_OUTPUT: output },
    run: () => ({ status: 0, stdout: 'example.test\nextra=value' }),
  }), /invalid gateway hostname/);

  assert.equal(existsSync(output), false);
});
