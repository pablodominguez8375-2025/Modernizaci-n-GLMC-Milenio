import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'

describe('system configuration demo contract', () => {
  it('loads the parameter catalog and creates a version in QA', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const initial = await api.getSystemSettings()
    expect(initial.items.some(item => item.code === 'system.publication.candidate.minimum_days')).toBe(true)
    expect(initial.items.some(item => item.code === 'system.permissions.lodge_venerable')).toBe(true)
    expect(initial.items.filter(item => item.category === 'Perfiles y permisos')).toHaveLength(10)
    const saved = await api.createSystemSettingVersion('system.publication.candidate.minimum_days', { value: '25', effectiveFrom: '2026-10-01', sourceReference: 'Acuerdo QA' })
    expect(saved.value).toBe('25')
    expect(saved.status).toBe('active')
  })

  it('versions the Venerable Maestro permission profile', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const saved = await api.createSystemSettingVersion('system.permissions.lodge_venerable', { value: 'Gestión del Taller|Aprobar egresos|Firmar documentos', effectiveFrom: '2026-11-01', sourceReference: 'Validación institucional QA' })
    expect(saved.value.split('|')).toContain('Aprobar egresos')
    expect(saved.sourceReference).toBe('Validación institucional QA')
  })
})
