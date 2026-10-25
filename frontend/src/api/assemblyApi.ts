// Padrón de la Gran Asamblea (Régimen Interior / Gran Secretaría). Contrato del servidor:
//   GET  /api/regimen-interior/asambleas
//   POST /api/regimen-interior/asambleas
//   GET  /api/regimen-interior/asambleas/{id}/listados
//   POST /api/regimen-interior/asambleas/{id}/talleres/{orgId}/bloqueo-saldo   (solo Régimen Interior)
//   POST /api/regimen-interior/asambleas/{id}/revision/{personId}/decision     (solo Régimen Interior)
// Todo es PRELIMINAR: el servidor nunca confirma sufragio ni cierra el padrón.

export interface AssemblySummary {
  id: string; name: string; meetingDate: string; cutoffDate: string; status: string
  provisional: boolean; officiallyClosed: boolean; mayVoteConfirmed: boolean
}
export interface AssemblyListResponse { items: AssemblySummary[]; pageSize: number }

export interface AssemblyListingRow {
  number: number; personId: string; organizationId: string; name: string; rut: string; workshop: string
  category: string; status: string; mayAttend: boolean; mayVote: boolean
  blockedByDebt: boolean; blockLifted: boolean; reasonCode: string; signature: string
}
export interface AssemblyVoterRow { number: number; personId: string; name: string; rut: string; workshop: string; signature: string }
export interface AssemblyWorkshopDebtRow { organizationId: string; workshop: string; members: number; affected: number; blockLifted: boolean }
export interface AssemblyListing {
  assemblyId: string; name: string; meetingDate: string; cutoffDate: string; status: string
  provisional: boolean; officiallyClosed: boolean; mayIssueBallots: boolean
  asambleistas: AssemblyListingRow[]; sufragantes: AssemblyVoterRow[]; talleresConBloqueo: AssemblyWorkshopDebtRow[]
}

export type AssemblyDebtAction = 'lift' | 'revoke'
export interface AssemblyDebtBlockPayload { action: AssemblyDebtAction; reason: string; evidenceReference: string }
export type AssemblyReviewAction = 'request_correction' | 'refer_validation' | 'confirm_ineligible' | 'attendance_only'
export interface AssemblyReviewPayload { action: AssemblyReviewAction; reason: string; evidenceReference: string }
export interface CreateAssemblyPayload { name: string; meetingDate: string; cutoffDate: string }

export const assemblyReviewActionLabels: Record<AssemblyReviewAction, string> = {
  request_correction: 'Solicitar corrección a la unidad responsable',
  refer_validation: 'Derivar validación',
  attendance_only: 'Habilitar solo para asistir',
  confirm_ineligible: 'Confirmar inhabilidad',
}

export function assemblyStatusLabel(row: Pick<AssemblyListingRow, 'status' | 'blockedByDebt' | 'blockLifted' | 'mayVote'>): string {
  if (row.blockLifted) return 'Habilitado (bloqueo levantado)'
  if (row.blockedByDebt) return 'Solo asistencia · saldo pendiente'
  if (row.status === 'habilitado') return row.mayVote ? 'Habilitado' : 'Habilitado para asistir'
  if (row.status === 'solo_asistencia') return 'Solo asistencia'
  if (row.status === 'inhabilitado') return 'Inhabilitado'
  return 'Pendiente de fuentes oficiales'
}

// ---- Demostración (datos ficticios; solo con mocks) ------------------------------------------------
const TALLER_MOROSO = 'demo-org-23'
const demoRows = [
  { id: 'a1', org: 'demo-org-1', last: 'Álvarez Soto', first: 'Luis', rut: '11.222.***-3', workshop: 'Taller Demostrativo Nº 1', category: 'Asambleísta permanente', status: 'habilitado', vote: true },
  { id: 'a2', org: TALLER_MOROSO, last: 'Bravo Núñez', first: 'Carla', rut: '12.333.***-4', workshop: 'Taller Demostrativo Nº 23', category: 'Representante', status: 'solo_asistencia', vote: false, debt: true },
  { id: 'a3', org: TALLER_MOROSO, last: 'Fuentes Ibáñez', first: 'Pedro', rut: '9.444.***-5', workshop: 'Taller Demostrativo Nº 23', category: 'Asambleísta permanente', status: 'solo_asistencia', vote: false, debt: true },
  { id: 'a4', org: 'demo-org-8', last: 'Muñoz Rojas', first: 'Ana', rut: '13.555.***-6', workshop: 'Taller Demostrativo Nº 8', category: 'Asambleísta permanente', status: 'inhabilitado', vote: false },
  { id: 'a5', org: 'demo-org-14', last: 'Ñanco Pérez', first: 'Marta', rut: '10.666.***-7', workshop: 'Taller Demostrativo Nº 14', category: 'Asambleísta permanente', status: 'pendiente', vote: false },
  { id: 'a6', org: 'demo-org-14', last: 'Zúñiga Vega', first: 'Jorge', rut: '14.777.***-8', workshop: 'Taller Demostrativo Nº 14', category: 'Representante', status: 'habilitado', vote: true },
] as const

export class MockAssemblyStore {
  private lifted = new Set<string>()
  private assemblies: AssemblySummary[] = [{
    id: 'demo-asamblea-2026', name: 'Gran Asamblea Ordinaria 2026 (datos ficticios)', meetingDate: '2026-11-21', cutoffDate: '2026-10-19',
    status: 'draft', provisional: true, officiallyClosed: false, mayVoteConfirmed: false,
  }]
  list(): AssemblyListResponse { return { items: this.assemblies.map(a => ({ ...a })), pageSize: 50 } }
  create(payload: CreateAssemblyPayload): AssemblySummary {
    const name = payload.name.trim()
    if (!name || name.length > 240 || !payload.meetingDate || !payload.cutoffDate || payload.cutoffDate > payload.meetingDate)
      throw new Error('Indique un nombre y fechas válidas: el corte no puede ser posterior a la Asamblea.')
    const created: AssemblySummary = { id: crypto.randomUUID(), name, meetingDate: payload.meetingDate, cutoffDate: payload.cutoffDate, status: 'draft', provisional: true, officiallyClosed: false, mayVoteConfirmed: false }
    this.assemblies = [created, ...this.assemblies]
    return created
  }
  listing(assemblyId: string): AssemblyListing {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    const isDemo = assembly.id === 'demo-asamblea-2026'
    const rows = isDemo ? [...demoRows] : []
    const sorted = rows.sort((a, b) => a.last.localeCompare(b.last, 'es-CL') || a.first.localeCompare(b.first, 'es-CL'))
    const asambleistas: AssemblyListingRow[] = sorted.map((r, i) => {
      const debt = 'debt' in r && r.debt === true
      const lifted = debt && this.lifted.has(r.org)
      return {
        number: i + 1, personId: r.id, organizationId: r.org, name: `${r.last}, ${r.first}`, rut: r.rut, workshop: r.workshop,
        category: r.category, status: lifted ? 'habilitado' : r.status, mayAttend: r.status !== 'inhabilitado' && r.status !== 'pendiente',
        mayVote: r.vote || lifted, blockedByDebt: debt && !lifted, blockLifted: lifted,
        reasonCode: debt ? 'MOROSIDAD_GRAN_TESORERIA' : r.status === 'pendiente' ? 'OFFICIAL_SOURCES_PENDING' : 'HABILITACION_VERIFICADA', signature: '',
      }
    })
    const sufragantes = asambleistas.filter(r => r.mayVote).map((r, i) => ({ number: i + 1, personId: r.personId, name: r.name, rut: r.rut, workshop: r.workshop, signature: '' }))
    const talleres = new Map<string, AssemblyWorkshopDebtRow>()
    for (const r of asambleistas.filter(x => x.blockedByDebt || x.blockLifted)) {
      const t = talleres.get(r.organizationId) ?? { organizationId: r.organizationId, workshop: r.workshop, members: asambleistas.filter(x => x.organizationId === r.organizationId).length, affected: 0, blockLifted: r.blockLifted }
      t.affected += 1; talleres.set(r.organizationId, t)
    }
    return { assemblyId: assembly.id, name: assembly.name, meetingDate: assembly.meetingDate, cutoffDate: assembly.cutoffDate, status: assembly.status, provisional: true, officiallyClosed: false, mayIssueBallots: false, asambleistas, sufragantes, talleresConBloqueo: [...talleres.values()] }
  }
  decideDebtBlock(organizationId: string, payload: AssemblyDebtBlockPayload): void {
    if (payload.reason.trim().length < 10 || !payload.evidenceReference.trim()) throw new Error('Indique el motivo (mínimo 10 caracteres) y la referencia del acta.')
    if (payload.action === 'lift') this.lifted.add(organizationId); else this.lifted.delete(organizationId)
  }
}
