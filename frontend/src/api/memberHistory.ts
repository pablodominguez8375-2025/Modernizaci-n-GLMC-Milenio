/** Historia del hermano: la Secretaría del Taller propone (agrega o corrige) cargos y fechas masónicas; Régimen Interior aprueba. */

export type HistoryItemKind = 'office' | 'degree'
export type HistoryItemAction = 'add' | 'replace'
export type HistoryMilestone = 'initiation' | 'wage_increase' | 'exaltation'
export type HistoryProposalStatus = 'submitted' | 'approved' | 'observed' | 'rejected'

export const historyMilestones: { type: HistoryMilestone; label: string; degree: string }[] = [
  { type: 'initiation', label: 'Iniciación', degree: 'apprentice' },
  { type: 'wage_increase', label: 'Aumento de salario', degree: 'fellowcraft' },
  { type: 'exaltation', label: 'Exaltación', degree: 'master' },
]

export interface HistoryItem {
  kind: HistoryItemKind
  action: HistoryItemAction
  targetRecordId?: string | null
  officeType?: string | null
  period?: string | null
  startDate?: string | null
  endDate?: string | null
  eventType?: string | null
  eventDate?: string | null
}

export interface HistoryProposal {
  id: string
  organizationId: string
  memberId: string
  status: HistoryProposalStatus
  evidenceReference: string
  reason: string
  createdAtUtc: string
  reviewedAtUtc?: string | null
  reviewNotes?: string | null
  items: HistoryItem[]
}

export interface MemberHistorySnapshot {
  officeOptions: { code: string; name: string }[]
  proposals: HistoryProposal[]
}

export interface MemberHistoryRequest { evidenceReference: string; reason: string; items: HistoryItem[] }

export interface PendingHistoryItem extends HistoryItem {
  /** Valor vigente que la propuesta reemplaza (solo en correcciones). */
  before?: { officeType?: string; period?: string; startDate?: string | null; endDate?: string | null; eventType?: string; eventDate?: string } | null
}

export interface PendingHistoryProposal {
  id: string
  organizationId: string
  organizationName: string | null
  memberId: string
  memberName: string | null
  institutionalNumber: string | null
  evidenceReference: string
  reason: string
  createdAtUtc: string
  items: PendingHistoryItem[]
}

export const defaultOfficeOptions = [
  { code: 'worshipful_master', name: 'Venerable Maestro' },
  { code: 'immediate_past_master', name: 'Inmediato Ex Venerable Maestro' },
  { code: 'first_warden', name: 'Primer Vigilante' },
  { code: 'second_warden', name: 'Segundo Vigilante' },
  { code: 'orator', name: 'Orador' },
  { code: 'secretary', name: 'Secretario/a' },
  { code: 'hospitalaria', name: 'Hospitalaria' },
  { code: 'treasurer', name: 'Tesorero/a' },
  { code: 'first_deacon', name: 'Primer Diácono' },
  { code: 'second_deacon', name: 'Segundo Diácono' },
  { code: 'master_of_ceremonies', name: 'Maestro de Ceremonias' },
  { code: 'inner_guard', name: 'Guarda Templo Interno' },
  { code: 'outer_guard', name: 'Guarda Templo Externo' },
  { code: 'master_of_harmony', name: 'Maestro de Armonía' },
]

/* ------------------------------------------------------------------ Demostración (datos ficticios, en memoria) */

interface DemoOfficeChange { id: string; memberId: string; organizationId: string; officeType: string; period: string; startDate: string | null; endDate: string | null; evidenceReference: string }

type DemoRecord = HistoryProposal & { memberName: string; organizationName: string; institutionalNumber: string | null }

const proposals: DemoRecord[] = []
const addedOffices: DemoOfficeChange[] = []
const officeEdits = new Map<string, Partial<DemoOfficeChange>>()
const degreeDates = new Map<string, string>() // `${memberId}|${eventType}` → fecha
let sequence = 1

export function demoHistoryReset() { proposals.length = 0; addedOffices.length = 0; officeEdits.clear(); degreeDates.clear(); sequence = 1 }

export function demoHistorySeed(seed: { memberId: string; memberName: string; organizationId: string; organizationName: string; institutionalNumber: string | null; targetOfficeId: string | null; targetOfficeBefore: { officeType: string; period: string } | null }) {
  if (proposals.some(item => item.memberId === seed.memberId)) return
  proposals.push({
    id: `demo-history-${sequence++}`, organizationId: seed.organizationId, memberId: seed.memberId, status: 'submitted',
    evidenceReference: 'Acta de tenida N° 14 del Taller', reason: 'Se completa la historia del hermano al migrar el Cuadro del Taller.',
    createdAtUtc: '2026-10-28T15:00:00Z', memberName: seed.memberName, organizationName: seed.organizationName, institutionalNumber: seed.institutionalNumber,
    items: [
      { kind: 'degree', action: 'add', eventType: 'initiation', eventDate: '2009-03-14' },
      { kind: 'office', action: 'add', officeType: 'orator', period: '2018', startDate: '2018-01-01', endDate: '2018-12-31' },
    ],
  })
  void seed.targetOfficeId; void seed.targetOfficeBefore
}

export function demoHistoryList(organizationId: string, memberId: string): MemberHistorySnapshot {
  return { officeOptions: defaultOfficeOptions, proposals: proposals.filter(x => x.organizationId === organizationId && x.memberId === memberId).map(clone).reverse() }
}

export function demoHistoryPropose(organizationId: string, organizationName: string, memberId: string, memberName: string, institutionalNumber: string | null, request: MemberHistoryRequest): HistoryProposal {
  if (proposals.some(x => x.memberId === memberId && x.status === 'submitted')) throw new Error('Este hermano ya tiene una propuesta pendiente en Régimen Interior. Espere su resolución.')
  if (!request.items.length) throw new Error('Agregue al menos un cambio.')
  if (!request.evidenceReference.trim()) throw new Error('Indique la fuente o evidencia (acta, plancha, certificado).')
  if (request.reason.trim().length < 10) throw new Error('Indique el motivo (al menos 10 caracteres).')
  const record: DemoRecord = { id: `demo-history-${sequence++}`, organizationId, memberId, status: 'submitted', evidenceReference: request.evidenceReference.trim(), reason: request.reason.trim(), createdAtUtc: new Date().toISOString(), memberName, organizationName, institutionalNumber, items: request.items.map(x => ({ ...x })) }
  proposals.push(record)
  return clone(record)
}

export function demoHistoryPending(organizationId?: string): { total: number; items: PendingHistoryProposal[] } {
  const items = proposals.filter(x => x.status === 'submitted' && (!organizationId || x.organizationId === organizationId)).map(x => ({
    id: x.id, organizationId: x.organizationId, organizationName: x.organizationName, memberId: x.memberId, memberName: x.memberName,
    institutionalNumber: x.institutionalNumber, evidenceReference: x.evidenceReference, reason: x.reason, createdAtUtc: x.createdAtUtc,
    items: x.items.map(item => ({ ...item, before: item.action === 'replace' ? demoBefore(x.memberId, item) : null })),
  }))
  return { total: items.length, items }
}

export function demoHistoryResolve(id: string, decision: 'approve' | 'observe' | 'reject', notes?: string | null) {
  const record = proposals.find(x => x.id === id)
  if (!record) throw new Error('La propuesta indicada no existe.')
  if (record.status !== 'submitted') throw new Error('Solo una propuesta pendiente puede resolverse.')
  if (decision !== 'approve' && !notes?.trim()) throw new Error('La observación o el rechazo debe indicar el motivo.')
  record.reviewedAtUtc = new Date().toISOString(); record.reviewNotes = notes?.trim() || null
  if (decision !== 'approve') { record.status = decision === 'observe' ? 'observed' : 'rejected'; return }
  for (const item of record.items) {
    if (item.kind === 'degree') degreeDates.set(`${record.memberId}|${item.eventType}`, item.eventDate!)
    else if (item.action === 'add') addedOffices.push({ id: `demo-office-${sequence++}`, memberId: record.memberId, organizationId: record.organizationId, officeType: item.officeType!, period: item.period!, startDate: item.startDate ?? null, endDate: item.endDate ?? null, evidenceReference: record.evidenceReference })
    else officeEdits.set(item.targetRecordId!, { officeType: item.officeType!, period: item.period!, startDate: item.startDate ?? null, endDate: item.endDate ?? null, evidenceReference: record.evidenceReference })
  }
  record.status = 'approved'
}

/** Aplica a una ficha ficticia lo aprobado en la demostración (cargos agregados o corregidos y fechas de hitos). */
export function demoHistoryApplyToProfile<T extends {
  member: { id: string }
  offices: { id: string; officeType: string; period: string; organizationId: string; organization: string; startDate: string; endDate: string | null; evidenceReference: string | null }[]
  degreeEvents: { id: string; degree: string; eventType: string; effectiveDate: string; organizationId: string; organization: string; evidenceReference: string | null }[]
}>(profile: T, organizationName: string): T {
  const memberId = profile.member.id
  const offices = profile.offices.map(office => { const edit = officeEdits.get(office.id); return edit ? { ...office, ...edit, startDate: (edit.startDate ?? office.startDate) } as typeof office : office })
  for (const added of addedOffices.filter(x => x.memberId === memberId)) {
    offices.push({ id: added.id, officeType: added.officeType, period: added.period, organizationId: added.organizationId, organization: organizationName, startDate: added.startDate ?? '', endDate: added.endDate, evidenceReference: added.evidenceReference })
  }
  const degreeEvents = profile.degreeEvents.map(event => { const date = degreeDates.get(`${memberId}|${event.eventType}`); return date ? { ...event, effectiveDate: date } : event })
  for (const milestone of historyMilestones) {
    const date = degreeDates.get(`${memberId}|${milestone.type}`)
    if (date && !degreeEvents.some(event => event.eventType === milestone.type)) {
      degreeEvents.push({ id: `demo-degree-${memberId}-${milestone.type}`, degree: milestone.degree, eventType: milestone.type, effectiveDate: date, organizationId: offices[0]?.organizationId ?? '', organization: organizationName, evidenceReference: null })
    }
  }
  return { ...profile, offices, degreeEvents }
}

function demoBefore(memberId: string, item: HistoryItem): PendingHistoryItem['before'] {
  if (item.kind === 'degree') return { eventType: item.eventType ?? undefined, eventDate: degreeDates.get(`${memberId}|${item.eventType}`) }
  return null
}

function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T }

export function statusLabel(status: HistoryProposalStatus) {
  return status === 'submitted' ? 'Pendiente en Régimen Interior' : status === 'approved' ? 'Aprobada' : status === 'observed' ? 'Observada' : 'Rechazada'
}
