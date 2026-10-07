import { afterEach, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { OrganizationProfileApiClient } from './organizationProfileApi'

afterEach(() => vi.unstubAllGlobals())
it('keeps profile, zone consultation and tariff aligned after each Oriente edit', async () => {
  vi.stubGlobal('fetch', vi.fn())
  const api = new PmgmApiClient({ useMocks: true })
  api.demoAccessSubject='demo:treasury'
  const profiles = new OrganizationProfileApiClient({ useMocks: true })
  const id = '23232323-2323-2323-2323-232323232323'
  for (const [orienteCode, city, country, zone, currency] of [
    ['santiago', 'Santiago', 'Chile', 'santiago', 'CLP'],
    ['other_chile', 'Concepción', 'Chile', 'other_oriente', 'CLP'],
    ['peru', 'Lima', 'Perú', 'peru', 'USD'],
  ]) {
    await profiles.updateWorkshopMetadata(id, { name: 'Taller QA', establishedOn: null, orienteCode, city, country })
    const profile = await profiles.getProfile(id)
    expect(profile.organization.treasuryTerritory).toBe(zone)
    expect(profile.quotaDecreeNumber).toBe('1.759')
    expect((await api.getTreasuryTerritory(id)).territory).toBe(zone)
    expect((await api.getTreasuryTerritories()).items.find(x => x.id === id)?.treasuryTerritory).toBe(zone)
    const schedule = await api.getOfficialFeeSchedule(id)
    expect(schedule.territory).toBe(zone)
    expect(schedule.items.every(x => x.currency === currency)).toBe(true)
  }
  expect(fetch).not.toHaveBeenCalled()
  expect('setTreasuryTerritory' in api).toBe(false)
  await profiles.updateWorkshopMetadata(id, { name: 'Taller QA', establishedOn: null, orienteCode: 'other_chile', city: 'Valparaíso', country: 'Chile' })
})
