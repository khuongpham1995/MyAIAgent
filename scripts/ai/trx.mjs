import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

// Read the fixed counters emitted by VSTest, not arbitrary XML. Unsupported or
// incomplete output fails closed; no DTD/entity resolution or XML dependency.
export function readTrx(directory) {
  const files = readdirSync(directory).filter(name => name.endsWith('.trx'));
  if (files.length !== 1) throw new Error(`Expected one fresh TRX in ${directory}, found ${files.length}.`);
  const file = join(directory, files[0]);
  const xml = readFileSync(file, 'utf8');
  const summaries = [...xml.matchAll(/<ResultSummary\b[^>]*>([\s\S]*?)<\/ResultSummary>/g)];
  if (/<!DOCTYPE|<!ENTITY/i.test(xml) || summaries.length !== 1 || !xml.trimEnd().endsWith('</TestRun>')) {
    throw new Error(`Unsupported or incomplete TRX: ${file}`);
  }
  const outcome = summaries[0][0].match(/^<ResultSummary\b[^>]*\boutcome="([^"]+)"/)?.[1];
  if (!['Completed', 'Passed', 'Failed', 'Error', 'Timeout', 'Aborted'].includes(outcome)) {
    throw new Error(`Unsupported TRX outcome: ${file}`);
  }
  const counters = [...summaries[0][1].matchAll(/<Counters\b([^>]*?)\/>/g)];
  if (counters.length !== 1) throw new Error(`Missing or duplicate TRX counters: ${file}`);
  const values = {};
  for (const [, key, value] of counters[0][1].matchAll(/(\w+)="([^"]*)"/g)) {
    if (Object.hasOwn(values, key) || !/^\d+$/.test(value) || !Number.isSafeInteger(Number(value))) {
      throw new Error(`Invalid TRX counter ${key}: ${file}`);
    }
    values[key] = Number(value);
  }
  const required = ['total', 'executed', 'passed', 'failed', 'notExecuted', 'error', 'timeout', 'aborted',
    'inconclusive', 'passedButRunAborted', 'notRunnable', 'disconnected', 'warning', 'inProgress', 'pending'];
  if (required.some(key => !Object.hasOwn(values, key))) throw new Error(`Incomplete TRX counters: ${file}`);
  const failed = values.failed + values.error + values.timeout + values.aborted + values.inconclusive +
    values.passedButRunAborted + values.notRunnable + values.disconnected + values.warning + values.inProgress + values.pending;
  // xUnit skips are not executed but VSTest leaves notExecuted at 0 for them, so skipped is total minus executed.
  const skipped = values.total - values.executed;
  if (values.total <= 0 || skipped < 0 || values.notExecuted > skipped ||
      values.executed !== values.passed + failed) throw new Error(`Inconsistent or empty TRX: ${file}`);
  return { file, outcome, total: values.total, passed: values.passed, failed, skipped };
}
