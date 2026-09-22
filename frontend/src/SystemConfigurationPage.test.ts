import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'

describe('system configuration demo contract', () => {
  it('loads the parameter catalog and creates a version in QA', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const initial = await api.getSystemSettings()
    expect(initial.items.some(item => item.code === 'system.publication.candidate.minimum_days')).toBe(true)
    expect(initial.items.some(item => item.code === 'system.permissions.lodge_venerable')).toBe(true)
    expect(initial.items.filter(item => item.category === 'Perfiles y permisos')).toHaveLength(10)
    const saved = await api.createSystemSettingVersion('system.publication.candidate.minimum_days', { value: '25', effectiveFrom: '2026-09-01', sourceReference: 'Acuerdo QA' })
    expect(saved.value).toBe('25')
    expect(saved.status).toBe('active')
  })

  it('uses the four institutional colors approved in the master baseline', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const settings = await api.getSystemSettings()
    const values = new Map(settings.items.filter(item => item.code.startsWith('system.brand.') && item.code.endsWith('_color')).map(item => [item.code, item.value]))
    expect(values.get('system.brand.primary_color')).toBe('#06148E')
    expect(values.get('system.brand.secondary_color')).toBe('#004AD4')
    expect(values.get('system.brand.gold_color')).toBe('#F3C609')
    expect(values.get('system.brand.accent_color')).toBe('#FBAE17')
  })

  it('versions the Venerable Maestro permission profile', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const saved = await api.createSystemSettingVersion('system.permissions.lodge_venerable', { value: 'Gestión del Taller|Aprobar egresos|Firmar documentos', effectiveFrom: '2026-11-01', sourceReference: 'Validación institucional QA' })
    expect(saved.value.split('|')).toContain('Aprobar egresos')
    expect(saved.sourceReference).toBe('Validación institucional QA')
    const history = await api.getSystemSettingVersions('system.permissions.lodge_venerable')
    expect(history.total).toBe(2)
    expect(history.items[0].value).toContain('Aprobar egresos')
  })

  it('marks future changes as scheduled', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const saved = await api.createSystemSettingVersion('system.security.session_minutes', { value: '45', effectiveFrom: '2099-01-01', sourceReference: 'Cambio futuro QA' })
    expect(saved.status).toBe('scheduled')
  })
})
