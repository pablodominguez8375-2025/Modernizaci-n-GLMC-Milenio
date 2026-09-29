import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

// Decisión PO 29-09-2026 (PMGM-ADR-014): lema oficial del Centenario.
describe('lema institucional', () => {
  const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
  it('usa el lema oficial «Camino al centenario 1929-2029»', () => {
    expect(app).toContain('Camino al centenario 1929-2029')
    expect(app).not.toContain('100 años de historia')
  })
  it('theme-color usa el azul institucional oficial', () => {
    const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8')
    expect(html).toContain('content="#06148E"')
  })
})
