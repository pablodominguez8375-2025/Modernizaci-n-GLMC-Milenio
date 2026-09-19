export type AdmissionType = 'affiliation' | 'incorporation'
export type AffiliationMode = 'simple' | 'activation'
export type AffiliationProcedure = 'standard' | 'reentry' | 'transfer'

export interface AdmissionCase {
  id: string
  organizationId: string
  admissionType: AdmissionType
  affiliationMode: AffiliationMode | null
  affiliationProcedure: AffiliationProcedure | null
  memberId: string | null
  personId: string
  originOrganizationId: string | null
  originLodgeName: string | null
  originLodgeNumber: string | null
  originObedience: string | null
  degree: string | null
  wageIncreaseEvidenceApplies: boolean
  exaltationEvidenceApplies: boolean
  hasPeaceAndFriendshipPact: boolean | null
  originObedienceRecognizedAsRegular: boolean | null
  previousRejectionDate: string | null
  rejectionCausesRemedied: boolean | null
  status: string
  createdAtUtc: string
}

export interface AdmissionCaseListItem {
  admissionCase: AdmissionCase
  organizationName: string
  personDisplayName: string
}
export interface AdmissionCaseListResponse { total: number; items: AdmissionCaseListItem[] }
export interface AdmissionPersonOption { personId: string; displayName: string; memberId: string | null }
export interface AdmissionPersonOptionsResponse { total: number; items: AdmissionPersonOption[] }
export interface AdmissionEvidence {
  id: string; admissionCaseId: string; evidenceType: string; documentVersionId: string | null; evidenceDate: string | null
  sourceReference: string | null; reviewStatus: string; reviewedAtUtc: string | null; notes: string | null; createdAtUtc: string
}
export interface AdmissionDecision {
  id: string; admissionCaseId: string; decisionType: string; status: string; asOfDate: string
  sourceReference: string | null; notes: string | null; structuredDataJson: string | null; recordedAtUtc: string
}
export interface AdmissionCommissionGroup {
  appointmentGroupId: string; appointmentDate: string; sourceReference: string; memberIds: string[]; recordedAtUtc: string
}
export interface AdmissionCaseDetail {
  admissionCase: AdmissionCase
  evidence: AdmissionEvidence[]
  decisions: AdmissionDecision[]
  commission: AdmissionCommissionGroup[]
}
export interface AdmissionRequirement { code: string; name?: string; status: string; reason: string }
export interface AdmissionEligibility {
  status: string
  canProceed: boolean
  requirements: AdmissionRequirement[]
}
export interface AdmissionEligibilityResponse {
  caseId: string
  admissionType: AdmissionType
  status: string
  eligibility: AdmissionEligibility
  evidence: Record<string, string | null>
}
export interface CreateAdmissionCasePayload {
  organizationId: string
  admissionType: AdmissionType
  affiliationMode?: AffiliationMode | null
  affiliationProcedure?: AffiliationProcedure | null
  memberId?: string | null
  personId: string
  originOrganizationId?: string | null
  originLodgeName?: string | null
  originLodgeNumber?: string | null
  originObedience?: string | null
  degree?: string | null
  wageIncreaseEvidenceApplies?: boolean
  exaltationEvidenceApplies?: boolean
  hasPeaceAndFriendshipPact?: boolean | null
  originObedienceRecognizedAsRegular?: boolean | null
  previousRejectionDate?: string | null
  rejectionCausesRemedied?: boolean | null
}
export interface AdmissionCeremonyRequestResponse {
  id: string; admissionCaseId: string; organizationId: string; ceremonyType: AdmissionType; memberId: string | null
  proposedDate: string | null; status: string; alreadyCreated: boolean
}

export type AdmissionAccessTokenProvider = () => Promise<string | null>
export interface AdmissionApiClientOptions {
  baseUrl?: string
  getAccessToken?: AdmissionAccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

class AdmissionApiHttpError extends Error {
  constructor(readonly status: number, message: string) {
    super(message)
    this.name = 'AdmissionApiHttpError'
  }
}

interface DemoProgress {
  article23: boolean
  presentation: boolean
  commissionAppointed: boolean
  commissionWaived: boolean
  commissionCompleted: boolean
  thirdDegree: boolean
  ballot: boolean
  ceremonyRequestId: string | null
  ceremonyStatus: string | null
  materialized: boolean
}

const demoOrganizationId = '23232323-2323-2323-2323-232323232323'
const demoPeople: AdmissionPersonOption[] = [
  { personId: '91000000-0000-0000-0000-000000000001', displayName: 'Hermana Afiliación Estándar · Demo', memberId: 'aaaaaaaa-1111-1111-1111-111111111111' },
  { personId: '91000000-0000-0000-0000-000000000002', displayName: 'Hermano Reintegro · Demo', memberId: 'aaaaaaaa-2222-2222-2222-222222222222' },
  { personId: '91000000-0000-0000-0000-000000000003', displayName: 'Hermana Traslado · Demo', memberId: 'aaaaaaaa-3333-3333-3333-333333333333' },
  { personId: '91000000-0000-0000-0000-000000000004', displayName: 'Persona Incorporación · Demo', memberId: null },
]

const demoCaseRows: AdmissionCaseListItem[] = [
  {
    organizationName: 'Taller Demostrativo Nº 23',
    personDisplayName: demoPeople[0].displayName,
    admissionCase: {
      id: 'a1000000-0000-0000-0000-000000000001', organizationId: demoOrganizationId, admissionType: 'affiliation',
      affiliationMode: 'simple', affiliationProcedure: 'standard', memberId: demoPeople[0].memberId, personId: demoPeople[0].personId,
      originOrganizationId: null, originLodgeName: 'Taller de origen · demo', originLodgeNumber: null, originObedience: null, degree: 'master',
      wageIncreaseEvidenceApplies: false, exaltationEvidenceApplies: false, hasPeaceAndFriendshipPact: null, originObedienceRecognizedAsRegular: null,
      previousRejectionDate: null, rejectionCausesRemedied: null, status: 'under_review', createdAtUtc: '2026-09-18T15:00:00Z',
    },
  },
  {
    organizationName: 'Taller Demostrativo Nº 23',
    personDisplayName: demoPeople[1].displayName,
    admissionCase: {
      id: 'a1000000-0000-0000-0000-000000000002', organizationId: demoOrganizationId, admissionType: 'affiliation',
      affiliationMode: 'activation', affiliationProcedure: 'reentry', memberId: demoPeople[1].memberId, personId: demoPeople[1].personId,
      originOrganizationId: null, originLodgeName: 'Taller histórico · demo', originLodgeNumber: '8', originObedience: null, degree: 'master',
      wageIncreaseEvidenceApplies: false, exaltationEvidenceApplies: false, hasPeaceAndFriendshipPact: null, originObedienceRecognizedAsRegular: null,
      previousRejectionDate: null, rejectionCausesRemedied: null, status: 'under_review', createdAtUtc: '2026-09-17T15:00:00Z',
    },
  },
  {
    organizationName: 'Taller Demostrativo Nº 23',
    personDisplayName: demoPeople[2].displayName,
    admissionCase: {
      id: 'a1000000-0000-0000-0000-000000000003', organizationId: demoOrganizationId, admissionType: 'affiliation',
      affiliationMode: 'simple', affiliationProcedure: 'transfer', memberId: demoPeople[2].memberId, personId: demoPeople[2].personId,
      originOrganizationId: '11111111-1111-1111-1111-111111111111', originLodgeName: 'Taller Demostrativo Nº 1', originLodgeNumber: '1',
      originObedience: null, degree: 'master', wageIncreaseEvidenceApplies: false, exaltationEvidenceApplies: false,
      hasPeaceAndFriendshipPact: null, originObedienceRecognizedAsRegular: null, previousRejectionDate: null, rejectionCausesRemedied: null,
      status: 'eligible', createdAtUtc: '2026-09-16T15:00:00Z',
    },
  },
  {
    organizationName: 'Taller Demostrativo Nº 23',
    personDisplayName: demoPeople[3].displayName,
    admissionCase: {
      id: 'a1000000-0000-0000-0000-000000000004', organizationId: demoOrganizationId, admissionType: 'incorporation',
      affiliationMode: null, affiliationProcedure: null, memberId: null, personId: demoPeople[3].personId,
      originOrganizationId: null, originLodgeName: 'Resp. Logia Exterior · demo', originLodgeNumber: '45', originObedience: 'Obediencia Exterior · demo',
      degree: 'master', wageIncreaseEvidenceApplies: true, exaltationEvidenceApplies: true, hasPeaceAndFriendshipPact: false,
      originObedienceRecognizedAsRegular: true, previousRejectionDate: null, rejectionCausesRemedied: null,
      status: 'under_review', createdAtUtc: '2026-09-15T15:00:00Z',
    },
  },
]

function seedProgress(row: AdmissionCaseListItem): DemoProgress {
  const procedure = row.admissionCase.affiliationProcedure
  if (procedure === 'transfer') return {
    article23: true, presentation: true, commissionAppointed: false, commissionWaived: true, commissionCompleted: false,
    thirdDegree: true, ballot: true, ceremonyRequestId: 'ac000000-0000-0000-0000-000000000003', ceremonyStatus: 'authorized', materialized: false,
  }
  return {
    article23: true, presentation: true, commissionAppointed: false, commissionWaived: false, commissionCompleted: false,
    thirdDegree: procedure === 'standard', ballot: false, ceremonyRequestId: null, ceremonyStatus: null, materialized: false,
  }
}

export class AdmissionApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AdmissionAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockCases = demoCaseRows.map(row => ({ ...row, admissionCase: { ...row.admissionCase } }))
  private readonly mockProgress = new Map<string, DemoProgress>(this.mockCases.map(row => [row.admissionCase.id, seedProgress(row)]))

  constructor(options: AdmissionApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async searchPeople(organizationId: string, query: string): Promise<AdmissionPersonOptionsResponse> {
    if (this.useMocks) {
      const q = query.trim().toLocaleLowerCase('es-CL')
      const items = demoPeople.filter(item => item.displayName.toLocaleLowerCase('es-CL').includes(q))
      return { total: items.length, items: items.map(item => ({ ...item })) }
    }
    const qs = new URLSearchParams({ organizationId, query })
    return this.request<AdmissionPersonOptionsResponse>(`/api/admisiones/personas/opciones?${qs}`)
  }

  async listCases(organizationId?: string): Promise<AdmissionCaseListResponse> {
    if (this.useMocks) {
      const rows = organizationId ? this.mockCases.filter(x => x.admissionCase.organizationId === organizationId) : this.mockCases
      return { total: rows.length, items: rows.map(cloneRow) }
    }
    const qs = organizationId ? `?organizationId=${encodeURIComponent(organizationId)}` : ''
    return this.request<AdmissionCaseListResponse>(`/api/admisiones/expedientes${qs}`)
  }

  async createCase(payload: CreateAdmissionCasePayload): Promise<AdmissionCase> {
    if (this.useMocks) {
      if (payload.admissionType === 'affiliation' && (!payload.affiliationMode || !payload.affiliationProcedure || !payload.memberId))
        throw new Error('La afiliación requiere modalidad, procedimiento y hermano existente.')
      if (payload.admissionType === 'incorporation' && (!payload.originObedience?.trim() || !payload.degree?.trim()))
        throw new Error('La incorporación requiere Obediencia de origen y grado acreditado.')
      const person = demoPeople.find(x => x.personId === payload.personId)
      const row: AdmissionCaseListItem = {
        organizationName: 'Taller Demostrativo Nº 23',
        personDisplayName: person?.displayName ?? 'Persona demostrativa',
        admissionCase: {
          id: crypto.randomUUID(), organizationId: payload.organizationId, admissionType: payload.admissionType,
          affiliationMode: payload.admissionType === 'affiliation' ? payload.affiliationMode ?? null : null,
          affiliationProcedure: payload.admissionType === 'affiliation' ? payload.affiliationProcedure ?? null : null,
          memberId: payload.admissionType === 'affiliation' ? payload.memberId ?? null : null, personId: payload.personId,
          originOrganizationId: payload.originOrganizationId ?? null, originLodgeName: payload.originLodgeName?.trim() || null,
          originLodgeNumber: payload.originLodgeNumber?.trim() || null, originObedience: payload.originObedience?.trim() || null,
          degree: payload.degree?.trim() || null, wageIncreaseEvidenceApplies: payload.wageIncreaseEvidenceApplies ?? false,
          exaltationEvidenceApplies: payload.exaltationEvidenceApplies ?? false, hasPeaceAndFriendshipPact: payload.hasPeaceAndFriendshipPact ?? null,
          originObedienceRecognizedAsRegular: payload.originObedienceRecognizedAsRegular ?? null,
          previousRejectionDate: payload.previousRejectionDate ?? null, rejectionCausesRemedied: payload.rejectionCausesRemedied ?? null,
          status: 'under_review', createdAtUtc: new Date().toISOString(),
        },
      }
      this.mockCases.unshift(row); this.mockProgress.set(row.admissionCase.id, seedProgress(row))
      return { ...row.admissionCase }
    }
    return this.postJson<AdmissionCase>('/api/admisiones/expedientes', normalizedCreatePayload(payload))
  }

  async getCase(caseId: string): Promise<AdmissionCaseDetail> {
    if (this.useMocks) {
      const row = this.requireRow(caseId)
      const progress = this.requireProgress(caseId)
      return {
        admissionCase: { ...row.admissionCase, status: progress.materialized ? 'resolved' : row.admissionCase.status },
        evidence: [],
        decisions: mockDecisions(caseId, progress),
        commission: progress.commissionAppointed ? [{
          appointmentGroupId: 'cg-' + caseId, appointmentDate: '2026-09-17', sourceReference: 'ACTA-COMISION-DEMO',
          memberIds: ['aaaaaaaa-1111-1111-1111-111111111111','aaaaaaaa-2222-2222-2222-222222222222','aaaaaaaa-3333-3333-3333-333333333333'],
          recordedAtUtc: '2026-09-17T20:00:00Z',
        }] : [],
      }
    }
    return this.request<AdmissionCaseDetail>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}`)
  }

  async getEligibility(caseId: string): Promise<AdmissionEligibilityResponse> {
    if (this.useMocks) return mockEligibility(this.requireRow(caseId), this.requireProgress(caseId))
    return this.request<AdmissionEligibilityResponse>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/elegibilidad`)
  }

  async recordArticle23(caseId: string, payload: { hasRayamiento: boolean; hasTribunalForcedWithdrawal: boolean; asOfDate: string; sourceReference: string; notes?: string | null }): Promise<void> {
    if (this.useMocks) { this.requireProgress(caseId).article23 = !payload.hasRayamiento && !payload.hasTribunalForcedWithdrawal; return }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/revision-articulo-2-3`, payload)
  }

  async recordFirstDegreePresentation(caseId: string, payload: { presentationDate: string; sourceReference: string; notes?: string | null }): Promise<void> {
    if (this.useMocks) { if (!this.requireProgress(caseId).article23) throw new Error('Debe existir revisión habilitante del art. 2.3.'); this.requireProgress(caseId).presentation = true; return }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/presentacion-primer-grado`, payload)
  }

  async appointCommission(caseId: string, payload: { memberIds: string[]; appointmentDate: string; sourceReference: string }): Promise<void> {
    if (this.useMocks) {
      if (new Set(payload.memberIds).size !== 3) throw new Error('La comisión debe tener exactamente tres Maestros distintos.')
      const progress=this.requireProgress(caseId); progress.commissionAppointed=true; progress.commissionCompleted=false; progress.commissionWaived=false; return
    }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/comision-informacion`, payload)
  }

  async waiveTransferCommission(caseId: string, payload: { asOfDate: string; sourceReference: string; notes?: string | null }): Promise<void> {
    if (this.useMocks) {
      const row=this.requireRow(caseId); if(row.admissionCase.affiliationProcedure!=='transfer') throw new Error('La dispensa sólo aplica a afiliación con traslado.')
      this.requireProgress(caseId).commissionWaived=true; return
    }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/comision-informacion/dispensa-traslado`, payload)
  }

  async completeCommission(caseId: string, payload: { completed: boolean; asOfDate: string; sourceReference: string; notes?: string | null }): Promise<void> {
    if (this.useMocks) {
      const progress=this.requireProgress(caseId); if(!progress.commissionAppointed) throw new Error('Debe nombrar la comisión vigente antes de concluirla.')
      progress.commissionCompleted=payload.completed; return
    }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/comision-informacion/conclusion`, payload)
  }

  async recordThirdDegree(caseId: string, payload: { asOfDate: string; sourceReference: string; presentMasters: number; votesInFavor: number; votesAgainst: number; abstentions: number; notes?: string | null }): Promise<void> {
    if (this.useMocks) {
      const row=this.requireRow(caseId), progress=this.requireProgress(caseId)
      const required=requiresCommission(row.admissionCase)
      if(required && !(progress.commissionCompleted || (row.admissionCase.affiliationProcedure==='transfer' && progress.commissionWaived)))
        throw new Error('Debe concluir la comisión vigente o registrar la dispensa válida de traslado.')
      if(payload.votesInFavor+payload.votesAgainst+payload.abstentions!==payload.presentMasters) throw new Error('La suma de votos debe coincidir con Maestros presentes.')
      progress.thirdDegree=payload.presentMasters>0 && payload.votesInFavor*3 >= payload.presentMasters*2
      return
    }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/decisiones/tercer-grado`, payload)
  }

  async recordFirstDegreeBallot(caseId: string, payload: { asOfDate: string; sourceReference: string; ballots: Array<{ procedureNumber: number; eligibleVoters: number; whiteBallots: number; blackBallots: number }>; ballotApproved: boolean; notes?: string | null }): Promise<void> {
    if (this.useMocks) { if(!this.requireProgress(caseId).thirdDegree) throw new Error('La decisión de 3.er grado debe estar aprobada.'); this.requireProgress(caseId).ballot=payload.ballotApproved; return }
    await this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/decisiones/balotaje-primer-grado`, payload)
  }

  async createCeremonyRequest(caseId: string, payload: { proposedDate?: string | null; notes?: string | null }): Promise<AdmissionCeremonyRequestResponse> {
    if (this.useMocks) {
      const row=this.requireRow(caseId), progress=this.requireProgress(caseId)
      const eligibility=mockEligibility(row,progress); if(!eligibility.eligibility.canProceed) throw new Error('El expediente aún no cumple la matriz de habilitación.')
      progress.ceremonyRequestId ??= crypto.randomUUID(); progress.ceremonyStatus ??= 'under_review'
      row.admissionCase.status='eligible'
      return { id:progress.ceremonyRequestId, admissionCaseId:caseId, organizationId:row.admissionCase.organizationId, ceremonyType:row.admissionCase.admissionType, memberId:row.admissionCase.memberId, proposedDate:payload.proposedDate??null, status:progress.ceremonyStatus, alreadyCreated:false }
    }
    return this.postJson<AdmissionCeremonyRequestResponse>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/solicitud-ceremonia`,payload)
  }

  async completeCeremony(requestId: string, payload: { meetingId: string; ceremonyDate: string }): Promise<void> {
    if (this.useMocks) {
      const entry=[...this.mockProgress.entries()].find(([,p])=>p.ceremonyRequestId===requestId)
      if(!entry) throw new Error('La solicitud de ceremonia no existe.')
      const progress=entry[1]; if(progress.ceremonyStatus!=='authorized') throw new Error('La ceremonia debe estar autorizada por el flujo institucional.')
      if(!payload.meetingId) throw new Error('Debe seleccionar una Tenida ceremonial cerrada.')
      progress.materialized=true; progress.ceremonyStatus='completed'
      const row=this.requireRow(entry[0]); row.admissionCase.status='resolved'
      return
    }
    await this.postJson(`/api/admisiones/ceremonias/${encodeURIComponent(requestId)}/registrar-realizacion`,payload)
  }

  private requireRow(caseId: string) {
    const row=this.mockCases.find(x=>x.admissionCase.id===caseId)
    if(!row) throw new AdmissionApiHttpError(404,'El expediente no existe.')
    return row
  }
  private requireProgress(caseId: string) {
    const value=this.mockProgress.get(caseId)
    if(!value) throw new AdmissionApiHttpError(404,'El expediente no existe.')
    return value
  }

  private async postJson<T = unknown>(path: string, payload: unknown): Promise<T> {
    return this.request<T>(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
  }
  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers=new Headers(init.headers); headers.set('Accept','application/json')
    const token=await this.getAccessToken?.()
    if(!token) throw new Error('Debe ingresar para consultar la información institucional.')
    headers.set('Authorization',`Bearer ${token}`)
    const response=await fetch(`${this.baseUrl}${path}`,{...init,credentials:'omit',redirect:'error',cache:'no-store',headers})
    if(!response.ok){
      if(response.status===401) await this.onUnauthorized?.()
      let message=''
      try{const body=await response.clone().json() as {message?:string}; message=typeof body.message==='string'?body.message:''}catch{/* sin JSON */}
      if(response.status===403) message='Su perfil no tiene atribución para esta actuación.'
      throw new AdmissionApiHttpError(response.status,message||`La API respondió ${response.status} ${response.statusText}.`)
    }
    if(response.status===204) return undefined as T
    return response.json() as Promise<T>
  }
}

function requiresCommission(value: AdmissionCase) {
  return value.admissionType==='incorporation' ||
    (value.admissionType==='affiliation' && (value.affiliationProcedure==='reentry' || value.affiliationProcedure==='transfer'))
}
function mockEligibility(row: AdmissionCaseListItem, p: DemoProgress): AdmissionEligibilityResponse {
  const c=row.admissionCase, required=requiresCommission(c), waived=c.affiliationProcedure==='transfer'&&p.commissionWaived
  const requirements:AdmissionRequirement[]=[
    req('article_2_3',p.article23,'Revisión del art. 2.3','Régimen Interior debe completar el control del art. 2.3.'),
    req('lodge_first_degree_presentation',p.presentation,'Presentación en 1.er grado','Debe constar lectura/presentación en Cámara de Primer Grado.'),
  ]
  if(required){
    requirements.push(req('information_commission',waived||p.commissionAppointed,waived?'Dispensa de Cámara del Medio registrada.':'Comisión de tres Maestros vigente.','Debe nombrarse la comisión de tres Maestros o, sólo para traslado, registrar dispensa de Cámara del Medio.'))
    requirements.push(req('information_commission_completed',waived||p.commissionCompleted,waived?'Comisión dispensada válidamente.':'Comisión concluida.','La comisión vigente debe concluir antes del 3.er grado.'))
  }
  requirements.push(req('lodge_third_degree_decision',p.thirdDegree,'Decisión de 3.er grado conforme.','Falta decisión favorable de 3.er grado.'))
  requirements.push(req('lodge_first_degree_ballot',p.ballot,'Balotaje de 1.er grado conforme.','Falta balotaje secreto posterior de 1.er grado.'))
  const canProceed=requirements.every(x=>x.status==='approved')
  return {caseId:c.id,admissionType:c.admissionType,status:canProceed?'complies':'pending_requirements',eligibility:{status:canProceed?'complies':'pending_requirements',canProceed,requirements},evidence:{}}
}
function req(code:string,ok:boolean,yes:string,no:string):AdmissionRequirement{return{code,status:ok?'approved':'rejected',reason:ok?yes:no}}
function mockDecisions(caseId:string,p:DemoProgress):AdmissionDecision[]{
  const rows:Array<{type:string;ok:boolean;source:string}>=[
    {type:'article_2_3_review',ok:p.article23,source:'ACTA-RI-DEMO'},
    {type:'lodge_first_degree_presentation',ok:p.presentation,source:'ACTA-1G-DEMO'},
    {type:'information_commission_waiver',ok:p.commissionWaived,source:'ACTA-CAMARA-MEDIO-DEMO'},
    {type:'information_commission_completed',ok:p.commissionCompleted,source:'INFORME-COMISION-DEMO'},
    {type:'lodge_third_degree_approval',ok:p.thirdDegree,source:'ACTA-3G-DEMO'},
    {type:'lodge_first_degree_ballot',ok:p.ballot,source:'ACTA-BALOTAJE-DEMO'},
    {type:'ceremony_request_created',ok:Boolean(p.ceremonyRequestId),source:p.ceremonyRequestId??''},
    {type:'ceremony_completed',ok:p.materialized,source:p.ceremonyRequestId??'DEMO'},
  ]
  return rows.filter(x=>x.ok).map((row,index)=>({id:`demo-decision-${index}-${caseId}`,admissionCaseId:caseId,decisionType:row.type,status:'approved',asOfDate:'2026-09-18',sourceReference:row.source,notes:null,structuredDataJson:null,recordedAtUtc:'2026-09-18T20:00:00Z'}))
}
function cloneRow(row:AdmissionCaseListItem):AdmissionCaseListItem{return{...row,admissionCase:{...row.admissionCase}}}
function normalizedCreatePayload(payload:CreateAdmissionCasePayload){
  return {
    ...payload,
    affiliationMode:payload.admissionType==='affiliation'?payload.affiliationMode??null:null,
    affiliationProcedure:payload.admissionType==='affiliation'?payload.affiliationProcedure??null:null,
    memberId:payload.admissionType==='affiliation'?payload.memberId??null:null,
    originOrganizationId:payload.originOrganizationId??null,
    originLodgeName:payload.originLodgeName?.trim()||null,
    originLodgeNumber:payload.originLodgeNumber?.trim()||null,
    originObedience:payload.originObedience?.trim()||null,
    degree:payload.degree?.trim()||null,
    wageIncreaseEvidenceApplies:payload.wageIncreaseEvidenceApplies??false,
    exaltationEvidenceApplies:payload.exaltationEvidenceApplies??false,
    hasPeaceAndFriendshipPact:payload.hasPeaceAndFriendshipPact??null,
    originObedienceRecognizedAsRegular:payload.originObedienceRecognizedAsRegular??null,
    previousRejectionDate:payload.previousRejectionDate??null,
    rejectionCausesRemedied:payload.rejectionCausesRemedied??null,
  }
}

export function createDefaultAdmissionApiClient(getAccessToken?:AdmissionAccessTokenProvider,onUnauthorized?:()=>Promise<void>):AdmissionApiClient{
  const baseUrl=import.meta.env.VITE_API_BASE_URL??''
  const useMocks=import.meta.env.VITE_USE_MOCKS==='true'
  const url=new URL(baseUrl||'/',window.location.origin)
  if(url.origin!==window.location.origin||url.username||url.password||url.search||url.hash) throw new Error('La API debe usar el mismo origen mediante el proxy institucional.')
  return new AdmissionApiClient({baseUrl,useMocks,getAccessToken,onUnauthorized})
}
