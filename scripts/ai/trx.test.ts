import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { readTrx } from './trx.ts';
import { fixture, trx } from './test-fixtures.ts';

test('TRX records passed, failed and skipped counts', t => {
  // Arrange
  const root = fixture(t);
  writeFileSync(join(root, 'tests.trx'), trx({ total: 5, executed: 4, passed: 3, failed: 1, notExecuted: 1 }));

  // Act & Assert
  assert.deepEqual(readTrx(root), { file: join(root, 'tests.trx'), outcome: 'Completed', total: 5, passed: 3, failed: 1, skipped: 1 });
});
test('xUnit skips, which VSTest does not count as notExecuted, are reported as skipped', t => {
  // Arrange
  const root = fixture(t);
  writeFileSync(join(root, 'tests.trx'), trx({ total: 100, executed: 90, passed: 90, notExecuted: 0 }));

  // Act & Assert
  assert.deepEqual(readTrx(root), { file: join(root, 'tests.trx'), outcome: 'Completed', total: 100, passed: 90, failed: 0, skipped: 10 });
});
test('failures are still counted when tests are also skipped', t => {
  // Arrange
  const root = fixture(t);
  writeFileSync(join(root, 'tests.trx'), trx({ total: 10, executed: 8, passed: 6, failed: 2 }));

  // Act & Assert
  assert.deepEqual(readTrx(root), { file: join(root, 'tests.trx'), outcome: 'Completed', total: 10, passed: 6, failed: 2, skipped: 2 });
});
for (const xml of [trx({ total: 3, executed: 3, passed: 3, notExecuted: 2 }), trx({ total: 2, executed: 3, passed: 3 })]) {
  test(`skipped counters that cannot be reconciled are rejected: ${xml.slice(30, 120)}`, t => {
    // Arrange
    const root = fixture(t);
    writeFileSync(join(root, 'tests.trx'), xml);

    // Act & Assert
    assert.throws(() => readTrx(root), /Inconsistent/);
  });
}
for (const xml of [trx({ total: 0, executed: 0, passed: 0 }), trx({ passed: -1 }), trx({ passed: 1 }),
  trx().replace(' failed="0"', ''), trx().replace('</TestRun>', ''), '<!DOCTYPE TestRun>' + trx(),
  trx().replace('total="3"', 'total="3" total="3"')]) {
  test(`invalid TRX rejected: ${xml.slice(0, 110)}`, t => {
    // Arrange
    const root = fixture(t);
    writeFileSync(join(root, 'tests.trx'), xml);

    // Act & Assert
    assert.throws(() => readTrx(root));
  });
}
test('duplicate TRX files are not silently double counted', t => {
  // Arrange
  const root = fixture(t);
  writeFileSync(join(root, 'one.trx'), trx());
  writeFileSync(join(root, 'two.trx'), trx());

  // Act & Assert
  assert.throws(() => readTrx(root), /Expected one/);
});
