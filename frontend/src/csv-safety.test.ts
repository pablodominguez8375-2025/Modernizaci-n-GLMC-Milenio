import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { buildCsv } from './listing'

describe('Seguridad · exportaciones CSV sin inyección de fórmulas (04-10-2026)', () => {
  it('neutraliza textos que empiezan con =, +, -, @ y deja los números intactos', () => {
    const csv = buildCsv([{ header: 'Nombre', value: (row: { name: string }) => row.name }, { header: 'Monto', value: (row: { amount: number }) => row.amount }],
      [{ name: '=HYPERLINK("http://x")', amount: -1500 }, { name: '@SUM(A1)', amount: 21000 }, { name: 'Normal', amount: 0 }])
    const lines = csv.replace('\uFEFF', '').split('\r\n')
    expect(lines[1]).toBe(`"'=HYPERLINK(""http://x"")";-1500`)
    expect(lines[2]).toBe("'@SUM(A1);21000")
    expect(lines[3]).toBe('Normal;0')
  })

  it('la bitácora de eventos también neutraliza fórmulas', () => {
    expect(readFileSync(new URL('./AuditableEventLog.tsx', import.meta.url), 'utf8')).toContain("/^[=+\\-@\\t\\r]/.test(t)")
  })
})
