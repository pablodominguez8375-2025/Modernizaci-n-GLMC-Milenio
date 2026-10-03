import { afterEach, expect, it, vi } from 'vitest'
import { currentWithdrawalLetter, reviewWithdrawalSignatureDemo, verifiedWithdrawalSignature, type WithdrawalCaseFixture, type WithdrawalSignatureReviewRequest } from './admissionWithdrawalEvidence'
import { PmgmApiClient } from './pmgmApi'
afterEach(() => vi.unstubAllGlobals())
const now = new Date('2026-10-03T15:00:00.000Z')
const input: WithdrawalSignatureReviewRequest = { evidenceId: 'letter', status: 'approved', asOfDate: '2026-10-03', sourceReference: 'SYNTHETIC ORIGINAL REVIEW' }
function fixture(): WithdrawalCaseFixture { return { id: 'case', admissionType: 'affiliation', affiliationMode: 'simple', withdrawalLetterGrantedDate: '2026-10-01', status: 'under_review', createdAtUtc: now.toISOString(), letters: [{ id: 'letter', documentVersionId: 'version', evidenceDate: '2026-10-01', reviewStatus: 'approved', reviewedAtUtc: '2026-10-02T15:00:00.000Z', createdAtUtc: '2026-10-01T15:00:00.000Z' }], decisions: [] } }
it('vincula la revisión a carta/version y la invalida tras reemplazo o nueva revisión', () => {
  const c = fixture(); const d = reviewWithdrawalSignatureDemo(c, input, now)
  expect(d.decisionType).toBe('withdrawal_letter_handwritten_signature:letter')
  expect(verifiedWithdrawalSignature(c, '2026-10-03')?.id).toBe(d.id)
  c.letters[0].reviewedAtUtc = '2026-10-03T15:00:01.000Z'
  expect(verifiedWithdrawalSignature(c, '2026-10-03')).toBeNull()
  c.letters.push({ ...c.letters[0], id: 'replacement', reviewStatus: 'rejected', createdAtUtc: '2026-10-03T14:00:00.000Z' })
  expect(currentWithdrawalLetter(c)?.id).toBe('replacement'); expect(verifiedWithdrawalSignature(c, '2026-10-03')).toBeNull()
  expect(() => reviewWithdrawalSignatureDemo(c, input, now)).toThrow()
})
it.each(['pending', 'date', 'mode', 'version', 'resolved', 'future', 'source', 'missing_id', 'before_grant'])('no guarda aprobación sin respaldo: %s', scenario => {
  const c = fixture(); const request = { ...input }
  if (scenario === 'pending') c.letters[0].reviewStatus = 'pending'
  if (scenario === 'date') c.letters[0].evidenceDate = null
  if (scenario === 'mode') c.affiliationMode = 'activation'
  if (scenario === 'version') c.letters[0].documentVersionId = null
  if (scenario === 'resolved') c.status = 'resolved'
  if (scenario === 'future') request.asOfDate = '2026-10-04'
  if (scenario === 'source') request.sourceReference = ' '
  if (scenario === 'missing_id') request.evidenceId = ''
  if (scenario === 'before_grant') request.asOfDate = '2026-09-30'
  expect(() => reviewWithdrawalSignatureDemo(c, request, now)).toThrow(); expect(c.decisions).toEqual([])
})
it('no acredita legado sin referencia ni recupera aprobación tras rechazo posterior', () => {
  const c = fixture(); const d = reviewWithdrawalSignatureDemo(c, input, now)
  d.decisionType = 'withdrawal_letter_handwritten_signature'; c.decisions = [d]
  expect(verifiedWithdrawalSignature(c, '2026-10-03')).toBeNull()
  c.decisions = []; reviewWithdrawalSignatureDemo(c, input, now)
  reviewWithdrawalSignatureDemo(c, { ...input, status: 'rejected' }, new Date(now.getTime() + 1000))
  expect(verifiedWithdrawalSignature(c, '2026-10-03')).toBeNull()
})
it('transporta referencia sin cache y no simula expedientes ajenos', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'decision' }))); vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'synthetic' }).reviewWithdrawalLetterSignature('case', input)
  expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/expedientes/case/verificaciones/carta-retiro-firma-manuscrita')
  expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual(input); expect(fetch.mock.calls[0][1].cache).toBe('no-store')
  await expect(new PmgmApiClient({ useMocks: true }).reviewWithdrawalLetterSignature('other-case', input)).rejects.toThrow('sintético')
})
