/**
 * MyAIAgent | Security Tooling
 *
 * Provides an injectable process-runner contract for security and deployment automation.
 */

import { spawnSync, type SpawnSyncOptions } from 'node:child_process';

export interface ProcessResult {
  status: number | null;
  stdout?: string | Buffer;
  stderr?: string | Buffer;
  error?: Error;
  signal?: NodeJS.Signals | null;
}

export type ProcessRunner = (command: string, args: string[], options: SpawnSyncOptions) => ProcessResult;

export const runProcess: ProcessRunner = (command, args, options) => spawnSync(command, args, options);
