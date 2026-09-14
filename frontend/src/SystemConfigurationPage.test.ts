import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'

describe('system configuration demo contract', () => {
  it('loads the parameter catalog and creates a version in QA', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const initial = await api.getSystemSettings()
    expect(initial.items.some(item => item.code === 'system.publication.candidate.minimum_days')).toBe(true)
    const saved = await api.createSystemSettingVersion('system.publication.candidate.minimum_days', { value: '25', effectiveFrom: '2026-10-01', sourceReference: 'Acuerdo QA' })
    expect(saved.value).toBe('25')
    expect(saved.status).toBe('active')
  })
})
