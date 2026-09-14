import { afterEach, describe, expect, it, vi } from 'vitest'
import { BootstrapApiClient, type InstitutionalBootstrapRequest } from './bootstrapApi'

const request: InstitutionalBootstrapRequest = {
  packageKey: 'glmch-pilot-v1',
  packageVersion: 1,
  institution: { name: 'Gran Logia Mixta de Chile' },
  workshops: [{ name: 'Respetable Logia Libertad Nº 23', number: '23' }],
}

afterEach(() => vi.restoreAllMocks())

describe('BootstrapApiClient', () => {
  it('uses bearer auth, no-store and same API route for dry-run', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ valid: true, alreadyApplied: false, payloadSha256: 'abc', errors: [], changes: {}, catalog: { securityProfiles: [], officeDefinitions: [] } }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    const client = new BootstrapApiClient({ baseUrl: '', getAccessToken: async () => 'token-test' })

    await client.plan(request)

    expect(fetchMock).toHaveBeenCalledOnce()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/platform/bootstrap/plan')
    expect(init?.credentials).toBe('omit')
    expect(init?.cache).toBe('no-store')
    expect(init?.redirect).toBe('error')
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer token-test')
  })

  it('rejects operation without an access token', async () => {
    const client = new BootstrapApiClient({ baseUrl: '', getAccessToken: async () => null })
    await expect(client.plan(request)).rejects.toThrow(/Superadmin/)
  })

  it('mock mode is idempotent and never calls the network', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
    const client = new BootstrapApiClient({ useMocks: true })
    const plan = await client.plan(request)
    expect(plan.valid).toBe(true)
    const applied = await client.apply(request)
    expect(applied.applied).toBe(true)
    expect(applied.alreadyApplied).toBe(false)
    const repeated = await client.apply(request)
    expect(repeated.alreadyApplied).toBe(true)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
