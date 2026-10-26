import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'
import { lodgeHealthErrorMessage, summarizeLodgeHealth } from './lodgeDataHealth'

const ok = (resource: string, relation: string) => ({ resource, relation, present: true, schemaUsable: true, selectGranted: true, requiredColumnsPresent: true })

describe('#688 estado de datos de Talleres', () => {
  it('agrupa por recurso y marca atención solo donde falla', () => {
    const rows = summarizeLodgeHealth([
      ok('miembros', 'core.memberships'), ok('tenidas', 'core.lodge_meetings'),
      { ...ok('instrucciones', 'core.lodge_instruction_sessions'), requiredColumnsPresent: false },
      { ...ok('miembros', 'core.people'), selectGranted: false },
    ])
    expect(rows.map(r => r.resource)).toEqual(['tenidas', 'instrucciones', 'miembros'])
    expect(rows[0].light).toBe('ok')
    expect(rows[1]).toMatchObject({ light: 'attention', problems: ['Faltan columnas en lodge_instruction_sessions'] })
    expect(rows[2].problems).toEqual(['La API no tiene permiso de lectura en people'])
  })

  it('explica tabla ausente sin mencionar columnas ni permisos', () => {
    const [row] = summarizeLodgeHealth([{ ...ok('tenidas', 'core.lodge_meetings'), present: false, selectGranted: false, requiredColumnsPresent: false }])
    expect(row.problems).toEqual(['Falta la tabla lodge_meetings'])
  })

  it('mensajes claros para 403 y 503, sin detalles internos', () => {
    expect(lodgeHealthErrorMessage(403)).toContain('no tiene permiso')
    expect(lodgeHealthErrorMessage(503)).toContain('no está disponible')
    expect(lodgeHealthErrorMessage(500)).not.toMatch(/sql|exception|stack/i)
  })

  it('la demo es ficticia y no expone datos personales', async () => {
    const response = await new PmgmApiClient({ useMocks: true }).getSupportLodgeDataHealth()
    expect(response.note).toContain('ficticios')
    expect(JSON.stringify(response)).not.toMatch(/@|rut|password/i)
  })
})

describe('#688 verificar lecturas del Taller', () => {
  it('ordena las lecturas y explica la que falla sin detalles internos', async () => {
    const { summarizeLodgeProbe, lodgeProbeErrorMessage } = await import('./lodgeDataHealth')
    const lines = summarizeLodgeProbe([{ resource: 'instrucciones', result: 'failed' }, { resource: 'tenidas', result: 'ok' }, { resource: 'miembros', result: 'ok' }])
    expect(lines.map(l => l.resource)).toEqual(['tenidas', 'instrucciones', 'miembros'])
    expect(lines[1]).toMatchObject({ ok: false, label: 'Instrucciones' })
    expect(lines[1].text).not.toMatch(/sql|exception|stack|sqlstate/i)
    expect(lines[0].ok).toBe(true)
    expect(lodgeProbeErrorMessage(404)).toContain('no existe')
    expect(lodgeProbeErrorMessage(403)).toContain('no tiene permiso')
  })
})
