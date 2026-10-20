/**
 * MyAIAgent | Deployment Tooling · Tests
 *
 * Checks deployment smoke-test retries, response limits, authentication, and redirect rejection without Azure calls.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { smokeTest, type HttpGet } from './smoke.ts';
import type { ProcessRunner } from '../security/process.ts';

function fixture(responses: { status: number; body?: string }[]) {
  const calls: { url: string; options: RequestInit }[] = [];
  const delays: number[] = [];
  const commands: string[][] = [];
  const token = ['smoke', 'fixture'].join('-');
  const request: HttpGet = async (url, options) => {
    calls.push({ url, options });
    const response = responses.shift() ?? { status: 503 };
    return new Response(response.body ?? '', { status: response.status });
  };
  const run: ProcessRunner = (command, args, options) => {
    assert.equal(command, 'az');
    assert.equal(options.shell, false);
    commands.push(args);
    return { status: 0, stdout: token };
  };
  return { calls, delays, commands, token, request, run, delay: async (ms: number) => { delays.push(ms); } };
}

test('smoke checks readiness, anonymous rejection and authenticated success with bounded retries', async () => {
  // Arrange
  const context = fixture([{ status: 503 }, { status: 200 }, { status: 401 },
    { status: 503 }, { status: 200, body: '{"isSuccess":true}' },
    { status: 200, body: '{"isSuccess":true}' }]);

  // Act
  await smokeTest(['gateway.example.test', 'audience'], context);

  // Assert
  assert.deepEqual(context.delays, [10_000, 10_000]);
  assert.deepEqual(context.commands, [['account', 'get-access-token', '--resource', 'api://audience', '--query', 'accessToken', '--output', 'tsv']]);
  assert.equal(context.calls[0].url, 'https://gateway.example.test/health/ready');
  assert.equal(context.calls[2].options.headers, undefined);
  assert.deepEqual(context.calls[3].options.headers, { Authorization: `Bearer ${context.token}` });
  assert.equal(context.calls.at(-1)?.url, 'https://gateway.example.test/admin/tools');
  assert.deepEqual(context.calls.at(-1)?.options.headers, { Authorization: `Bearer ${context.token}` });
  assert.ok(context.calls.every(call => call.options.redirect === 'error' && call.options.signal instanceof AbortSignal));
});

test('MCP discovery must succeed even after authenticated agent listing passes', async () => {
  // Arrange
  const context = fixture([{ status: 200 }, { status: 401 }, { status: 200, body: '{"isSuccess":true}' },
    ...Array.from({ length: 30 }, () => ({ status: 200, body: '{"isSuccess":false}' }))]);

  // Act & Assert
  await assert.rejects(smokeTest(['gateway.example.test', 'audience'], context), /failed for \/admin\/tools/);
  assert.equal(context.calls.length, 33);
  assert.equal(context.delays.length, 29);
});

test('anonymous admin success fails before requesting a token', async () => {
  // Arrange
  const context = fixture([{ status: 200 }, { status: 200 }]);

  // Act & Assert
  await assert.rejects(smokeTest(['gateway.example.test', 'audience'], context), /Anonymous admin access was not rejected: HTTP 200/);

  assert.equal(context.commands.length, 0);
});

test('readiness transport failures stop after thirty attempts without fetching a token', async () => {
  // Arrange
  const context = fixture([]);
  context.request = async (url, options) => {
    context.calls.push({ url, options });
    throw new TypeError('network unavailable');
  };

  // Act & Assert
  await assert.rejects(smokeTest(['gateway.example.test', 'audience'], context), /Gateway readiness failed: HTTP 0/);

  assert.equal(context.calls.length, 30);
  assert.equal(context.delays.length, 29);
  assert.equal(context.commands.length, 0);
});

for (const body of ['invalid JSON', '{"isSuccess":false}', '{"isSuccess":"true"}', 'x'.repeat(1024 * 1024 + 1)]) {
  test(`unsuccessful or oversized admin payload cannot pass (${body.length} bytes)`, async () => {
    // Arrange
    const context = fixture([{ status: 200 }, { status: 401 }, ...Array.from({ length: 30 }, () => ({ status: 200, body }))]);

    // Act & Assert
    await assert.rejects(smokeTest(['gateway.example.test', 'audience'], context), /Authenticated admin smoke test failed/);

    assert.equal(context.calls.length, 32);
    assert.equal(context.delays.length, 29);
  });
}

test('missing access token fails without an authenticated HTTP call or token output', async () => {
  // Arrange
  const context = fixture([{ status: 200 }, { status: 401 }]);
  context.run = () => ({ status: 1, stderr: context.token });

  // Act & Assert
  await assert.rejects(smokeTest(['gateway.example.test', 'audience'], context), error => {
    assert.ok(error instanceof Error);
    assert.equal(error.message.includes(context.token), false);
    return /Could not obtain/.test(error.message);
  });

  assert.equal(context.calls.length, 2);
});

test('hostnames containing schemes, ports, credentials or paths are rejected before network access', async () => {
  // Arrange, Act & Assert
  for (const hostname of ['https://example.test', 'user@example.test', 'example.test:443', 'example.test/path']) {
    const context = fixture([]);
    await assert.rejects(smokeTest([hostname, 'audience'], context), /Expected a gateway hostname/);
    assert.equal(context.calls.length, 0);
    assert.equal(context.commands.length, 0);
  }
});
