// Padrón de la Gran Asamblea (Régimen Interior / Gran Secretaría). Contrato del servidor:
//   GET  /api/regimen-interior/asambleas
//   POST /api/regimen-interior/asambleas
//   GET  /api/regimen-interior/asambleas/{id}/listados
//   POST /api/regimen-interior/asambleas/{id}/talleres/{orgId}/bloqueo-saldo   (solo Régimen Interior)
//   POST /api/regimen-interior/asambleas/{id}/revision/{personId}/decision     (solo Régimen Interior)
//   GET  /api/regimen-interior/asambleas/{id}/cierre/estado                    (informe «Qué falta para cerrar»)
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

export type AssemblyReadinessState = 'ok' | 'pending' | 'blocked'
export interface AssemblyReadinessItem { code: string; title: string; state: AssemblyReadinessState; detail: string; count: number | null }
export interface AssemblyClosureReadiness {
  assemblyId: string; name: string; status: string; meetingDate: string; cutoffDate: string
  mayClose: boolean; provisional: boolean; items: AssemblyReadinessItem[]; blockingCodes: string[]
  closedAtUtc?: string | null; closureDigest?: string | null
}
export type AssemblyBalanceSource = 'gran_tesoreria' | 'gran_hospitalaria'
export interface AssemblyBalancesPayload { source?: AssemblyBalanceSource; sourceReference: string; currency: string; items: { organizationId: string; amountPending: number }[] }
export interface AssemblyBalancesOverviewItem {
  source?: AssemblyBalanceSource | null
  id: string; name: string; cutoffDate: string; meetingDate: string
  workshops: { organizationId: string; name: string; amountPending: number | null; currency: string | null; sourceReference: string | null }[]
}
export type AssemblyOfficialList = 'asambleistas' | 'sufragantes'
export interface AssemblyCloseResult { assemblyId: string; closed: boolean; closedAtUtc: string; voters: number; digest: string }
export interface AssemblyEvaluateResult { assemblyId: string; evaluated: number; appended: number; counts: Record<string, number> }
export const assemblyReadinessStateLabel: Record<AssemblyReadinessState, string> = { ok: 'Listo', pending: 'Pendiente', blocked: 'Bloqueado' }

export type AssemblyAttestationKind = 'membership' | 'master_periods' | 'honor_coverage' | 'regimen_coverage'
export interface AssemblyAttestationStatus { kind: AssemblyAttestationKind; title: string; attested: boolean; actaReference: string | null; statement: string | null; recordedAtUtc: string | null }
export interface AssemblyAttestationList { assemblyId: string; items: AssemblyAttestationStatus[] }
export interface AssemblyAttestationPayload { kind: AssemblyAttestationKind; action: 'attest' | 'revoke'; actaReference: string; statement: string }
/** Qué constancias de Régimen Interior respaldan cada paso del informe. */
export const assemblyAttestationsByStep: Record<string, AssemblyAttestationKind[]> = {
  master_terms: ['master_periods'], membership: ['membership'], rights: ['honor_coverage', 'regimen_coverage'],
}
export const assemblyAttestationTitles: Record<AssemblyAttestationKind, string> = {
  membership: 'Calidad de miembro activo', master_periods: 'Períodos de Venerable Maestro',
  honor_coverage: 'Cobertura del Tribunal de Honor', regimen_coverage: 'Cobertura de Régimen Interior',
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

/** Calidad por la que el asambleísta puede asistir (regla de asambleístas). */
export function assemblyCategoryLabel(category: string): string {
  if (category === 'permanent_assembly_member') return 'Ex Venerable Maestro'
  if (category === 'representative') return 'Venerable Maestro (primer período)'
  return category
}

// ---- Demostración (datos ficticios; solo con mocks) ------------------------------------------------
const TALLER_MOROSO = 'demo-org-23'
const DEMO_WORKSHOPS = ['demo-org-1', 'demo-org-23', 'demo-org-8', 'demo-org-14']
const demoRows = [
  { id: 'a1', org: 'demo-org-1', last: 'Álvarez Soto', first: 'Luis', rut: '11.222.***-3', workshop: 'Taller Demostrativo Nº 1', category: 'permanent_assembly_member', status: 'habilitado', vote: true },
  { id: 'a2', org: TALLER_MOROSO, last: 'Bravo Núñez', first: 'Carla', rut: '12.333.***-4', workshop: 'Taller Demostrativo Nº 23', category: 'representative', status: 'solo_asistencia', vote: false, debt: true },
  { id: 'a3', org: TALLER_MOROSO, last: 'Fuentes Ibáñez', first: 'Pedro', rut: '9.444.***-5', workshop: 'Taller Demostrativo Nº 23', category: 'permanent_assembly_member', status: 'solo_asistencia', vote: false, debt: true },
  { id: 'a4', org: 'demo-org-8', last: 'Muñoz Rojas', first: 'Ana', rut: '13.555.***-6', workshop: 'Taller Demostrativo Nº 8', category: 'permanent_assembly_member', status: 'inhabilitado', vote: false },
  { id: 'a5', org: 'demo-org-14', last: 'Ñanco Pérez', first: 'Marta', rut: '10.666.***-7', workshop: 'Taller Demostrativo Nº 14', category: 'permanent_assembly_member', status: 'pendiente', vote: false },
  { id: 'a6', org: 'demo-org-14', last: 'Zúñiga Vega', first: 'Jorge', rut: '14.777.***-8', workshop: 'Taller Demostrativo Nº 14', category: 'representative', status: 'habilitado', vote: true },
] as const

export class MockAssemblyStore {
  private lifted = new Set<string>()
  private balances = new Map<string, Map<string, number>>()
  private evaluatedIds = new Set<string>()
  private closedIds = new Map<string, { at: string; digest: string }>()
  private attestations = new Map<string, Map<AssemblyAttestationKind, { actaReference: string; statement: string; recordedAtUtc: string }>>()
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
  readiness(assemblyId: string): AssemblyClosureReadiness {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    const demo = assembly.id === 'demo-asamblea-2026'
    const members = demo ? demoRows.length : 0
    const evaluated = this.evaluatedIds.has(assemblyId)
    const pendingEval = demo && !evaluated ? demoRows.filter(r => r.status === 'pendiente').length : 0
    const att = this.attestations.get(assemblyId) ?? new Map()
    const has = (k: AssemblyAttestationKind) => att.has(k)
    const both = (o: string) => this.balances.get(`${assemblyId}|gran_tesoreria`)?.has(o) && this.balances.get(`${assemblyId}|gran_hospitalaria`)?.has(o)
    const bal = { size: DEMO_WORKSHOPS.filter(o => both(o)).length }
    const closedInfo = this.closedIds.get(assemblyId)
    const items: AssemblyReadinessItem[] = [
      { code: 'assembly', title: 'Asamblea y fecha de corte', state: 'ok', detail: `Corte al ${assembly.cutoffDate.split('-').reverse().join('-')}, reunión el ${assembly.meetingDate.split('-').reverse().join('-')}.`, count: null },
      demo
        ? { code: 'master_terms', title: 'Períodos de Venerable Maestro', state: has('master_periods') ? 'ok' : 'pending', detail: has('master_periods') ? 'Régimen Interior certificó los períodos de los 9 Venerables Maestros con su acta.' : 'Se encontraron 9 hermanos con cargo de Venerable Maestro. Falta la constancia oficial de cada período (acta).', count: 9 }
        : { code: 'master_terms', title: 'Períodos de Venerable Maestro', state: 'blocked', detail: 'No hay cargos de Venerable Maestro registrados en las fichas. Primero deben cargarse (Carga inicial).', count: 0 },
      { code: 'membership', title: 'Calidad de miembro activo', state: has('membership') ? 'ok' : 'pending', detail: has('membership') ? 'Régimen Interior certificó la calidad de miembro activo de los asambleístas al corte, con su acta.' : 'Falta la constancia de Régimen Interior sobre la calidad de miembro activo al corte (acta).', count: null },
      { code: 'roster', title: 'Asambleístas acreditados', state: members > 0 ? 'ok' : 'pending', detail: members > 0 ? `${members} asambleístas acreditados para esta Asamblea.` : 'Todavía no se han acreditado asambleístas.', count: members },
      { code: 'evaluation', title: 'Evaluación de habilitación', state: members > 0 && pendingEval === 0 ? 'ok' : 'pending', detail: members === 0 ? 'No hay asambleístas para evaluar.' : pendingEval === 0 ? `Los ${members} asambleístas tienen una evaluación vigente.` : `${pendingEval} de ${members} asambleístas siguen pendientes de evaluación.`, count: pendingEval },
      { code: 'grand_treasury', title: 'Saldos de Gran Tesorería y Gran Hospitalaria por Taller', state: demo && (bal?.size ?? 0) === DEMO_WORKSHOPS.length ? 'ok' : 'pending',
        detail: !demo ? 'Se registra cuando haya asambleístas acreditados.' : (bal?.size ?? 0) === DEMO_WORKSHOPS.length ? `Los ${DEMO_WORKSHOPS.length} Talleres tienen su saldo validado por Gran Tesorería y Gran Hospitalaria.` : `${bal?.size ?? 0} de ${DEMO_WORKSHOPS.length} Talleres tienen su saldo validado por Gran Tesorería y Gran Hospitalaria; cada uno lo registra desde su menú.`, count: demo ? DEMO_WORKSHOPS.length - (bal?.size ?? 0) : 0 },
      { code: 'rights', title: 'Tribunal de Honor y Régimen Interior', state: has('honor_coverage') && has('regimen_coverage') ? 'ok' : 'pending',
        detail: has('honor_coverage') && has('regimen_coverage') ? 'Ambas fuentes están certificadas como completas al corte (sin restricciones registradas).'
          : has('honor_coverage') ? 'Falta la constancia de cobertura completa de Régimen Interior al corte (sin restricciones registradas).'
          : has('regimen_coverage') ? 'Falta la constancia de cobertura completa del Tribunal de Honor al corte (sin restricciones registradas).'
          : 'Falta la declaración de cobertura completa de ambas fuentes al corte (sin restricciones registradas).', count: 0 },
    ]
    const allOk = !closedInfo && items.every(i => i.state === 'ok')
    items.push(closedInfo
      ? { code: 'closure', title: 'Cierre del padrón', state: 'ok', detail: `Padrón cerrado el ${new Date(closedInfo.at).toLocaleDateString('es-CL')}. Ya no admite cambios.`, count: null }
      : { code: 'closure', title: 'Cierre del padrón', state: allOk ? 'pending' : 'blocked', detail: allOk ? 'Todo está listo: Régimen Interior puede cerrar el padrón. El sistema vuelve a verificar todo al cerrar.' : 'El cierre se habilita cuando todos los pasos anteriores estén completos y certificados.', count: null })
    return { assemblyId, name: assembly.name, status: closedInfo ? 'closed' : assembly.status, meetingDate: assembly.meetingDate, cutoffDate: assembly.cutoffDate, mayClose: allOk, provisional: !closedInfo, items,
      blockingCodes: closedInfo ? [] : items.filter(i => i.state !== 'ok').map(i => i.code), closedAtUtc: closedInfo?.at ?? null, closureDigest: closedInfo?.digest ?? null }
  }
  accredit(assemblyId: string): void {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    if (assembly.id !== 'demo-asamblea-2026') throw new Error('No hay cargos de Venerable Maestro registrados en las fichas. Primero deben cargarse (Carga inicial).')
    throw new Error('Los asambleístas de esta Asamblea ya están acreditados.')
  }
  balancesOverview(source: AssemblyBalanceSource = 'gran_tesoreria'): AssemblyBalancesOverviewItem[] {
    return this.assemblies.filter(a => a.id === 'demo-asamblea-2026' && !this.closedIds.has(a.id)).map(a => ({
      id: a.id, name: a.name, cutoffDate: a.cutoffDate, meetingDate: a.meetingDate, source,
      workshops: DEMO_WORKSHOPS.map(org => { const mine = this.balances.get(`${a.id}|${source}`); return ({
        organizationId: org, name: demoRows.find(r => r.org === org)?.workshop ?? org,
        amountPending: mine?.get(org) ?? null, currency: mine?.has(org) ? 'CLP' : null,
        sourceReference: mine?.has(org) ? 'Registro validado' : null }) }),
    }))
  }
  recordBalances(assemblyId: string, payload: AssemblyBalancesPayload): void {
    if (assemblyId !== 'demo-asamblea-2026') throw new Error('Esta Asamblea aún no tiene asambleístas acreditados.')
    if (!payload.sourceReference.trim()) throw new Error('Indique la referencia del oficio.')
    if (payload.items.some(i => !Number.isFinite(i.amountPending) || i.amountPending < 0)) throw new Error('Los saldos deben ser montos de cero o más.')
    const key = `${assemblyId}|${payload.source ?? 'gran_tesoreria'}`
    const bal = this.balances.get(key) ?? new Map<string, number>()
    payload.items.forEach(i => bal.set(i.organizationId, i.amountPending))
    this.balances.set(key, bal)
  }
  evaluate(assemblyId: string): AssemblyEvaluateResult {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    if (this.closedIds.has(assemblyId)) throw new Error('La Asamblea ya está cerrada.')
    const att = this.attestations.get(assemblyId)
    if (assembly.id !== 'demo-asamblea-2026') throw new Error('Esta Asamblea aún no tiene asambleístas acreditados.')
    const missing = (Object.keys(assemblyAttestationTitles) as AssemblyAttestationKind[]).filter(k => !att?.has(k))
    if (missing.length) throw new Error('Faltan constancias de Régimen Interior: ' + missing.map(k => assemblyAttestationTitles[k]).join(', ') + '.')
    this.evaluatedIds.add(assemblyId)
    return { assemblyId, evaluated: demoRows.length, appended: demoRows.length, counts: { habilitado: 3, solo_asistencia: 2, inhabilitado: 1 } }
  }
  close(assemblyId: string, rosterReference: string): AssemblyCloseResult {
    if (!rosterReference.trim()) throw new Error('Indique la referencia del acta de cierre.')
    const report = this.readiness(assemblyId)
    if (report.closedAtUtc) throw new Error('El padrón ya está cerrado.')
    if (!report.mayClose) throw new Error('Todavía faltan pasos para cerrar el padrón: ' + report.items.filter(i => i.state !== 'ok' && i.code !== 'closure').map(i => i.title).join(', ') + '.')
    const at = new Date().toISOString()
    const digest = Array.from({ length: 64 }, (_, i) => '0123456789abcdef'[(i * 7 + assemblyId.length * 3) % 16]).join('')
    this.closedIds.set(assemblyId, { at, digest })
    return { assemblyId, closed: true, closedAtUtc: at, voters: 3, digest }
  }
  attestationList(assemblyId: string): AssemblyAttestationList {
    if (!this.assemblies.some(a => a.id === assemblyId)) throw new Error('No existe la Asamblea indicada.')
    const att = this.attestations.get(assemblyId)
    return { assemblyId, items: (Object.keys(assemblyAttestationTitles) as AssemblyAttestationKind[]).map(kind => {
      const a = att?.get(kind)
      return { kind, title: assemblyAttestationTitles[kind], attested: !!a, actaReference: a?.actaReference ?? null, statement: a?.statement ?? null, recordedAtUtc: a?.recordedAtUtc ?? null }
    }) }
  }
  recordAttestation(assemblyId: string, payload: AssemblyAttestationPayload): void {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    if (payload.actaReference.trim().length < 1 || payload.statement.trim().length < 10) throw new Error('Indique la referencia del acta y una declaración de al menos 10 caracteres.')
    if (assembly.id !== 'demo-asamblea-2026') throw new Error('Esta Asamblea aún no tiene asambleístas acreditados: no hay nada que certificar.')
    const att = this.attestations.get(assemblyId) ?? new Map()
    if (payload.action === 'attest') {
      if (att.has(payload.kind)) throw new Error('Esa constancia ya está vigente.')
      att.set(payload.kind, { actaReference: payload.actaReference.trim(), statement: payload.statement.trim(), recordedAtUtc: new Date().toISOString() })
    } else {
      if (!att.has(payload.kind)) throw new Error('No hay una constancia vigente que retirar.')
      att.delete(payload.kind)
    }
    this.attestations.set(assemblyId, att)
  }
  officialRoster(assemblyId: string, list: AssemblyOfficialList, format: 'pdf' | 'csv'): { blob: Blob; fileName: string } {
    const info = this.closedIds.get(assemblyId)
    if (!info) throw new Error('El padrón oficial solo se puede descargar cuando la Asamblea está cerrada.')
    const l = this.listing(assemblyId)
    const attendees = l.asambleistas.filter(r => r.mayAttend)
    const rows = list === 'sufragantes' ? l.sufragantes.map(v => [v.number, v.name, v.rut.replace('***', '000'), v.workshop, '']) : attendees.map((r, i) => [i + 1, r.name, r.rut.replace('***', '000'), r.workshop, assemblyCategoryLabel(r.category)])
    const day = info.at.slice(0, 10)
    const name = `padron-${list}-${day}.${format}`
    if (format === 'pdf') return { blob: new Blob(['Padrón oficial DEMOSTRATIVO (datos ficticios)'], { type: 'application/pdf' }), fileName: name }
    return { blob: new Blob(['\uFEFF' + [list === 'sufragantes' ? ['N°', 'Apellidos y nombres', 'RUT', 'Taller', 'Firma'] : ['N°', 'Apellidos y nombres', 'RUT', 'Taller', 'Firma', 'Calidad'], ...rows.map(r => list === 'sufragantes' ? [r[0], r[1], r[2], r[3], ''] : [r[0], r[1], r[2], r[3], '', r[4]])].map(r => r.map(c => `"${String(c).replaceAll('"', '""')}"`).join(';')).join('\r\n')], { type: 'text/csv;charset=utf-8' }), fileName: name }
  }
  listing(assemblyId: string): AssemblyListing {
    const assembly = this.assemblies.find(a => a.id === assemblyId)
    if (!assembly) throw new Error('No existe la Asamblea indicada.')
    const isDemo = assembly.id === 'demo-asamblea-2026'
    const evaluated = this.evaluatedIds.has(assemblyId)
    const closedInfo = this.closedIds.get(assemblyId)
    const rows = isDemo ? [...demoRows] : []
    const sorted = rows.sort((a, b) => a.last.localeCompare(b.last, 'es-CL') || a.first.localeCompare(b.first, 'es-CL'))
    const asambleistas: AssemblyListingRow[] = sorted.map((r, i) => {
      const debt = 'debt' in r && r.debt === true
      const lifted = debt && this.lifted.has(r.org)
      return {
        number: i + 1, personId: r.id, organizationId: r.org, name: `${r.last}, ${r.first}`, rut: r.rut, workshop: r.workshop,
        category: r.category, status: lifted ? 'habilitado' : r.status === 'pendiente' && evaluated ? 'habilitado' : r.status, mayAttend: r.status !== 'inhabilitado' && (r.status !== 'pendiente' || evaluated),
        mayVote: r.vote || lifted || (r.status === 'pendiente' && evaluated), blockedByDebt: debt && !lifted, blockLifted: lifted,
        reasonCode: debt ? 'MOROSIDAD_GRAN_TESORERIA' : r.status === 'pendiente' ? 'OFFICIAL_SOURCES_PENDING' : 'HABILITACION_VERIFICADA', signature: '',
      }
    })
    const sufragantes = asambleistas.filter(r => r.mayVote).map((r, i) => ({ number: i + 1, personId: r.personId, name: r.name, rut: r.rut, workshop: r.workshop, signature: '' }))
    const talleres = new Map<string, AssemblyWorkshopDebtRow>()
    for (const r of asambleistas.filter(x => x.blockedByDebt || x.blockLifted)) {
      const t = talleres.get(r.organizationId) ?? { organizationId: r.organizationId, workshop: r.workshop, members: asambleistas.filter(x => x.organizationId === r.organizationId).length, affected: 0, blockLifted: r.blockLifted }
      t.affected += 1; talleres.set(r.organizationId, t)
    }
    return { assemblyId: assembly.id, name: assembly.name, meetingDate: assembly.meetingDate, cutoffDate: assembly.cutoffDate, status: assembly.status, provisional: !closedInfo, officiallyClosed: !!closedInfo, mayIssueBallots: false, asambleistas, sufragantes, talleresConBloqueo: [...talleres.values()] }
  }
  decideDebtBlock(organizationId: string, payload: AssemblyDebtBlockPayload): void {
    if (payload.reason.trim().length < 10 || !payload.evidenceReference.trim()) throw new Error('Indique el motivo (mínimo 10 caracteres) y la referencia del acta.')
    if (payload.action === 'lift') this.lifted.add(organizationId); else this.lifted.delete(organizationId)
  }
}
