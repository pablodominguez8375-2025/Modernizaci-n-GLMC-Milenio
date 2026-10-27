import { afterEach, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

afterEach(() => vi.unstubAllGlobals())

// Issue #754 QA HTTP 415: ASP.NET binds CreateUserAccountRequest only from application/json.
// Use a synthetic member and fake response; never create a live account or email.
it('posts user creation as application/json with bearer token, no cookies and a JSON body', async () => {
  const result = {
    subject: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    email: 'sintetico@example.invalid',
    memberId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
    organizationId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
    platformAdministrator: false,
    superuser: false,
    requiresPasswordChange: true,
    initialPasswordEmailSent: true,
  }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(result), {
    status: 201, headers: { 'Content-Type': 'application/json' },
  }))
  vi.stubGlobal('fetch', fetch)
  const request = { memberId: result.memberId, organizationId: result.organizationId }
  const client = new PmgmApiClient({ getAccessToken: async () => 'synthetic-token' })

  await expect(client.createUserAccount(request)).resolves.toMatchObject(result)

  expect(fetch).toHaveBeenCalledTimes(1)
  const [url, init] = fetch.mock.calls[0] as [string, RequestInit & { headers: Headers }]
  expect(url).toBe('/api/system/user-accounts')
  expect(init.method).toBe('POST')
  expect(init.headers.get('Content-Type')).toBe('application/json')
  expect(init.headers.get('Accept')).toBe('application/json')
  expect(init.headers.get('Authorization')).toBe('Bearer synthetic-token')
  expect(init).toMatchObject({ credentials: 'omit', redirect: 'error', cache: 'no-store' })
  expect(JSON.parse(init.body as string)).toEqual(request)
})

it('does not add Content-Type on a read-only eligibility GET', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({
    items: [], canCreateUser: true, canCreateSuperuser: false, deliveryConfigured: true,
  })))
  vi.stubGlobal('fetch', fetch)

  const client = new PmgmApiClient({ getAccessToken: async () => 'synthetic-token' })
  await client.getUserAccountCandidates()

  const [url, init] = fetch.mock.calls[0] as [string, RequestInit & { headers: Headers }]
  expect(url).toBe('/api/system/user-accounts/eligible-members')
  expect(init.method).toBeUndefined()
  expect(init.headers.has('Content-Type')).toBe(false)
  expect(init.body).toBeUndefined()
})
