import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'
import { diagnosticCsv, diagnosticJson, safeCsvCell } from './supportDiagnosticExport'

describe('soporte técnico · exportación segura', () => {
  it('neutraliza fórmulas y conserva comillas para CSV', () => {
    for (const payload of ['=SUM(1,2)', '+cmd', '-attack', '@import', '  =SHEET()', '\t+cmd', '\r=cmd']) {
      expect(safeCsvCell(payload)).toMatch(/^"'?/)
      expect(safeCsvCell(payload)).not.toBe('"' + payload + '"')
    }
    expect(safeCsvCell('x"y')).toBe('"x""y"')
  })

  it('exporta identificadores y rutas sin credenciales en demo', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const response = await api.getSupportDiagnostics()
    expect(response.volatileStorage).toBe(true)
    expect(response.items).toHaveLength(2)
    const csv = diagnosticCsv(response.items)
    expect(csv).toContain('Correlación')
    expect(csv).toContain('qa-synthetic-http-500')
    expect(csv).not.toContain('Bearer')
    const json = JSON.parse(diagnosticJson(response))
    expect(json.items).toHaveLength(2)
    expect(json.source).toBe('qa-demo-memory')
  })

  it('filtra por gravedad y ruta técnica', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const result = await api.getSupportDiagnostics({
      severity: 'error',
      route: 'gestion-logial',
    })
    expect(result.items).toHaveLength(1)
    expect(result.items[0].statusCode).toBe(500)
  })
})
