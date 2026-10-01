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
  it('keeps the current publication policy when future versions are scheduled', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const code = 'system.publication.candidate.visible_fields'
    expect((await api.getSystemSettingVersions(code)).total).toBe(0)
    await api.createSystemSettingVersion(code, { value: 'Taller|nombre completo|fotografía', effectiveFrom: '2099-01-01', sourceReference: 'QA primera versión futura' })
    await api.createSystemSettingVersion(code, { value: 'Fotografía|Nombre completo|Taller', effectiveFrom: '2099-02-01', sourceReference: 'QA segunda versión futura' })
    const current = (await api.getSystemSettings()).items.find(item => item.code === code)!
    expect(current.value).toBe('Fotografía|Nombre completo|Taller')
    expect(current.sourceReference).toBe('Decisión aprobada REQ-025')
    expect(current.status).toBe('default')
    const history = (await api.getSystemSettingVersions(code)).items
    expect(history[1].effectiveTo).toBe('2099-01-31')
    expect(history).toHaveLength(2)
    await expect(api.createSystemSettingVersion(code, { value: current.value, effectiveFrom: '2099-01-15', sourceReference: 'QA retroactividad' })).rejects.toThrow('posterior')
  })

  it('rejects private fields, omissions and backdated publication policies', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const code = 'system.publication.candidate.visible_fields'
    for (const value of ['Fotografía|Nombre completo|Taller|RUT', 'Nombre completo|Taller', 'Fotografía|Fotografía|Taller'])
      await expect(api.createSystemSettingVersion(code, { value, effectiveFrom: '2099-01-01', sourceReference: 'QA inválida' })).rejects.toThrow('exclusivamente')
    await expect(api.createSystemSettingVersion(code, { value: 'Fotografía|Nombre completo|Taller', effectiveFrom: '2020-01-01', sourceReference: 'QA retroactiva' })).rejects.toThrow('adelante')
  })

})
