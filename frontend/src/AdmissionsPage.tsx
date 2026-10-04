import { useEffect, useState, type FormEvent } from 'react'
import { type AdmissionCaseDetail, type AdmissionEvidenceItem, type AdmissionPersonOption, type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import { type MembershipApiClient } from './api/membershipApi'
import { type DocumentApiClient } from './api/documentApi'
import { affiliationModeForDate, chileCivilDate } from './admissionDates'
import './admissions.css'
import AdmissionProcedurePanel from './AdmissionProcedurePanel'
import ExternalIncorporationDrawer from './ExternalIncorporationDrawer'
import { ActionDrawer } from './actionKit'
import { type DemoProfileKey } from './demoProfiles'

type AdmissionType = 'affiliation' | 'incorporation'
export default function AdmissionsPage({ api, membershipApi, documentApi, demoProfileKey }: { api: PmgmApiClient; membershipApi: MembershipApiClient; documentApi: DocumentApiClient; demoProfileKey?: DemoProfileKey }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [type, setType] = useState<AdmissionType>('affiliation')
  const [query, setQuery] = useState('')
  const [people, setPeople] = useState<AdmissionPersonOption[]>([])
  const [personId, setPersonId] = useState('')
  const [searching, setSearching] = useState(false)
  const [searched, setSearched] = useState(false)
  const [originObedience, setOriginObedience] = useState('')
  const [originLodgeName, setOriginLodgeName] = useState('')
  const [originLodgeNumber, setOriginLodgeNumber] = useState('')
  const [degree, setDegree] = useState('master')
  const [hasPact, setHasPact] = useState('true')
  const [withdrawalDate, setWithdrawalDate] = useState('')
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [activeCaseId, setActiveCaseId] = useState(api.useMocks ? 'demo-crv-review' : '')
  const [activeCase, setActiveCase] = useState<AdmissionCaseDetail | null>(null)
  useEffect(() => {
    let cancelled = false
    api.getOrganizationOptions().then(r => {
      if (cancelled) return
      const items = r.items.filter(x => x.type.toLowerCase() !== 'order')
      setOrganizations(items); setOrganizationId(items[0]?.id ?? '')
    }).catch(e => { if (!cancelled) setError(toMessage(e)) })
    return () => { cancelled = true }
  }, [api])
  useEffect(() => {
    let cancelled = false
    setPeople([]); setPersonId(''); setSearched(false); setSearching(false)
    if (!organizationId || query.trim().length < 3) return
    setSearching(true)
    const timer = window.setTimeout(() => {
      api.searchAdmissionPeople({ organizationId, admissionType: type, query: query.trim() })
        .then(r => { if (!cancelled) { setPeople(r.items); setSearched(true) } })
        .catch(e => { if (!cancelled) setError(toMessage(e)) })
        .finally(() => { if (!cancelled) setSearching(false) })
    }, 180)
    return () => { cancelled = true; window.clearTimeout(timer) }
  }, [api, organizationId, query, type])
  const resetSearch = () => { setPeople([]); setPersonId(''); setMessage(null); setError(null) }
  const selected = people.find(x => x.personId === personId) ?? null
  const derivedMode = affiliationModeForDate(withdrawalDate)
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setError(null); setMessage(null)
    if (!organizationId || !selected || searching) { setError('Busque y seleccione la identidad correcta.'); return }
    if (type === 'affiliation' && (!derivedMode || !selected.memberId)) { setError('Indique una fecha válida de otorgamiento de la Carta de Retiro Voluntario.'); return }
    setWorking(true)
    try {
      const result = await api.createAdmissionCase({
        organizationId, admissionType: type,
        personId: selected.personId, memberId: type === 'affiliation' ? selected.memberId : null,
        affiliationMode: type === 'affiliation' ? derivedMode : null,
        withdrawalLetterGrantedDate: type === 'affiliation' ? withdrawalDate : null,
        originObedience: type === 'incorporation' ? originObedience.trim() || null : null,
        originLodgeName: type === 'incorporation' ? originLodgeName.trim() || null : null,
        originLodgeNumber: type === 'incorporation' ? originLodgeNumber.trim() || null : null,
        degree: type === 'incorporation' ? degree : null,
        hasPeaceAndFriendshipPact: type === 'incorporation' ? (hasPact === 'unknown' ? null : hasPact === 'true') : null,
      })
      setActiveCaseId(result.id)
      setMessage('Expediente ' + result.id + ' creado y enviado a revisión.')
    } catch (e) { setError(toMessage(e)) } finally { setWorking(false) }
  }
  return <div className="admissions-page">
    <section className="page-heading"><div><p className="eyebrow">Secretaría · admisiones especiales</p><h1>Afiliación e Incorporación</h1><p>Camino separado de Insinuados e iniciación. Reutilice la identidad existente.</p></div><span className="count-badge">Expediente trazable</span></section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}
      <div className="admissions-switch" role="group" aria-label="Tipo de admisión">
        <button type="button" disabled={working} aria-pressed={type === 'affiliation'} className={type === 'affiliation' ? 'active' : ''} onClick={() => { resetSearch(); setType('affiliation') }}>Afiliación</button>
        <button type="button" disabled={working} aria-pressed={type === 'incorporation'} className={type === 'incorporation' ? 'active' : ''} onClick={() => { resetSearch(); setType('incorporation') }}>Incorporación</button>
      </div>
    {type === 'incorporation' && <ExternalIncorporationDrawer key={organizationId} api={api} organizationId={organizationId} onCreated={result => { setActiveCaseId(result.id); setMessage('Incorporación registrada para revisión. Expediente ' + result.id); setError(null); setQuery(''); setPeople([]); setPersonId('') }} />}
    <ActionDrawer label="Crear expediente de admisión" keepOpen><form className="panel admissions-form" onSubmit={submit}>
      <div className="form-grid">
        <label>Taller de destino<select required disabled={working} value={organizationId} onChange={e => { resetSearch(); setOrganizationId(e.target.value) }}><option value="">Seleccione…</option>{organizations.map(x => <option key={x.id} value={x.id}>{x.name}{x.number ? ' Nº ' + x.number : ''}</option>)}</select></label>
        {type === 'affiliation' && <label>Fecha Carta de Retiro Voluntario<input type="date" required max={chileCivilDate()} value={withdrawalDate} onChange={e => setWithdrawalDate(e.target.value)} /></label>}
        <label>Buscar identidad<input type="search" maxLength={80} disabled={working} value={query} onChange={e => { resetSearch(); setQuery(e.target.value) }} placeholder="Nombre o número institucional exacto" aria-describedby="admission-search-help" /></label>
        <label>Persona seleccionada<select required disabled={working || searching} value={personId} onChange={e => setPersonId(e.target.value)}><option value="">Seleccione…</option>{people.map(x => <option key={x.personId} value={x.personId}>{x.displayName}{x.institutionalNumber ? ' · ' + x.institutionalNumber : ''}</option>)}</select></label>
      </div>
      <p id="admission-search-help">{type === 'affiliation' ? 'Ingrese al menos tres caracteres. Incluye la historia del Taller; para otro Taller use el número institucional exacto.' : 'Busque una persona de expedientes autorizados. Para una identidad nueva use el botón Nueva persona de otra Obediencia.'}</p>
      {searching && <p role="status">Buscando…</p>}
      {searched && !searching && people.length === 0 && <p role="status">No hay coincidencias autorizadas. No cree una identidad duplicada.</p>}
      {selected && <div className="admission-identity"><strong>{selected.displayName}</strong><span>{selected.institutionalNumber ?? 'Persona externa, sin membresía GLMCh'}</span></div>}
      {type === 'affiliation' ? <fieldset><legend>Modalidad calculada por la Carta de Retiro</legend><div className="admission-derived-mode">{derivedMode ? derivedMode === 'simple' ? 'Afiliación simple' : 'Afiliación con activación' : 'Indique una fecha válida de la Carta de Retiro Voluntario.'}</div></fieldset> : <fieldset><legend>Antecedentes de incorporación</legend><div className="form-grid">
        <label>Obediencia de origen<input required value={originObedience} onChange={e => setOriginObedience(e.target.value)} /></label>
        <label>Taller de origen<input value={originLodgeName} onChange={e => setOriginLodgeName(e.target.value)} /></label>
        <label>Número de Taller<input value={originLodgeNumber} onChange={e => setOriginLodgeNumber(e.target.value)} /></label>
        <label>Grado<select value={degree} onChange={e => setDegree(e.target.value)}><option value="master">Maestro</option><option value="fellowcraft">Compañero</option><option value="apprentice">Aprendiz</option></select></label>
        <label>Pacto de Paz y Amistad<select value={hasPact} onChange={e => setHasPact(e.target.value)}><option value="true">Sí</option><option value="false">No</option><option value="unknown">No consta</option></select></label>
      </div></fieldset>}
      <div className="form-actions"><button className="primary-button" type="submit" disabled={working || searching || !selected}>{working ? 'Registrando…' : 'Crear expediente de admisión'}</button><small>La iniciación normal continúa en Secretaría → Insinuados → Circuito de iniciación.</small></div>
    </form></ActionDrawer>
    <AdmissionDocumentPanel demoProfileKey={demoProfileKey} api={api} membershipApi={membershipApi} documentApi={documentApi} organizationId={organizationId} caseId={activeCaseId} caseDetail={activeCase} onCaseChange={setActiveCase} onCaseIdChange={setActiveCaseId} />
  </div>
}
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'La API no respondió.' }

function AdmissionDocumentPanel({ api, membershipApi, documentApi, demoProfileKey, organizationId, caseId, caseDetail, onCaseChange, onCaseIdChange }: { api: PmgmApiClient; membershipApi: MembershipApiClient; documentApi: DocumentApiClient; demoProfileKey?: DemoProfileKey; organizationId: string; caseId: string; caseDetail: AdmissionCaseDetail | null; onCaseChange: (value: AdmissionCaseDetail | null) => void; onCaseIdChange: (value: string) => void }) {
  const [cases, setCases] = useState<Array<{ id: string; admissionType: string; status: string; createdAtUtc: string; evidenceCount: number; latestEvidenceStatus: string | null }>>([])
  const [caseStatus, setCaseStatus] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [evidenceType, setEvidenceType] = useState('withdrawal_letter')
  const [evidenceDate, setEvidenceDate] = useState('')
  const [sourceReference, setSourceReference] = useState('')
  const [notes, setNotes] = useState('')
  const [reviewTarget, setReviewTarget] = useState<AdmissionEvidenceItem | null>(null)
  const [reviewStatus, setReviewStatus] = useState<'approved' | 'observed' | 'rejected'>('approved')
  const [reviewDate, setReviewDate] = useState(chileCivilDate())
  const [reviewSource, setReviewSource] = useState('')
  const [materializeDate, setMaterializeDate] = useState(chileCivilDate())
  const [materializeSource, setMaterializeSource] = useState('')
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const loadCase = async () => {
    setWorking(true); setError(null)
    try { onCaseChange(await api.getAdmissionCase(caseId.trim())); setMessage('Expediente cargado con sus evidencias y decisiones.') }
    catch (reason) { onCaseChange(null); setError(toMessage(reason)) } finally { setWorking(false) }
  }
  useEffect(() => {
    let active = true
    api.listAdmissionCases(organizationId || undefined, caseStatus || undefined).then(result => { if (active) setCases(result.items) }).catch(() => { if (active) setCases([]) })
    return () => { active = false }
  }, [api, organizationId, caseId, caseStatus])
  const ensureCollection = async (organizationId: string) => {
    const collections = await documentApi.getCollections(organizationId)
    const existing = collections.items.find(item => item.name === 'Admisiones · evidencias')
    if (existing) return existing.id
    return (await documentApi.createCollection({ code: 'ADM-' + organizationId.replaceAll('-', '').slice(0, 10).toUpperCase(), name: 'Admisiones · evidencias', description: 'Documentos privados vinculados a expedientes de afiliación e incorporación.', scope: 'organization', organizationId })).id
  }
  const addEvidence = async (event: FormEvent) => {
    event.preventDefault(); if (!caseDetail || !file) return
    setWorking(true); setError(null); setMessage(null)
    try {
      if (file.size <= 0 || file.size > 20 * 1024 * 1024) throw new Error('El archivo debe pesar entre 1 byte y 20 MB.')
      if (evidenceType === 'withdrawal_letter' && file.type !== 'application/pdf') throw new Error('La Carta de Retiro Voluntario debe cargarse en PDF.')
      const uploaded = await documentApi.uploadManagedFile(await ensureCollection(caseDetail.organizationId), { title: 'Evidencia de admisión · ' + caseDetail.id, documentType: evidenceType, classification: 'restricted', accessPolicy: 'management_only' }, file)
      const saved = await api.addAdmissionEvidence(caseDetail.id, { evidenceType, documentVersionId: uploaded.version.id, evidenceDate: evidenceDate || null, sourceReference: sourceReference.trim() || null, notes: notes.trim() || null })
      onCaseChange({ ...caseDetail, evidence: [saved, ...caseDetail.evidence] }); setFile(null); setEvidenceDate(''); setSourceReference(''); setNotes(''); setMessage('Evidencia cargada, escaneada y vinculada al expediente.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }
  const reviewEvidence = async (event: FormEvent) => {
    event.preventDefault(); if (!caseDetail || !reviewTarget) return
    setWorking(true); setError(null); setMessage(null)
    try {
      const result = await api.reviewAdmissionEvidence(caseDetail.id, reviewTarget.id, { status: reviewStatus, asOfDate: reviewDate, sourceReference: reviewSource.trim(), notes: null })
      onCaseChange({ ...caseDetail, evidence: caseDetail.evidence.map(item => item.id === result.evidence.id ? result.evidence : item), decisions: [result.decision, ...caseDetail.decisions] }); setReviewTarget(null); setMessage('Revisión registrada y auditada.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }
  const materialize = async (event: FormEvent) => {
    event.preventDefault(); if (!caseDetail) return
    setWorking(true); setError(null); setMessage(null)
    try {
      const result = await api.materializeAdmissionCase(caseDetail.id, { effectiveDate: materializeDate, evidenceReference: materializeSource.trim() })
      onCaseChange(await api.getAdmissionCase(caseDetail.id))
      setMessage(result.idempotent ? 'La materialización ya existía; no se creó una segunda pertenencia.' : 'Pertenencia materializada y expediente cerrado con auditoría.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }
  return <section className="panel admissions-documents" aria-labelledby="admission-documents-title">
    <div className="page-heading"><div><p className="eyebrow">Secretaría · gestión documental</p><h2 id="admission-documents-title">Evidencias del expediente</h2><p>Carga privada, análisis de seguridad, versión trazable y revisión institucional. La API conserva la autoridad final.</p></div><span className="count-badge">{caseDetail ? caseDetail.evidence.length + ' evidencia(s)' : 'Sin expediente cargado'}</span></div>
    <div className="form-grid"><label>Estado<select value={caseStatus} onChange={event => setCaseStatus(event.target.value)}><option value="">Todos</option><option value="under_review">En revisión</option><option value="observed">Observado</option><option value="eligible">Elegible</option></select></label><label>Expediente<select value={caseId} onChange={event => onCaseIdChange(event.target.value)}><option value="">Seleccione…</option>{cases.map(item => <option key={item.id} value={item.id}>{item.admissionType === 'affiliation' ? 'Afiliación' : 'Incorporación'} · {item.status} · {item.evidenceCount} evidencia(s)</option>)}</select></label><button className="secondary-button" type="button" disabled={working || !caseId.trim()} onClick={() => void loadCase()}>Cargar expediente</button></div>
    {error && <div className="error-banner" role="alert">{error}</div>}{message && <div className="success-banner" role="status">{message}</div>}
    {caseDetail && <><ActionDrawer label="Adjuntar evidencia" keepOpen><form className="admissions-evidence-form" onSubmit={event => void addEvidence(event)}>
      <label>Tipo<select value={evidenceType} onChange={event => setEvidenceType(event.target.value)}><option value="withdrawal_letter">Carta de Retiro Voluntario</option><option value="legalized_initiation_evidence">Iniciación legalizada</option><option value="legalized_wage_increase_evidence">Aumento de salario legalizado</option><option value="legalized_exaltation_evidence">Exaltación legalizada</option><option value="degree_evidence">Grado declarado</option></select></label>
      <label>Archivo<input required type="file" accept={evidenceType === 'withdrawal_letter' ? 'application/pdf' : '.pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document'} onChange={event => setFile(event.target.files?.[0] ?? null)} /></label>
      <label>Fecha del documento<input type="date" value={evidenceDate} onChange={event => setEvidenceDate(event.target.value)} /></label><label>Fuente institucional<input required maxLength={500} value={sourceReference} onChange={event => setSourceReference(event.target.value)} /></label><label>Notas<input maxLength={4000} value={notes} onChange={event => setNotes(event.target.value)} /></label>
      <button className="primary-button" disabled={working || !file} type="submit">{working ? 'Procesando…' : 'Cargar y vincular evidencia'}</button>
    </form></ActionDrawer><div className="admission-evidence-list">{caseDetail.evidence.map(item => <article className="admission-evidence-row" key={item.id}><div><strong>{evidenceLabel(item.evidenceType)}</strong><small>{item.documentVersionId ? 'Versión documental vinculada' : 'Sin versión documental'} · {item.evidenceDate ?? 'Sin fecha'}</small></div><span className={'document-state ' + item.reviewStatus}>{item.reviewStatus}</span>{item.reviewStatus === 'pending' && <button className="secondary-button compact" type="button" onClick={() => { setReviewTarget(item); setReviewDate(chileCivilDate()); setReviewSource('') }}>Revisar</button>}</article>)}</div>
    <AdmissionProcedurePanel demoProfileKey={demoProfileKey} key={caseDetail.id} api={api} membershipApi={membershipApi} admission={caseDetail} onRefresh={async () => onCaseChange(await api.getAdmissionCase(caseDetail.id))} />
    {caseDetail.status !== 'resolved' && <ActionDrawer label="Materializar pertenencia" keepOpen><form className="panel admissions-materialization-form" onSubmit={event => void materialize(event)}><h3>Materializar pertenencia</h3><p>Disponible sólo cuando la API confirma todos los requisitos normativos del expediente.</p><label>Fecha efectiva<input required type="date" max={chileCivilDate()} value={materializeDate} onChange={event => setMaterializeDate(event.target.value)} /></label><label>Resolución o referencia institucional<input required maxLength={500} value={materializeSource} onChange={event => setMaterializeSource(event.target.value)} /></label><button className="primary-button" disabled={working || !materializeSource.trim()} type="submit">Materializar y cerrar expediente</button></form></ActionDrawer>}</>}
    {reviewTarget && <form className="panel admissions-review-form" onSubmit={event => void reviewEvidence(event)}><h3>Revisar {evidenceLabel(reviewTarget.evidenceType)}</h3><label>Resultado<select value={reviewStatus} onChange={event => setReviewStatus(event.target.value as typeof reviewStatus)}><option value="approved">Aprobar</option><option value="observed">Observar</option><option value="rejected">Rechazar</option></select></label><label>Fecha de revisión<input required type="date" value={reviewDate} max={chileCivilDate()} onChange={event => setReviewDate(event.target.value)} /></label><label>Fuente institucional<input required maxLength={500} value={reviewSource} onChange={event => setReviewSource(event.target.value)} /></label><button className="primary-button" disabled={working} type="submit">Registrar revisión</button><button className="secondary-button" type="button" onClick={() => setReviewTarget(null)}>Cancelar</button></form>}
  </section>
}
function evidenceLabel(value: string) { return value === 'withdrawal_letter' ? 'Carta de Retiro Voluntario' : value === 'legalized_initiation_evidence' ? 'Iniciación legalizada' : value === 'legalized_wage_increase_evidence' ? 'Aumento de salario legalizado' : value === 'legalized_exaltation_evidence' ? 'Exaltación legalizada' : 'Grado declarado' }
