import type { SpawnSyncOptions } from 'node:child_process';

export interface CandidateFile {
  path: string;
  mode: 'deleted' | '120000' | '100644' | '100755';
  sha256: string | null;
}

export interface Candidate {
  version: number;
  head: string;
  base: string;
  files: CandidateFile[];
  digest: string;
}

export interface TestCounts {
  total: number;
  passed: number;
  failed: number;
  skipped: number;
}

export interface TestSuite extends TestCounts {
  service: string;
  file: string;
  outcome: string;
}

export interface QualityStep {
  name: string;
  status: 'PASS' | 'FAIL' | 'NOT_RUN' | 'NOT_APPLICABLE';
  command?: string[];
  exitCode?: number | null;
  error?: string | null;
  signal?: NodeJS.Signals | null;
  reason?: string;
}

export interface QualityReport {
  version: number;
  status: 'RUNNING' | 'PASS' | 'FAIL';
  scope: 'full' | 'targeted';
  project: string | null;
  solution: string;
  directory: string;
  candidate: Candidate;
  tests: TestCounts & { suites: TestSuite[] };
  warnings: string[];
  steps: QualityStep[];
  format?: { scope: string; include: string[] };
  error?: string;
}

export interface ReviewRecord {
  reviewer: string;
  verdict: string;
  candidateDigest: string;
  qualitySha256: string;
  summary: string;
  changes: string[];
  risks: string[];
}

export interface ToolResult {
  status: number | null;
  error?: Error;
  signal?: NodeJS.Signals | null;
}

export type ToolRunner = (command: string, args: string[], options: SpawnSyncOptions) => ToolResult;
