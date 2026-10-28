/**
 * MyAIAgent | Deployment Tooling
 *
 * Checks deployed HTTPS readiness, anonymous rejection, and authenticated Gateway success with bounded retries.
 */

import { setTimeout } from 'node:timers/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runProcess, type ProcessRunner } from '../security/process.ts';
import { gatewayHostname, UsageError } from './cli.ts';

export type HttpGet = (url: string, options: RequestInit) => Promise<Response>;

async function successfulPayload(response: Response): Promise<boolean> {
  if (!response.body) return false;
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let content = '';
  let size = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > 1024 * 1024) return false;
      content += decoder.decode(value, { stream: true });
    }
    const payload: unknown = JSON.parse(content + decoder.decode());
    return payload !== null && typeof payload === 'object' && 'isSuccess' in payload && payload.isSuccess === true;
  } finally {
    try { await reader.cancel(); } finally { reader.releaseLock(); }
  }
}

export async function smokeTest(args: string[], { run = runProcess, request = fetch, delay = (ms: number) => setTimeout(ms) }:
  { run?: ProcessRunner; request?: HttpGet; delay?: (ms: number) => Promise<void> } = {}): Promise<void> {
  if (args.length !== 2 || !args[1]) throw new UsageError('Usage: npm run deploy:smoke -- <gateway-fqdn> <Entra-audience>');
  const gateway = `https://${gatewayHostname(args[0])}`;
  const audience = args[1];

  async function probe(path: string, token?: string): Promise<{ status: number; success: boolean }> {
    try {
      const response = await request(`${gateway}${path}`, {
        redirect: 'error', signal: AbortSignal.timeout(token ? 30_000 : 10_000),
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      });
      const success = token && response.status === 200 ? await successfulPayload(response) : false;
      await response.body?.cancel();
      return { status: response.status, success };
    } catch {
      // A transport, timeout, redirect, or invalid response fails this attempt without logging headers or body.
      return { status: 0, success: false };
    }
  }

  let ready = false;
  let status = 0;
  for (let attempt = 0; attempt < 30; attempt++) {
    ({ status } = await probe('/health/ready'));
    if (status === 200) { ready = true; break; }
    if (attempt < 29) await delay(10_000);
  }
  if (!ready) throw new Error(`Gateway readiness failed: HTTP ${status}`);
  ({ status } = await probe('/api/admin/agents'));
  if (status !== 401) throw new Error(`Anonymous admin access was not rejected: HTTP ${status}`);

  const result = run('az', ['account', 'get-access-token', '--resource', `api://${audience}`,
    '--query', 'accessToken', '--output', 'tsv'], { encoding: 'utf8', shell: false, timeout: 30_000, maxBuffer: 1024 * 1024 });
  const token = result.stdout?.toString().trim();
  if (result.status !== 0 || result.error || result.signal || !token) throw new Error('Could not obtain the smoke-test access token.');
  for (const path of ['/api/admin/agents', '/api/admin/tools']) {
    let succeeded = false;
    for (let attempt = 0; attempt < 30; attempt++) {
      const response = await probe(path, token);
      status = response.status;
      if (status === 200 && response.success) { succeeded = true; break; }
      if (attempt < 29) await delay(10_000);
    }
    if (!succeeded) throw new Error(`Authenticated admin smoke test failed for ${path}: HTTP ${status}`);
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    await smokeTest(process.argv.slice(2));
    console.log('Gateway readiness, authorization, and MCP discovery smoke tests passed.');
  } catch (error) {
    console.error(error instanceof Error ? error.message : 'Smoke test failed.');
    process.exitCode = error instanceof UsageError ? 2 : 1;
  }
}
