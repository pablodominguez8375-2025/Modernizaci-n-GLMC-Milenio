import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('Vistas sin botón superior «Imprimir vista» (decisión PO 08-10-2026)', () => {
  it('ni la cabecera de vistas ni Tesorería del Taller lo muestran', () => {
    for (const file of ['App.tsx', 'LodgeTreasuryPanel.tsx']) {
      expect(readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')).not.toContain('Imprimir vista')
    }
  })
})
