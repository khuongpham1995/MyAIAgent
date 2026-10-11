import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { readTrx } from './trx.mjs';
import { fixture, trx } from './test-fixtures.mjs';

test('TRX records passed, failed and skipped counts', t => {
  const root = fixture(t);
  writeFileSync(join(root, 'tests.trx'), trx({ total: 5, executed: 4, passed: 3, failed: 1, notExecuted: 1 }));
  assert.deepEqual(readTrx(root), { file: join(root, 'tests.trx'), outcome: 'Completed', total: 5, passed: 3, failed: 1, skipped: 1 });
});
for (const xml of [trx({ total: 0, executed: 0, passed: 0 }), trx({ passed: -1 }), trx({ passed: 1 }),
  trx().replace(' failed="0"', ''), trx().replace('</TestRun>', ''), '<!DOCTYPE TestRun>' + trx(),
  trx().replace('total="3"', 'total="3" total="3"')]) {
  test(`invalid TRX rejected: ${xml.slice(0, 110)}`, t => {
    const root = fixture(t);
    writeFileSync(join(root, 'tests.trx'), xml);
    assert.throws(() => readTrx(root));
  });
}
test('duplicate TRX files are not silently double counted', t => {
  const root = fixture(t);
  writeFileSync(join(root, 'one.trx'), trx());
  writeFileSync(join(root, 'two.trx'), trx());
  assert.throws(() => readTrx(root), /Expected one/);
});
