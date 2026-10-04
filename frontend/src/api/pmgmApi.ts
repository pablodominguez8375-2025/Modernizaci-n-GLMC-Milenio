import { DynamicAccessClient } from './dynamicAccess'
import { getDemoProfile, type DemoProfileKey } from '../demoProfiles'
import { chileCivilDate } from '../admissionDates'
import { correctWithdrawalDateDemo, type WithdrawalDateCorrectionRequest, type WithdrawalDateCorrectionResponse, reviewWithdrawalSignatureDemo, withdrawalReviewFixture, type WithdrawalSignatureReviewRequest, type WithdrawalSignatureDecision } from './admissionWithdrawalEvidence'
import { adjustMockReceipt } from './lodgeReceiptAdjustments'
import { affiliationModeForDate } from '../admissionDates'
import { ExternalIncorporationDemo, type ExternalIncorporationRequest } from './externalIncorporation'
export type { ExternalIncorporationRequest } from './externalIncorporation'
import { lookupDemoAdmissionPeople, validateDemoAdmissionIdentity, type AdmissionPersonSearch, type AdmissionPersonSearchResponse } from './admissionLookup'
export type { AdmissionPersonOption } from './admissionLookup'
export interface CandidatePublication { displayName: string; workshopName: string; workshopNumber: string | null; publishedFromUtc: string; publishedUntilUtc: string | null; requiredDays: number; elapsedDays: number; complianceDateUtc: string; ruleCode: string; status: string }
export interface CandidatePortalResponse { culture: string; portal: string; total: number; items: CandidatePublication[] }
export interface SystemInfo { project: string; api: string; version: string; runtime: string; culture: string; institutionalTimeZone: string; defaultCurrency: string }
export interface SessionCapabilities {
  canApproveTransfers: boolean
  canRunRegimenInteriorReports: boolean
  canManageGrandSecretariat: boolean
  canManageTreasuryRegularity: boolean
  canManageHospitalariaRegularity: boolean
  canEvaluateCeremonies: boolean
  canReviewCeremonies: boolean
  canValidateCeremonyInternalAffairs: boolean
  canAuthorizeCeremonies: boolean
  canManagePrivacy: boolean
  canReadLodgeSecretariat?: boolean
  canManageLodgeSecretariat?: boolean
  canManageLodgeTreasury?: boolean
  canReadLodgeHospitalaria?: boolean
  canManageLodgeHospitalaria?: boolean
  canApproveLodgeExpenses?: boolean
  canConfigureSystem?: boolean
  canReadLodgeCouncilSummary?: boolean
  canManageLodgeCouncilSummaryAccess?: boolean
  canManageAnyWorkshopProfile?: boolean
  canManageApprenticeInstruction?: boolean
  canManageFellowcraftInstruction?: boolean
  canManageMasterInstruction?: boolean
  canReadOrderApprenticeInstructions?: boolean
  canReadOrderFellowcraftInstructions?: boolean
  canReadOrderMasterInstructions?: boolean
  canReadAllOrderInstructions?: boolean
}
export interface SessionProfile { displayName: string; accessScope: 'order' | 'organization' | 'authenticated'; capabilities: SessionCapabilities }
export type TreasuryTerritory = 'santiago' | 'other_oriente' | 'peru'
export interface OrganizationOption { id: string; name: string; number: string | null; type: string }
export interface TreasuryTerritoryOption extends OrganizationOption { treasuryTerritory: TreasuryTerritory | null }
export interface OrganizationOptionsResponse { total: number; items: OrganizationOption[] }
export interface SystemSetting { code: string; category: string; label: string; valueType: 'integer' | 'text' | 'list'; value: string; effectiveFrom: string; sourceReference: string; status: string }
export interface SystemSettingsResponse { total: number; items: SystemSetting[] }
export interface CreateSystemSettingVersionRequest { value: string;…30811 tokens truncated…==organizationId).sort((a,b)=>(b.submissionNumber??1)-(a.submissionNumber??1))[0];if(last&&last.status!=='observed')throw new Error('La transferencia más reciente sigue en revisión o conciliada.');const expected=items.reduce((n,x)=>n+x.amountDue,0);if(payload.amount!==expected||!payload.reference.trim())throw new Error('El monto transferido debe coincidir con lo recaudado y tener referencia.');const transfer:DeathReplenishmentTransfer={id:crypto.randomUUID(),organizationId,submissionNumber:(last?.submissionNumber??0)+1,amount:payload.amount,expectedAmount:expected,transferDate:payload.transferDate,reference:payload.reference,status:'submitted'};c.transfers.push(transfer);return {...transfer}}return this.postJson(`/api/hospitalaria/reposiciones/casos/${encodeURIComponent(caseId)}/talleres/${encodeURIComponent(organizationId)}/transferencias`,payload)}
  async reviewDeathReplenishmentTransfer(transferId:string,decision:'observed'|'reconciled',notes?:string|null):Promise<{id:string;status:string;organizationId:string}>{if(this.useMocks){const t=this.mockDeathReplenishmentCases.flatMap(c=>c.transfers).find(x=>x.id===transferId);if(!t)throw new Error('No existe la transferencia.');if(decision==='observed'&&!notes?.trim())throw new Error('Indique el motivo de observación.');if(decision==='reconciled'&&t.amount!==t.expectedAmount)throw new Error('El pago transferido no coincide con el total esperado.');t.status=decision;t.reviewNotes=notes??null;const cases=[...new Set((this.mockDeathReplenishmentObligations.get(t.organizationId)??[]).map(x=>x.caseId))];const outstanding=cases.some(id=>(this.mockDeathReplenishmentObligations.get(t.organizationId)??[]).some(x=>x.caseId===id&&x.status!=='paid')||!this.mockDeathReplenishmentCases.find(c=>c.id===id)?.transfers.some(x=>x.organizationId===t.organizationId&&x.status==='reconciled'));this.mockHospitalaria.set(t.organizationId,{id:`hospitalaria-${crypto.randomUUID()}`,organizationId:t.organizationId,status:decision==='reconciled'&&!outstanding?'up_to_date':'overdue',asOfDate:new Date().toISOString().slice(0,10),sourceReference:`hospitalaria-reposicion:${t.id}`,notes:decision==='reconciled'&&!outstanding?'Reposición transferida y conciliada por Gran Hospitalaria.':'Existen reposiciones pendientes u observadas.',recordedAtUtc:new Date().toISOString()});return{id:t.id,status:t.status,organizationId:t.organizationId}}return this.postJson(`/api/hospitalaria/reposiciones/transferencias/${encodeURIComponent(transferId)}/revision`,{decision,notes:notes??null})}
  private countPending(caseId:string){return [...this.mockDeathReplenishmentObligations.values()].flat().filter(x=>x.caseId===caseId&&x.status!=='paid').length}
  private ensureMockDeathReplenishments(){if(this.mockDeathReplenishmentCases.length)return;const caseId='death-case-demo-1';this.mockDeathReplenishmentCases.push({id:caseId,deathDate:'2026-09-10',deceasedDisplayName:'Hermano fallecido demostrativo',amountPerActiveMember:1500,obligatedMembers:440,dueAmount:660000,paidAmount:1500,pendingMembers:439,transfers:[]});for(const org of this.mockOrganizations){const items=Array.from({length:22},(_,i)=>{const paid=org.number==='1'&&i===0?1500:0;return{id:`obligation-demo-${org.number}-${i+1}`,caseId,deathDate:'2026-09-10',deceasedDisplayName:'Hermano fallecido demostrativo',memberId:`member-demo-${org.number}-${i+1}`,memberDisplayName:`Hermano ${String(i+1).padStart(2,'0')} · Taller ${org.number}`,amountDue:1500,paidAmount:paid,balance:1500-paid,status:(paid===1500?'paid':'pending') as 'paid'|'pending',payments:paid?[{id:'hosp-pay-demo-1',amount:paid,paymentMethod:'transfer',paymentDate:'2026-09-12',receiptNumber:'HOSP-DEMO-001',reference:'TRX-HOSP-DEMO-001'}]:[]}});this.mockDeathReplenishmentObligations.set(org.id,items)}}

  async getHospitalariaWorkshopRegularity(organizationId: string, asOf?: string): Promise<WorkshopRegularitySnapshot | null> {
    if (this.useMocks) return mockSnapshotAsOf(this.mockHospitalaria.get(organizationId), asOf)
    const query = new URLSearchParams(); if (asOf) query.set('asOf', asOf)
    return this.optionalGet<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad${query.size ? `?${query}` : ''}`)
  }
  async setHospitalariaWorkshopRegularity(organizationId: string, payload: WorkshopRegularityRequest): Promise<WorkshopRegularitySnapshot> {
    if (this.useMocks) { const snapshot = mockRegularitySnapshot(organizationId, payload, 'hospitalaria'); this.mockHospitalaria.set(organizationId, snapshot); return snapshot }
    return this.postJson<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad`, payload)
  }

  async getSecretariatAvailability(fromUtc: string, toUtc: string): Promise<SpaceAvailabilityResponse> { if (this.useMocks) { const items = this.mockSpaces.map(space => ({ ...space, isAvailable: !this.mockBusySpaces.has(space.id) })); return { fromUtc, toUtc, total: items.length, available: items.filter(x => x.isAvailable).length, items } } const query = new URLSearchParams({ fromUtc, toUtc }); return this.request<SpaceAvailabilityResponse>(`/api/gran-secretaria/espacios/disponibilidad?${query}`) }
  async getSecretariatDocuments(): Promise<SecretariatDocumentsResponse> { if (this.useMocks) return { total: this.mockDocuments.length, items: [...this.mockDocuments] }; return this.request<SecretariatDocumentsResponse>('/api/gran-secretaria/documentos') }
  async getSubmittedTenidas(): Promise<GrandSecretariatTenidasResponse> {
    if (this.useMocks) return { total: this.mockSubmittedTenidas.length, items: this.mockSubmittedTenidas.map(item => ({ ...item, lodge: { ...item.lodge } })) }
    return this.request<GrandSecretariatTenidasResponse>('/api/gran-secretaria/tenidas')
  }
  async reviewSubmittedTenida(recordId: string, decision: 'received' | 'observed', notes?: string | null): Promise<{ id:string; status:string }> {
    if (this.useMocks) {
      const item=this.mockSubmittedTenidas.find(value=>value.recordId===recordId); if(!item) throw new Error('La Tenida remitida no existe.')
      item.submissionStatus=decision; item.reviewedAtUtc=new Date().toISOString(); item.reviewNotes=notes?.trim()||null
      return { id: recordId, status: item.submissionStatus }
    }
    return this.postJson<{ id:string; status:string }>(`/api/gran-secretaria/tenidas/${encodeURIComponent(recordId)}/revision`, { decision, notes: notes ?? null })
  }
  async downloadSubmittedTenidaExtract(recordId: string): Promise<Blob> {
    if (this.useMocks) return new Blob(['Extracto PDF demostrativo'], { type: 'application/pdf' })
    const headers=new Headers({ Accept:'application/pdf' }); const token=await this.getAccessToken?.(); if(!token) throw new Error('Debe ingresar para descargar el extracto.'); headers.set('Authorization', `Bearer ${token}`)
    const response=await fetch(`${this.baseUrl}/api/gran-secretaria/tenidas/${encodeURIComponent(recordId)}/extracto`, { credentials:'omit', redirect:'error', cache:'no-store', headers })
    if(!response.ok){ if(response.status===401) await this.onUnauthorized?.(); throw new PmgmApiHttpError(response.status, response.status===403?'Su cuenta no tiene permiso para descargar este extracto.':`La API respondió ${response.status} ${response.statusText}.`) }
    return response.blob()
  }
  async getSecretariatCeremonyQueue(): Promise<GrandSecretariatCeremonyQueueResponse> { if (this.useMocks) return { total: this.mockCeremonies.length, items: this.mockCeremonies.map(item => ({ ...item })) }; return this.request<GrandSecretariatCeremonyQueueResponse>('/api/institutional/gran-secretaria/ceremonias-autorizadas') }
  async createSecretariatSpace(payload: CreateSpaceRequest): Promise<InstitutionalSpace> { if (this.useMocks) { const space: InstitutionalSpace = { id: crypto.randomUUID(), ...payload, location: payload.location ?? null, capacity: payload.capacity ?? null, status: 'active' }; this.mockSpaces.push(space); return space } return this.postJson<InstitutionalSpace>('/api/gran-secretaria/espacios', payload) }
  async createSecretariatReservation(payload: CreateReservationRequest): Promise<{ id: string; status: string }> {
    if (this.useMocks) {
      if (this.mockBusySpaces.has(payload.spaceId)) throw new Error('El templo o sala ya está reservado en ese horario.')
      const ceremony = payload.ceremonyRequestId ? this.mockCeremonies.find(item => item.id === payload.ceremonyRequestId) : undefined
      if (payload.ceremonyRequestId && !ceremony) throw new Error('La ceremonia indicada no existe en la bandeja autorizada.')
      if (ceremony && ceremony.organizationId !== payload.organizationId) throw new Error('La ceremonia no corresponde al Taller indicado.')
      const id = crypto.randomUUID(); this.mockBusySpaces.add(payload.spaceId)
      if (ceremony) { const space = this.mockSpaces.find(item => item.id === payload.spaceId); ceremony.spaceReservationId = id; ceremony.spaceName = space?.name ?? 'Espacio institucional'; ceremony.reservationStartsAtUtc = payload.startsAtUtc; ceremony.reservationEndsAtUtc = payload.endsAtUtc }
      return { id, status: 'reserved' }
    }
    return this.postJson<{ id: string; status: string }>('/api/gran-secretaria/reservas', payload)
  }
  async issueSecretariatDocument(payload: IssueDocumentRequest): Promise<SecretariatDocument> { if (this.useMocks) { const kind=payload.documentType==='plancha'?(payload.planchaKind??'formal_communication'):null; const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: payload.documentType, planchaKind: kind, documentCode: `${payload.documentType === 'decree' ? 'DEC' : 'PLA-COM'}-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: payload.title, content: payload.content, organizationId: payload.organizationId ?? null, relatedCeremonyRequestId: null, spaceReservationId: null, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document } return this.postJson<SecretariatDocument>('/api/gran-secretaria/documentos', payload) }
  async issueSecretariatCeremonyAuthorization(ceremonyRequestId: string, spaceReservationId: string | null = null): Promise<SecretariatDocument> {
    if (this.useMocks) {
      const ceremony = this.mockCeremonies.find(item => item.id === ceremonyRequestId); if (!ceremony) throw new Error('La ceremonia indicada no existe.'); if (ceremony.formalAuthorizationIssued) throw new Error('La ceremonia ya cuenta con autorización formal vigente.'); if (spaceReservationId && ceremony.spaceReservationId !== spaceReservationId) throw new Error('La reserva indicada no corresponde a esta ceremonia.'); ceremony.formalAuthorizationIssued = true
      const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: 'plancha', planchaKind: 'ceremony_authorization', documentCode: `PLA-AUT-CER-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: `Plancha de Autorización de Ceremonia — ${ceremonyTypeLabel(ceremony.ceremonyType)}`, content: `Plancha formal de autorización demostrativa para ${ceremony.organizationName}. No constituye Decreto.`, organizationId: ceremony.organizationId, relatedCeremonyRequestId: ceremony.id, spaceReservationId, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document
    }
    return this.postJson<SecretariatDocument>(`/api/gran-secretaria/ceremonias/${encodeURIComponent(ceremonyRequestId)}/autorizacion`, { spaceReservationId })
  }

  private requireMockReviewCeremony(id: string): CeremonyReviewQueueItem { const item = this.mockReviewCeremonies.find(value => value.id === id); if (!item) throw new Error('La ceremonia indicada no existe en la bandeja.'); return item }
  private requireMockTreasuryStatement(id: string): TreasuryStatement { const item = this.mockTreasuryStatements.get(id); if (!item) throw new Error('El cuadro mensual indicado no existe.'); return item }
  private ensureMockHospitalariaMovements(organizationId:string):LodgeHospitalariaMovement[]{
    let items=this.mockLodgeHospitalariaMovements.get(organizationId)
    if(!items){items=defaultHospitalariaMovements(organizationId);this.mockLodgeHospitalariaMovements.set(organizationId,items)}
    return items
  }
  private requireMockHospitalariaMovement(id:string):LodgeHospitalariaMovement{for(const rows of this.mockLodgeHospitalariaMovements.values()){const item=rows.find(x=>x.id===id);if(item)return item}throw new Error('El movimiento de Hospitalaria no existe.')}
  private requireMockHospitalariaSubmission(id:string):HospitalariaMonthlySubmission{const item=[...this.mockHospitalariaSubmissions.values()].find(x=>x.id===id);if(!item)throw new Error('La rendición de Hospitalaria no existe.');return item}
  private postJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private async optionalGet<T>(path: string): Promise<T | null> { try { return await this.request<T>(path) } catch (error) { if (error instanceof PmgmApiHttpError && error.status === 404) return null; throw error } }
  async createAdmissionCase(payload: CreateAdmissionCaseRequest): Promise<AdmissionCaseResponse> {
    if (this.useMocks) {
      validateDemoAdmissionIdentity(payload, this.externalIncorporationDemo.people)
      if (payload.admissionType === 'affiliation') {
        const expected = affiliationModeForDate(payload.withdrawalLetterGrantedDate ?? '')
        if (!expected) throw new Error('Indique una fecha válida de otorgamiento de la Carta de Retiro Voluntario, sin fecha futura.')
        if (payload.affiliationMode !== expected) throw new Error('La modalidad no corresponde a la antigüedad de la Carta de Retiro Voluntario.')
      }
      return { id: crypto.randomUUID(), organizationId: payload.organizationId, admissionType: payload.admissionType, affiliationMode: payload.affiliationMode ?? null, withdrawalLetterGrantedDate: payload.withdrawalLetterGrantedDate ?? null, memberId: payload.memberId ?? null, personId: payload.personId, status: 'under_review', createdAtUtc: new Date().toISOString() }
    }
    return this.postJson<AdmissionCaseResponse>('/api/admisiones/expedientes', payload)
  }

  async searchAdmissionPeople(input: AdmissionPersonSearch): Promise<AdmissionPersonSearchResponse> {
    if (this.useMocks) return lookupDemoAdmissionPeople(input, this.externalIncorporationDemo.people)
    return this.request<AdmissionPersonSearchResponse>(`/api/admisiones/personas-busqueda?${new URLSearchParams({ ...input, query: input.query.trim() })}`)
  }

  async createExternalIncorporation(payload: ExternalIncorporationRequest): Promise<AdmissionCaseResponse> {
    if (this.useMocks) return this.externalIncorporationDemo.create(payload)
    return this.postJson<AdmissionCaseResponse>('/api/admisiones/incorporaciones/persona-nueva', payload)
  }

  async getAdmissionCase(caseId: string): Promise<AdmissionCaseDetail> {
    if (this.useMocks) {
      const item = this.withdrawalReviewDemo
      if (caseId !== item.id) throw new Error('La demo sólo expone el expediente sintético demo-crv-review.')
      return { id: item.id, organizationId: defaultMockOrganizations.find(x => x.number === '23')!.id, admissionType: item.admissionType, affiliationMode: item.affiliationMode, withdrawalLetterGrantedDate: item.withdrawalLetterGrantedDate, memberId: 'demo-member-1', personId: 'demo-person-1', status: item.status, createdAtUtc: item.createdAtUtc, evidence: item.letters.map(letter => ({ id: letter.id, admissionCaseId: item.id, evidenceType: 'withdrawal_letter', documentVersionId: letter.documentVersionId, evidenceDate: letter.evidenceDate, sourceReference: 'SYNTHETIC', reviewStatus: letter.reviewStatus, reviewedAtUtc: letter.reviewedAtUtc, notes: null, createdAtUtc: letter.createdAtUtc })), decisions: item.decisions }
    }
    const response = await this.request<{ admissionCase: AdmissionCaseResponse; evidence: AdmissionEvidenceItem[]; decisions: AdmissionDecisionItem[] }>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}`)
    return { ...response.admissionCase, evidence: response.evidence, decisions: response.decisions }
  }

  async listAdmissionCases(organizationId?: string, status?: string): Promise<{ total: number; items: AdmissionCaseListItem[] }> {
    if (this.useMocks) {
      return { total: 1, items: [{ id: this.withdrawalReviewDemo.id, organizationId: defaultMockOrganizations.find(x => x.number === '23')!.id, admissionType: this.withdrawalReviewDemo.admissionType, affiliationMode: this.withdrawalReviewDemo.affiliationMode, withdrawalLetterGrantedDate: this.withdrawalReviewDemo.withdrawalLetterGrantedDate, memberId: 'demo-member-1', personId: 'demo-person-1', status: this.withdrawalReviewDemo.status, createdAtUtc: this.withdrawalReviewDemo.createdAtUtc, evidenceCount: this.withdrawalReviewDemo.letters.length, latestEvidenceStatus: this.withdrawalReviewDemo.letters[0]?.reviewStatus ?? null }] }
    }
    const query = new URLSearchParams()
    if (organizationId) query.set('organizationId', organizationId)
    if (status) query.set('status', status)
    return this.request<{ total: number; items: AdmissionCaseListItem[] }>('/api/admisiones/expedientes' + (query.size ? '?' + query.toString() : ''))
  }

  async addAdmissionEvidence(caseId: string, payload: AddAdmissionEvidenceRequest): Promise<AdmissionEvidenceItem> {
    if (this.useMocks) throw new Error('La carga operativa requiere una API institucional; la demo sólo muestra evidencia sintética.')
    return this.postJson<AdmissionEvidenceItem>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/evidencias`, payload)
  }

  async reviewAdmissionEvidence(caseId: string, evidenceId: string, payload: AdmissionEvidenceReviewRequest): Promise<{ evidence: AdmissionEvidenceItem; decision: AdmissionDecisionItem }> {
    if (this.useMocks) throw new Error('La revisión operativa requiere una API institucional; la demo sólo muestra evidencia sintética.')
    return this.postJson<{ evidence: AdmissionEvidenceItem; decision: AdmissionDecisionItem }>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/evidencias/${encodeURIComponent(evidenceId)}/revision`, payload)
  }

  async getAdmissionProcedure(caseId: string, demoProfileKey: DemoProfileKey = 'brother'): Promise<AdmissionProcedureResponse> {
    if (this.useMocks) {
      const c = await this.getAdmissionCase(caseId)
      const capabilities = getDemoProfile(demoProfileKey).capabilities
      const required = ['article_2_3_review', 'lodge_first_degree_presentation', 'lodge_third_degree_approval', 'lodge_first_degree_ballot']
      const requirements = required.map(code => ({ code, status: c.decisions.find(x => x.decisionType === code)?.status ?? 'observed', reason: 'Registro sintético de demostración; la autorización institucional se realiza en la API instalada.' }))
      return { caseId, status: 'observed', canProceedToCeremonyRequest: false, requirements, actions: { canManageLodge: capabilities.canManageLodgeSecretariat === true, canReviewSignature: capabilities.canManageGrandSecretariat, canReviewArticle23: capabilities.canValidateCeremonyInternalAffairs, canProvideGrandMasterDecision: demoProfileKey === 'grandMaster' || demoProfileKey === 'grandLodge' } }
    }
    return this.request<AdmissionProcedureResponse>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/habilitacion-procedimiento`)
  }

  async recordAdmissionProcedure(caseId: string, action: AdmissionProcedureAction, payload: AdmissionProcedurePayload): Promise<AdmissionDecisionItem> {
    if (this.useMocks) {
      if (caseId !== this.withdrawalReviewDemo.id || this.withdrawalReviewDemo.status === 'resolved') throw new Error('Seleccione el expediente sintético abierto de demostración.')
      if (!payload.sourceReference.trim()) throw new Error('Debe indicar la referencia institucional.')
      const types: Record<AdmissionProcedureAction, string> = { 'decisiones/presentacion-primer-grado':'lodge_first_degree_presentation', 'decisiones/tercer-grado':'lodge_third_degree_approval', 'decisiones/balotaje-primer-grado':'lodge_first_degree_ballot', 'comision-informacion':'information_commission_appointed', 'comision-informacion/conclusion':'information_commission_completed', 'revision-articulo-2-3':'article_2_3_review', 'decisiones/indulto-gran-maestria':'grand_master_pardon', 'decisiones/reconocimiento-regularidad':'grand_master_regularity_recognition', 'decisiones/gran-maestria-aceptacion-especial':'grand_master_special_acceptance' }
      const decision = { id: crypto.randomUUID(), admissionCaseId: caseId, decisionType: types[action], status: payload.status ?? ((payload.completed === false || payload.approved === false || payload.hasRayamiento || payload.hasTribunalForcedWithdrawal) ? 'rejected' : 'approved'), asOfDate: payload.asOfDate ?? payload.appointmentDate ?? chileCivilDate(), sourceReference: payload.sourceReference.trim(), notes: payload.notes ?? null, recordedBySubject: 'synthetic-demo', recordedAtUtc: new Date().toISOString() }
      this.withdrawalReviewDemo.decisions.unshift(decision)
      return decision
    }
    return this.postJson<AdmissionDecisionItem>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/${action}`, payload)
  }

  async createAdmissionCeremony(caseId: string, payload: { proposedDate: string; notes?: string | null }): Promise<{id:string;status:string;alreadyCreated:boolean}> {
    if (this.useMocks) throw new Error('La solicitud formal requiere la API institucional y sus requisitos completos. La demo conserva únicamente registros sintéticos.')
    return this.postJson(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/solicitud-ceremonia`, payload)
  }

  async materializeAdmissionCase(caseId: string, payload: MaterializeAdmissionRequest): Promise<MaterializeAdmissionResponse> {
    if (this.useMocks) {
      if (caseId !== this.withdrawalReviewDemo.id) throw new Error('La demo de materialización usa el expediente sintético demo-crv-review.')
      const existing = this.withdrawalReviewDemo.decisions.find(x => x.decisionType === 'membership_materialized')
      if (existing && (existing.asOfDate !== payload.effectiveDate || existing.sourceReference !== payload.evidenceReference.trim())) throw new Error('El expediente ya fue materializado con otros antecedentes.')
      if (existing) return { idempotent: true, membership: { id: 'demo-membership-materialized', memberId: 'demo-member-1', organizationId: defaultMockOrganizations.find(x => x.number === '23')!.id, startDate: payload.effectiveDate, status: 'active' } }
      this.withdrawalReviewDemo.status = 'resolved'
      this.withdrawalReviewDemo.decisions.unshift({ id: crypto.randomUUID(), admissionCaseId: caseId, decisionType: 'membership_materialized', status: 'approved', asOfDate: payload.effectiveDate, sourceReference: payload.evidenceReference.trim(), notes: 'Materialización sintética de demostración.', recordedBySubject: 'secretaria-demo', recordedAtUtc: new Date().toISOString() })
      return { idempotent: false, membershipId: 'demo-membership-materialized', admissionCaseId: caseId }
    }
    return this.postJson<MaterializeAdmissionResponse>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/materializar`, payload)
  }

  async reviewWithdrawalLetterSignature(caseId: string, payload: WithdrawalSignatureReviewRequest): Promise<WithdrawalSignatureDecision> {
    if (this.useMocks) {
      if (caseId !== this.withdrawalReviewDemo.id) throw new Error('La demo de revisión usa únicamente el expediente sintético demo-crv-review; la vista operativa sigue pendiente.')
      return reviewWithdrawalSignatureDemo(this.withdrawalReviewDemo, payload)
    }
    return this.postJson<WithdrawalSignatureDecision>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/verificaciones/carta-retiro-firma-manuscrita`, payload)
  }

  async correctWithdrawalLetterDate(caseId: string, payload: WithdrawalDateCorrectionRequest): Promise<WithdrawalDateCorrectionResponse> {
    if (this.useMocks) {
      if (caseId !== this.withdrawalReviewDemo.id) throw new Error('La corrección demo usa únicamente el expediente sintético demo-crv-review; el panel operativo sigue pendiente.')
      return correctWithdrawalDateDemo(this.withdrawalReviewDemo, payload)
    }
    return this.postJson<WithdrawalDateCorrectionResponse>(`/api/admisiones/expedientes/${encodeURIComponent(caseId)}/carta-retiro/correccion-fecha`, payload)
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para consultar la información institucional.'); headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* respuesta sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para realizar esta operación.'
      throw new PmgmApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultPmgmApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): PmgmApiClient { const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''; const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'; const url = new URL(baseUrl || '/', window.location.origin); if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La API debe usar el mismo origen mediante el proxy institucional.'); return new PmgmApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized }) }
function sleep(milliseconds: number): Promise<void> { return new Promise(resolve => window.setTimeout(resolve, milliseconds)) }
function mockInitialDeliberation(ceremonyRequestId: string, payload: InitialDeliberationRequest): InitialDeliberationResponse {
  if (!payload.sourceReference.trim()) throw new Error('Debe indicar la referencia del acta o extracto que respalda la deliberación.')
  if (payload.presentVoters <= 0) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.quorum', reason: 'Debe existir al menos una persona habilitada presente para registrar la votación.', ceremonyStatus: 'under_review' }
  if (payload.votesInFavor < 0 || payload.votesInFavor > payload.presentVoters) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.votes', reason: 'La cantidad de votos favorables no es válida.', ceremonyStatus: 'under_review' }
  const elapsedDays = dateOnlyDayNumber(payload.deliberationDate) - dateOnlyDayNumber('2026-09-12')
  const minimumWaitingDays = payload.minimumWaitingDays ?? 7
  if (elapsedDays < minimumWaitingDays) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.waiting_period', reason: `Deben transcurrir al menos ${minimumWaitingDays} días desde la presentación; han transcurrido ${elapsedDays}.`, ceremonyStatus: 'under_review' }
  if (payload.votesInFavor !== payload.presentVoters) return { id: ceremonyRequestId, validationStatus: 'rejected', code: 'initial_deliberation.unanimity', reason: 'La aprobación inicial requiere unanimidad de las personas presentes.', ceremonyStatus: 'rejected' }
  return { id: ceremonyRequestId, validationStatus: 'approved', code: 'initial_deliberation.approved', reason: 'Se cumple el plazo mínimo y la votación inicial fue unánime.', ceremonyStatus: 'under_review' }
}
function dateOnlyDayNumber(value: string) { const [year, month, day] = value.split('-').map(Number); if (!year || !month || !day) throw new Error('La fecha de deliberación no es válida.'); return Math.floor(Date.UTC(year, month - 1, day) / 86_400_000) }
function ceremonyTypeLabel(type: CeremonyType) { return type === 'initiation' ? 'Iniciación' : type === 'wage_increase' ? 'Aumento de salario' : 'Exaltación' }
function defaultHospitalariaMovements(organizationId:string):LodgeHospitalariaMovement[]{
  return [
    {id:`hosp-income-${organizationId}`,organizationId,movementType:'income',category:'charity_bag',amount:185000,movementDate:'2026-09-05',memberReference:null,destination:null,evidenceReference:'TENIDA-DEMO-2026-09-05',observation:'Tronco de Beneficencia · dato ficticio',approvalStatus:'not_required',approvalSource:null,councilDecisionId:null,approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-05T23:00:00Z'},
    {id:`hosp-aid-${organizationId}`,organizationId,movementType:'expense',category:'charity_aid',amount:45000,movementDate:'2026-09-10',memberReference:'Referencia reservada DEMO-001',destination:'Socorro reservado · demo',evidenceReference:'AYUDA-DEMO-001',observation:'Antecedente sensible ficticio; sólo Taller.',approvalStatus:'pending_approval',approvalSource:null,councilDecisionId:null,approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-10T18:00:00Z'}
  ]
}
function mockHospitalariaCouncilAidDecisions(organizationId:string):HospitalariaCouncilAidDecision[]{return[{id:`council-aid-${organizationId}`,sessionId:`council-session-${organizationId}`,sessionDate:'2026-09-11',subject:'Socorro reservado · referencia demo',amount:45000}]}
function mockHospitalariaCouncilFinancialReviews(organizationId:string):HospitalariaCouncilFinancialReview[]{return[{id:`council-review-${organizationId}`,sessionId:`council-session-review-${organizationId}`,sessionDate:'2026-09-15',periodLabel:'2026-09',conclusion:'Estado mensual de Hospitalaria revisado por Consejo · demo'}]}
function summarizeMockHospitalaria(organizationId:string,from:string,to:string,items:LodgeHospitalariaMovement[]):LodgeHospitalariaSummary{const income=items.filter(x=>x.movementType==='income').reduce((a,x)=>a+x.amount,0);const approvedExpenses=items.filter(x=>x.movementType==='expense'&&x.approvalStatus==='approved').reduce((a,x)=>a+x.amount,0);const pendingExpenses=items.filter(x=>x.movementType==='expense'&&x.approvalStatus==='pending_approval').length;const categories=[...new Set(items.map(x=>x.category))].map(category=>({category,total:items.filter(x=>x.category===category).reduce((a,x)=>a+x.amount,0),count:items.filter(x=>x.category===category).length}));return{organizationId,from,to,income,approvedExpenses,periodNet:income-approvedExpenses,pendingExpenses,movements:items.length,categories,items:items.map(x=>({...x}))}}
function currentMonthStart(){const d=new Date();return new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit'}).format(d)+'-01'}
function currentMonthEnd(){const p=currentMonthStart().slice(0,7).split('-').map(Number);return monthEnd(p[0],p[1])}
function monthEnd(year:number,month:number){return `${year}-${String(month).padStart(2,'0')}-${String(new Date(Date.UTC(year,month,0)).getUTCDate()).padStart(2,'0')}`}
function cloneHospitalariaSubmission(item:HospitalariaMonthlySubmission):HospitalariaMonthlySubmission{return{...item}}
function mockSnapshotAsOf(snapshot: WorkshopRegularitySnapshot | undefined, asOf?: string): WorkshopRegularitySnapshot | null { if (!snapshot) return null; if (asOf && snapshot.asOfDate > asOf) return null; return { ...snapshot } }
function mockGrandTreasuryRate(feeType:LodgeFeeType,territory:TreasuryTerritory|null|undefined,asOf:string):number|null{if(asOf<'2026-01-01')return null;if(feeType==='past_active')return 0;if(territory==='peru')return feeType==='normal'?6:null;if(territory==='santiago')return feeType==='normal'?21000:feeType==='spouse'?13000:feeType==='senior'?10000:feeType==='student'?8000:null;if(territory==='other_oriente')return feeType==='normal'?15000:feeType==='spouse'?10000:feeType==='senior'?8000:feeType==='student'?8000:null;return null}
function defaultLodgeFeePlans(organizationId: string): LodgeFeePlan[] { return [
  { id: `fee-normal-${organizationId}`, organizationId, feeType: 'normal', memberAmount: 26000, grandTreasuryAmount: 21000, workshopAmount: 5000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-student-${organizationId}`, organizationId, feeType: 'student', memberAmount: 10000, grandTreasuryAmount: 8000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-senior-${organizationId}`, organizationId, feeType: 'senior', memberAmount: 12000, grandTreasuryAmount: 10000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-spouse-${organizationId}`, organizationId, feeType: 'spouse', memberAmount: 15000, grandTreasuryAmount: 13000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
] }
function mockEffectiveLodgeFeePlan(plans:LodgeFeePlan[],feeType:LodgeFeeType,periodYear:number,periodMonth:number):LodgeFeePlan|undefined{
  const cutoff=new Date(Date.UTC(periodYear,periodMonth,0)).toISOString().slice(0,10)
  return plans.filter(plan=>plan.feeType===feeType&&plan.effectiveFrom<=cutoff&&(plan.effectiveUntil===null||plan.effectiveUntil>=cutoff)).sort((a,b)=>b.effectiveFrom.localeCompare(a.effectiveFrom))[0]
}
function summarizeMockLodgeTreasury(organizationId:string,periodYear:number,periodMonth:number,charges:LodgeTreasuryCharge[],plans:LodgeFeePlan[]):LodgeTreasurySummary{
  const memberExpected=charges.reduce((sum,charge)=>sum+charge.memberAmount,0)
  const collected=charges.reduce((sum,charge)=>sum+charge.paidAmount,0)
  const receivable=charges.reduce((sum,charge)=>sum+charge.balance,0)
  const grandTreasuryExpected=charges.reduce((sum,charge)=>{
    if(charge.feeType==='past_active')return sum
    if(!charge.feeType)throw new Error(`El cargo ficticio de ${charge.memberDisplayName} no tiene categoría de cuota.`)
    const plan=mockEffectiveLodgeFeePlan(plans,charge.feeType,periodYear,periodMonth)
    if(!plan||plan.grandTreasuryAmount===null)throw new Error(`No hay aporte institucional vigente para ${charge.feeType} en ${periodYear}-${String(periodMonth).padStart(2, '0')}.`)
    return sum+plan.grandTreasuryAmount
  },0)
  const paid=charges.filter(charge=>charge.status==='paid').length
  const partial=charges.filter(charge=>charge.status==='partial').length
  const pending=charges.filter(charge=>charge.status==='pending').length
  return{organizationId,periodYear,periodMonth,members:charges.length,memberExpected,collected,receivable,grandTreasuryExpected,workshopMarginProjected:memberExpected-grandTreasuryExpected,paid,partial,overdue:pending,trafficLight:charges.length===0?'no_data':receivable===0?'green':collected>0?'amber':'red'}
}
function mockTreasuryCharges(organizationId:string):LodgeTreasuryCharge[]{return[
  {id:`charge-1-${organizationId}`,memberId:'member-demo-001',memberDisplayName:'Andrea Demostrativa',feeType:'normal',memberAmount:26000,monthlyFeeAmount:26000,paidAmount:26000,balance:0,status:'paid',payments:[{id:'pay-demo-001',receiptNumber:'REC-DEMO-001',amount:26000,paymentMethod:'transfer',paymentDate:'2026-09-05',reference:'TRX-DEMO-001'}]},
  {id:`charge-2-${organizationId}`,memberId:'member-demo-002',memberDisplayName:'Beatriz Demostrativa',feeType:'normal',memberAmount:26000,monthlyFeeAmount:26000,paidAmount:13000,balance:13000,status:'partial',payments:[{id:'pay-demo-002',receiptNumber:'REC-DEMO-002',amount:13000,paymentMethod:'cash',paymentDate:'2026-09-09',reference:null}]},
  {id:`charge-3-${organizationId}`,memberId:'member-demo-003',memberDisplayName:'Carolina Demostrativa',feeType:'spouse',memberAmount:15000,monthlyFeeAmount:15000,paidAmount:0,balance:15000,status:'pending',payments:[]}
]}
function mockTreasuryExpenses(organizationId:string):LodgeTreasuryExpense[]{return[
  {id:`expense-1-${organizationId}`,organizationId,category:'Servicios',amount:42000,expenseDate:'2026-09-12',description:'Servicio operativo del Taller · dato ficticio',evidenceReference:'PDF-RESPALDO-DEMO-001',approvalStatus:'pending_approval',recordedBySubject:'tesoreria-demo',approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-12T18:00:00Z'}
]}
function cloneTreasuryCharge(item:LodgeTreasuryCharge):LodgeTreasuryCharge{return{...item,payments:item.payments.map(x=>({...x}))}}
function mockRegularitySnapshot(organizationId: string, payload: WorkshopRegularityRequest, prefix: string): WorkshopRegularitySnapshot { return { id: `${prefix}-${crypto.randomUUID()}`, organizationId, scope: 'organization', status: payload.status, asOfDate: payload.asOfDate, sourceReference: payload.sourceReference ?? null, notes: payload.notes ?? null, recordedAtUtc: new Date().toISOString() } }
function mockTreasuryStatement(organizationId: string, payload: CreateTreasuryStatementRequest, currency:'CLP'|'USD'='CLP'): TreasuryStatement { return { id: crypto.randomUUID(), organizationId, periodYear: payload.periodYear, periodMonth: payload.periodMonth, cutoffDate: payload.cutoffDate, status: 'draft', currency, sourceReference: payload.sourceReference ?? null, expectedAmount: 0, transferAmount: 0, depositAmount: 0, paidAmount: 0, differenceAmount: 0, unresolvedIdentities: 0, feeBreakdown:[], lines: [], payments: [], submittedAtUtc: null, reconciledAtUtc: null, closedAtUtc: null } }
function recalculateMockTreasury(statement: TreasuryStatement) { statement.expectedAmount = statement.lines.reduce((total, line) => total + line.payableAmount, 0); statement.transferAmount = statement.payments.filter(payment => payment.paymentMethod === 'transfer').reduce((total, payment) => total + payment.amount, 0); statement.depositAmount = statement.payments.filter(payment => payment.paymentMethod === 'deposit').reduce((total, payment) => total + payment.amount, 0); statement.paidAmount = statement.transferAmount + statement.depositAmount; statement.differenceAmount = statement.expectedAmount - statement.paidAmount; statement.feeBreakdown=Array.from(statement.lines.reduce((map,line)=>{const current=map.get(line.contributionType)??{feeType:line.contributionType,members:0,amount:0};current.members++;current.amount+=line.payableAmount;map.set(line.contributionType,current);return map},new Map<LodgeFeeType,TreasuryFeeBreakdown>()).values()) }
function cloneTreasuryStatement(statement: TreasuryStatement): TreasuryStatement { return { ...statement, feeBreakdown:statement.feeBreakdown.map(item=>({...item})), lines: statement.lines.map(line => ({ ...line })), payments: statement.payments.map(payment => ({ ...payment })) } }
function cloneCeremonyQueueItem(item: CeremonyReviewQueueItem): CeremonyReviewQueueItem { return { ...item, eligibility: { ...item.eligibility, publication: item.eligibility.publication ? { ...item.eligibility.publication } : null, ceremonyRight: item.eligibility.ceremonyRight ? { ...item.eligibility.ceremonyRight } : null, requirements: item.eligibility.requirements.map(value => ({ ...value })) }, actions: { ...item.actions } } }
function recomputeMockEligibility(item: CeremonyReviewQueueItem) { const blocked = item.eligibility.requirements.some(value => value.status === 'rejected'); const observed = item.eligibility.requirements.some(value => value.status === 'observed'); item.eligibility.canAuthorize = !blocked && !observed; item.eligibility.status = item.eligibility.canAuthorize ? 'complies' : observed ? 'observed' : 'does_not_comply'; item.actions.canAuthorize = item.eligibility.canAuthorize }
function mockRegimenSummary(filters: { organizationId?: string; asOf?: string; from?: string }): RegimenInteriorSummary {
  const asOf = filters.asOf ?? '2026-09-08'
  const from = filters.from ?? '2026-01-01'
  const scoped = !!filters.organizationId
  const multiplier = scoped ? 1 : 20
  const affiliated = 24 * multiplier
  return {
    scope: scoped ? 'organization' : 'order',
    organizationId: filters.organizationId ?? null,
    asOf,
    period: { from, to: asOf },
    members: { totalRelated: affiliated, currentlyAffiliated: affiliated, active: affiliated, inactive: 0, currentWithBlockingStatus: 0 },
    events: { voluntaryWithdrawals: 0, forcedWithdrawals: 0, reinstatements: 0, deaths: 0, transfers: 0 },
    financialRegularity: { source: 'Gran Tesorería · escenario QA', currentAffiliations: affiliated, upToDate: affiliated, delinquent: 0, pending: 0, exempt: 0, withoutStatus: 0, delinquentMembersDistinct: 0 },
    degreeDistribution: { master: 12 * multiplier, fellowcraft: 5 * multiplier, apprentice: 5 * multiplier, past_active: 2 * multiplier },
    pendingTransfers: 0,
  }
}

function groupWorkshopDeathCases(organizationId:string,items:DeathReplenishmentObligation[],cases:DeathReplenishmentCase[]){return [...new Set(items.map(x=>x.caseId))].map(caseId=>{const rows=items.filter(x=>x.caseId===caseId);const transfer=cases.find(x=>x.id===caseId)?.transfers.filter(x=>x.organizationId===organizationId).sort((a,b)=>(b.submissionNumber??1)-(a.submissionNumber??1))[0];return{caseId,deathDate:rows[0].deathDate,deceasedDisplayName:rows[0].deceasedDisplayName,dueAmount:rows.reduce((n,x)=>n+x.amountDue,0),paidAmount:rows.reduce((n,x)=>n+x.paidAmount,0),allPaid:rows.every(x=>x.status==='paid'),transfer:transfer?{id:transfer.id,amount:transfer.amount,reference:transfer.reference,status:transfer.status,transferDate:transfer.transferDate}:null}})}
