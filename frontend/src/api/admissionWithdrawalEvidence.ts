import { affiliationModeForDate, chileCivilDate } from '../admissionDates'

export interface WithdrawalSignatureReviewRequest { status: 'approved' | 'observed' | 'rejected'; asOfDate: string; sourceReference: string; notes?: string | null; evidenceId: string }
export interface WithdrawalSignatureDecision { id: string; admissionCaseId: string; decisionType: string; status: string; asOfDate: string; sourceReference: string; notes: string | null; recordedBySubject: string; recordedAtUtc: string }
export interface WithdrawalLetterFixture { id: string; documentVersionId: string | null; evidenceDate: string | null; reviewStatus: string; reviewedAtUtc: string | null; createdAtUtc: string }
export interface WithdrawalCaseFixture { id: string; admissionType: string; affiliationMode: string | null; withdrawalLetterGrantedDate: string | null; status: string; createdAtUtc: string; letters: WithdrawalLetterFixture[]; decisions: WithdrawalSignatureDecision[] }
const prefix = 'withdrawal_letter_handwritten_signature:'
export function currentWithdrawalLetter(c: WithdrawalCaseFixture) {
  return [...c.letters].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc) || b.id.localeCompare(a.id))[0]
}
function validDate(c: WithdrawalCaseFixture, e: WithdrawalLetterFixture, today: string) {
  return e.evidenceDate !== null && affiliationModeForDate(e.evidenceDate, today) !== null &&
    (c.admissionType !== 'affiliation' || (["simple", "activation"].includes(c.affiliationMode ?? "") && c.withdrawalLetterGrantedDate === e.evidenceDate &&
      affiliationModeForDate(e.evidenceDate, chileCivilDate(new Date(c.createdAtUtc))) === c.affiliationMode))
}
export function verifiedWithdrawalSignature(c: WithdrawalCaseFixture, today = chileCivilDate()) {
  const e = currentWithdrawalLetter(c)
  if (!e?.documentVersionId || e.reviewStatus !== 'approved' || !e.reviewedAtUtc || !validDate(c, e, today)) return null
  const d = [...c.decisions].filter(d => d.decisionType === prefix + e.id).sort((a, b) => b.recordedAtUtc.localeCompare(a.recordedAtUtc) || b.id.localeCompare(a.id))[0]
  return d && d.status === 'approved' && d.asOfDate >= e.evidenceDate! && d.asOfDate <= today &&
    d.recordedAtUtc >= e.reviewedAtUtc && d.sourceReference.trim() ? d : null
}
export function reviewWithdrawalSignatureDemo(c: WithdrawalCaseFixture, request: WithdrawalSignatureReviewRequest, now = new Date()) {
  const today = chileCivilDate(now)
  if (c.status === 'resolved') throw new Error('El expediente ya está resuelto.')
  if (!request.evidenceId || !request.asOfDate || affiliationModeForDate(request.asOfDate, today) === null ||
      !request.sourceReference?.trim() || request.sourceReference.length > 500 || (request.notes?.length ?? 0) > 4000 ||
      !['approved', 'observed', 'rejected'].includes(request.status)) throw new Error('Indique carta, fecha de revisión y fuente institucional válidas.')
  const e = currentWithdrawalLetter(c)
  if (!e || e.id !== request.evidenceId || !e.documentVersionId) throw new Error('Debe revisar la carta trazable más reciente de este expediente.')
  if (request.status === 'approved' && (e.reviewStatus !== 'approved' || !e.reviewedAtUtc || !validDate(c, e, today) || request.asOfDate < e.evidenceDate!)) throw new Error('La carta debe estar aprobada con fecha acreditada y modalidad coherente.')
  const d: WithdrawalSignatureDecision = { id: crypto.randomUUID(), admissionCaseId: c.id, decisionType: prefix + e.id, status: request.status, asOfDate: request.asOfDate, sourceReference: request.sourceReference.trim(), notes: request.notes?.trim() || null, recordedBySubject: 'synthetic-grand-secretariat', recordedAtUtc: now.toISOString() }
  c.decisions.push(d)
  return { ...d }
}
/** Explicit synthetic review fixture; not an institutional permission or live file review. */
export function withdrawalReviewFixture(): WithdrawalCaseFixture {
  const now = new Date(); const today = chileCivilDate(now)
  return { id: 'demo-crv-review', admissionType: 'affiliation', affiliationMode: 'simple', withdrawalLetterGrantedDate: today, status: 'under_review', createdAtUtc: now.toISOString(), letters: [{ id: 'demo-crv-letter', documentVersionId: 'demo-crv-version', evidenceDate: today, reviewStatus: 'approved', reviewedAtUtc: new Date(now.getTime() - 1000).toISOString(), createdAtUtc: new Date(now.getTime() - 2000).toISOString() }], decisions: [] }
}
