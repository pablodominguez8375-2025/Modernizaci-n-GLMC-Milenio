import { type FormEvent, type ReactNode, useEffect, useMemo, useState } from 'react'
import { type DocumentApiClient } from './api/documentApi'
import {
  type CreateHistoricalMemberIntakeRequest,
  type HistoricalMemberIntake,
  type LodgeAdministrativeMeeting,
  type LodgeApiClient,
  type LodgeMeeting,
  type LodgeMemberOption,
  type LodgeSecretariatRecord,
  type LodgeSecretariatRecordType,
} from './api/lodgeApi'
import { useLodgeCouncilApi } from './LodgeCouncilApiContext'
import './lodgeSecretariat.css'

type Props = {
  organizationId: string
  lodgeApi: LodgeApiClient
  documentApi: DocumentApiClient
  meetings: LodgeMeeting[]
  members: LodgeMemberOption[]
}

type DocumentKind = 'work_paper' | 'extract' | 'full_minute'

export default function LodgeSecretariatPanel({ organizationId, lodgeApi, documentApi, meetings, members }: Props) {
  const councilApi = useLodgeCouncilApi()
  const [intakes, setIntakes] = useState<HistoricalMemberIntake[]>([])
  const [adminMeetings, setAdminMeetings] = useState<LodgeAdministrativeMeeting[]>([])
  const [records, setRecords] = useState<LodgeSecretariatRecord[]>([])
  const [councilSessions, setCouncilSessions] = useState<Array<{ id: string; sessionDate: string; title: string | null; status: string }>>([])
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [firstNames, setFirstNames] = useState('')
  const [lastNames, setLastNames] = useState('')
  const [rut, setRut] = useState('')
  const [institutionalNumber, setInstitutionalNumber] = useState('')
  const [currentDegree, setCurrentDegree] = useState<'apprentice' | 'fellowcraft' | 'master'>('master')
  const [membershipStartDate, setMembershipStartDate] = useState('')
  const [initiationDate, setInitiationDate] = useState('')
  const [wageIncreaseDate, setWageIncreaseDate] = useState('')
  const [exaltationDate, setExaltationDate] = useState('')
  const [evidenceReference, setEvidenceReference] = useState('Cuadro del Taller vigente')
  const [officeType, setOfficeType] = useState('')
  const [officePeriod, setOfficePeriod] = useState('2026')
  const [bulkFile, setBulkFile] = useState<File | null>(null)

  const [meetingDate, setMeetingDate] = useState(today())
  const [meetingTitle, setMeetingTitle] = useState('')
  const [meetingPurpose, setMeetingPurpose] = useState('')

  const [recordType, setRecordType] = useState<LodgeSecretariatRecordType>('tenida')
  const [sourceRecordId, setSourceRecordId] = useState('')
  const [workPaperAuthorMemberId, setWorkPaperAuthorMemberId] = useState('')
  const [uploadingKind, setUploadingKind] = useState<DocumentKind | null>(null)

  useEffect(() => {
    if (!organizationId) return
    let active = true
    setError(null)
    Promise.all([
      lodgeApi.getHistoricalMemberIntakes(organizationId),
      lodgeApi.getAdministrativeMeetings(organizationId),
      lodgeApi.getSecretariatRecords(organizationId),
      councilApi.getSessions(organizationId),
    ]).then(([historical, reunions, secretariatRecords, councils]) => {
      if (!active) return
      setIntakes(historical.items)
      setAdminMeetings(reunions.items)
      setRecords(secretariatRecords.items)
      setCouncilSessions(councils.items)
    }).catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [organizationId, lodgeApi, councilApi])

  const sources = useMemo(() => {
    if (recordType === 'tenida') return meetings.map(x => ({ id: x.id, label: `${formatDate(x.meetingDate)} · ${x.title || meetingTypeLabel(x.meetingType)}`, date: x.meetingDate, status: x.status, ceremonial: !!x.ceremonyType }))
    if (recordType === 'reunion') return adminMeetings.map(x => ({ id: x.id, label: `${formatDate(x.meetingDate)} · ${x.title}`, date: x.meetingDate, status: x.status, ceremonial: false }))
    return councilSessions.map(x => ({ id: x.id, label: `${formatDate(x.sessionDate)} · ${x.title || 'Consejo de Administración'}`, date: x.sessionDate, status: x.status, ceremonial: false }))
  }, [recordType, meetings, adminMeetings, councilSessions])

  useEffect(() => {
    setSourceRecordId(current => sources.some(x => x.id === current) ? current : sources[0]?.id ?? '')
  }, [sources])

  const currentRecord = records.find(x => x.recordType === recordType && x.sourceRecordId === sourceRecordId) ?? null
  const currentSource = sources.find(x => x.id === sourceRecordId) ?? null
  const planchaAllowed = recordType === 'tenida' && currentSource && !currentSource.ceremonial
  const canSubmitToGrandSecretariat =
    recordType === 'tenida' &&
    currentSource &&
    (currentSource.status === 'held' || currentSource.status === 'closed') &&
    !!currentRecord?.extractDocumentVersionId

  const refreshHistorical = async () => setIntakes((await lodgeApi.getHistoricalMemberIntakes(organizationId)).items)
  const refreshMeetings = async () => setAdminMeetings((await lodgeApi.getAdministrativeMeetings(organizationId)).items)
  const refreshRecords = async () => setRecords((await lodgeApi.getSecretariatRecords(organizationId)).items)

  const createHistorical = async (event: FormEvent) => {
    event.preventDefault()
    const offices = officeType.trim() ? [{ officeType: officeType.trim(), period: officePeriod.trim() || 'vigente', isCurrent: true }] : []
    const payload: CreateHistoricalMemberIntakeRequest = {
      firstNames, lastNames, rut: rut || null, institutionalNumber: institutionalNumber || null,
      currentDegree, membershipStartDate: membershipStartDate || null, initiationDate: initiationDate || null,
      wageIncreaseDate: wageIncreaseDate || null, exaltationDate: exaltationDate || null,
      cutoffDate: today(), evidenceReference, offices,
    }
    await execute(async () => {
      await lodgeApi.createHistoricalMemberIntake(organizationId, payload)
      await refreshHistorical()
      setFirstNames(''); setLastNames(''); setRut(''); setInstitutionalNumber(''); setMembershipStartDate('')
      setInitiationDate(''); setWageIncreaseDate(''); setExaltationDate(''); setOfficeType('')
    }, 'Carga histórica guardada como borrador. Debe enviarse a Régimen Interior para validación.')
  }

  const submitHistorical = async (id: string) => {
    await execute(async () => { await lodgeApi.submitHistoricalMemberIntake(id); await refreshHistorical() }, 'Antecedente enviado a Régimen Interior.')
  }

  const downloadTemplate = async () => {
    await execute(async () => {
      const blob = await lodgeApi.downloadHistoricalTemplate(organizationId)
      downloadBlob(blob, 'Cuadro-del-Taller-carga-inicial.xlsx')
    }, 'Plantilla de carga masiva preparada.')
  }

  const importTemplate = async () => {
    if (!bulkFile) return
    await execute(async () => {
      const result = await lodgeApi.importHistoricalMemberIntakes(organizationId, bulkFile)
      await refreshHistorical()
      setBulkFile(null)
      setMessage(`${result.imported} registros importados como borrador para revisión de Secretaría.`)
    }, null)
  }

  const createAdministrativeMeeting = async (event: FormEvent) => {
    event.preventDefault()
    await execute(async () => {
      await lodgeApi.createAdministrativeMeeting(organizationId, { meetingDate, title: meetingTitle, purpose: meetingPurpose || null })
      await refreshMeetings()
      setMeetingTitle(''); setMeetingPurpose('')
    }, 'Reunión programada como registro privado del Taller.')
  }

  const markMeetingHeld = async (id: string) => {
    await execute(async () => { await lodgeApi.markAdministrativeMeetingHeld(id); await refreshMeetings() }, 'Reunión marcada como Realizada.')
  }

  const ensureSecretariatCollection = async () => {
    const response = await documentApi.getCollections(organizationId)
    const existing = response.items.find(x => x.organizationId === organizationId && x.name === 'Secretaría del Taller')
    if (existing) return existing.id
    const created = await documentApi.createCollection({
      code: `SEC-${organizationId.replaceAll('-', '').slice(0, 12).toUpperCase()}`,
      name: 'Secretaría del Taller',
      description: 'Archivo privado de Tenidas, Reuniones y Consejo de Administración.',
      scope: 'organization',
      organizationId,
    })
    return created.id
  }

  const uploadDocument = async (kind: DocumentKind, file: File) => {
    if (!sourceRecordId || !currentSource) return
    if (kind === 'work_paper' && !planchaAllowed) {
      setError('La plancha de trabajo sólo corresponde a Tenidas no ceremoniales.')
      return
    }
    if (kind === 'work_paper' && !workPaperAuthorMemberId) {
      setError('Seleccione al hermano autor de la plancha.')
      return
    }

    setUploadingKind(kind); setError(null); setMessage(null)
    try {
      const normalized = normalizeInstitutionalFile(file)
      const collectionId = await ensureSecretariatCollection()
      const labels = {
        work_paper: { title: `Plancha de trabajo · ${currentSource.label}`, documentType: 'work_paper', classification: 'confidential' as const },
        extract: { title: `Extracto de acta · ${currentSource.label}`, documentType: 'minute_extract', classification: 'confidential' as const },
        full_minute: { title: `Acta completa · ${currentSource.label}`, documentType: 'full_minute', classification: 'restricted' as const },
      }
      if (kind === 'extract' && normalized.type !== 'application/pdf') throw new Error('El extracto debe cargarse obligatoriamente en PDF.')
      if ((kind === 'work_paper' || kind === 'full_minute') && !isPdfOrWord(normalized)) throw new Error('La plancha y el acta completa admiten PDF o Word (.docx).')

      const uploaded = await documentApi.uploadManagedFile(collectionId, {
        title: labels[kind].title,
        documentType: labels[kind].documentType,
        classification: labels[kind].classification,
        accessPolicy: 'management_only',
      }, normalized)

      const current = currentRecord
      const payload = {
        workPaperDocumentVersionId: kind === 'work_paper' ? uploaded.version.id : current?.workPaperDocumentVersionId ?? null,
        workPaperAuthorMemberId: kind === 'work_paper' ? workPaperAuthorMemberId : current?.workPaperAuthorMemberId ?? null,
        extractDocumentVersionId: kind === 'extract' ? uploaded.version.id : current?.extractDocumentVersionId ?? null,
        fullMinuteDocumentVersionId: kind === 'full_minute' ? uploaded.version.id : current?.fullMinuteDocumentVersionId ?? null,
      }
      await lodgeApi.upsertSecretariatRecord(organizationId, recordType, sourceRecordId, payload)
      await refreshRecords()
      setMessage(kind === 'work_paper'
        ? 'Plancha de trabajo guardada en el archivo privado del Taller. Puede cargarse o reemplazarse posteriormente.'
        : kind === 'extract'
          ? 'Extracto PDF guardado. En una Tenida realizada puede remitirse a Gran Secretaría.'
          : 'Acta completa guardada como documento opcional y privado del Taller.')
    } catch (reason) { setError(toMessage(reason)) } finally { setUploadingKind(null) }
  }

  const submitExtract = async () => {
    if (!canSubmitToGrandSecretariat || !sourceRecordId) return
    await execute(async () => { await lodgeApi.submitTenidaExtract(organizationId, sourceRecordId); await refreshRecords() }, 'Extracto PDF remitido a Gran Secretaría. La plancha y el acta completa permanecen privadas del Taller.')
  }

  const execute = async (action: () => Promise<void>, success: string | null) => {
    setWorking(true); setError(null); setMessage(null)
    try { await action(); if (success) setMessage(success) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setWorking(false) }
  }

  if (!organizationId) return null

  return <section className="lodge-secretariat-workspace">
    <div className="lodge-secretariat-heading">
      <div><p className="lodge-kicker">Gestión Logial › Secretaría del Taller</p><h2>Cuadro, reuniones y archivo de Secretaría</h2><p>La Secretaría opera la información del Taller. Régimen Interior valida la carga histórica y Gran Secretaría sólo recibe datos básicos de Tenidas con su extracto PDF.</p></div>
      <span className="lodge-live-chip">Ámbito privado del Taller</span>
    </div>

    {error && <div className="error-banner" role="alert">{error}</div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <div className="lodge-secretariat-grid">
      <article className="panel">
        <div className="panel-heading"><div><p className="eyebrow">Puesta en marcha</p><h3>Carga inicial del Cuadro del Taller</h3></div><span className="count-badge">{intakes.length} cargas</span></div>
        <p className="form-note">Para hermanos que ya pertenecen a la Orden. No obliga a inventar fechas desconocidas y no usa el flujo de nuevos iniciados.</p>
        <form className="regularity-form" onSubmit={createHistorical}>
          <div className="lodge-form-row"><Field label="Nombres"><input required value={firstNames} onChange={e=>setFirstNames(e.target.value)} /></Field><Field label="Apellidos"><input required value={lastNames} onChange={e=>setLastNames(e.target.value)} /></Field></div>
          <div className="lodge-form-row"><Field label="RUT"><input value={rut} onChange={e=>setRut(e.target.value)} placeholder="Opcional" /></Field><Field label="Nº institucional"><input value={institutionalNumber} onChange={e=>setInstitutionalNumber(e.target.value)} /></Field></div>
          <div className="lodge-form-row"><Field label="Grado actual"><select value={currentDegree} onChange={e=>setCurrentDegree(e.target.value as typeof currentDegree)}><option value="apprentice">Aprendiz</option><option value="fellowcraft">Compañero</option><option value="master">Maestro</option></select></Field><Field label="Ingreso al Taller"><input type="date" value={membershipStartDate} onChange={e=>setMembershipStartDate(e.target.value)} /></Field></div>
          <div className="lodge-form-row"><Field label="Iniciación"><input type="date" value={initiationDate} onChange={e=>setInitiationDate(e.target.value)} /></Field><Field label="Aumento de salario"><input type="date" value={wageIncreaseDate} onChange={e=>setWageIncreaseDate(e.target.value)} /></Field><Field label="Exaltación"><input type="date" value={exaltationDate} onChange={e=>setExaltationDate(e.target.value)} /></Field></div>
          <div className="lodge-form-row"><Field label="Cargo vigente opcional"><input value={officeType} onChange={e=>setOfficeType(e.target.value)} placeholder="Ej.: lodge_secretariat" /></Field><Field label="Período"><input value={officePeriod} onChange={e=>setOfficePeriod(e.target.value)} /></Field></div>
          <Field label="Fuente / evidencia"><input required value={evidenceReference} onChange={e=>setEvidenceReference(e.target.value)} /></Field>
          <button className="regularity-primary" type="submit" disabled={working}>Guardar borrador</button>
        </form>

        <div className="lodge-secretariat-import">
          <button type="button" className="regularity-secondary" disabled={working} onClick={()=>void downloadTemplate()}>Descargar plantilla Excel</button>
          <input aria-label="Plantilla Excel del Cuadro" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={e=>setBulkFile(e.target.files?.[0]??null)} />
          <button type="button" className="regularity-secondary" disabled={working||!bulkFile} onClick={()=>void importTemplate()}>Importar Excel</button>
        </div>

        <div className="lodge-secretariat-list">
          {intakes.slice(0,12).map(item=><div key={item.id}><div><strong>{item.firstNames} {item.lastNames}</strong><span>{degreeLabel(item.currentDegree)} · {intakeStatusLabel(item.status)}</span><small>{item.evidenceReference}</small></div>{(item.status==='draft'||item.status==='observed')&&<button type="button" className="regularity-secondary" disabled={working} onClick={()=>void submitHistorical(item.id)}>Enviar a RI</button>}</div>)}
          {!intakes.length && <p className="muted">No hay cargas históricas ingresadas todavía.</p>}
        </div>
      </article>

      <article className="panel">
        <div className="panel-heading"><div><p className="eyebrow">Privado del Taller</p><h3>Reuniones</h3></div><span className="count-badge">{adminMeetings.length}</span></div>
        <form className="regularity-form" onSubmit={createAdministrativeMeeting}>
          <Field label="Fecha"><input type="date" required value={meetingDate} onChange={e=>setMeetingDate(e.target.value)} /></Field>
          <Field label="Título"><input required value={meetingTitle} onChange={e=>setMeetingTitle(e.target.value)} /></Field>
          <Field label="Propósito"><input value={meetingPurpose} onChange={e=>setMeetingPurpose(e.target.value)} /></Field>
          <button className="regularity-primary" type="submit" disabled={working}>Programar reunión</button>
        </form>
        <div className="lodge-secretariat-list">
          {adminMeetings.map(item=><div key={item.id}><div><strong>{item.title}</strong><span>{formatDate(item.meetingDate)} · {item.status==='held'?'Realizada':'Programada'}</span><small>{item.purpose||'Sin descripción adicional'}</small></div>{item.status==='scheduled'&&<button type="button" className="regularity-secondary" onClick={()=>void markMeetingHeld(item.id)}>Marcar realizada</button>}</div>)}
          {!adminMeetings.length && <p className="muted">No hay reuniones registradas.</p>}
        </div>
      </article>
    </div>

    <article className="panel lodge-secretariat-documents">
      <div className="panel-heading"><div><p className="eyebrow">Archivo institucional</p><h3>Documentos de Tenidas, Reuniones y Consejos</h3></div><span className="count-badge">{records.length} registros</span></div>
      <p className="form-note">Reuniones y Consejos son privados. En Tenidas, Gran Secretaría sólo recibe el extracto PDF remitido; nunca la plancha ni el acta completa.</p>
      <div className="lodge-secretariat-source">
        <Field label="Tipo"><select value={recordType} onChange={e=>setRecordType(e.target.value as LodgeSecretariatRecordType)}><option value="tenida">Tenida</option><option value="reunion">Reunión</option><option value="consejo">Consejo de Administración</option></select></Field>
        <Field label="Registro"><select value={sourceRecordId} onChange={e=>setSourceRecordId(e.target.value)}><option value="">Seleccione…</option>{sources.map(x=><option key={x.id} value={x.id}>{x.label}</option>)}</select></Field>
      </div>

      {currentSource && <div className="lodge-secretariat-document-actions">
        <div className="secretariat-document-card">
          <strong>Plancha de trabajo</strong><span>{planchaAllowed?'Opcional · PDF o Word · puede cargarse posteriormente':'No corresponde a este registro'}</span>
          {planchaAllowed && <><select aria-label="Hermano autor de la plancha" value={workPaperAuthorMemberId} onChange={e=>setWorkPaperAuthorMemberId(e.target.value)}><option value="">Hermano autor…</option>{members.map(m=><option key={m.id} value={m.id}>{m.displayName}</option>)}</select><FileButton disabled={!!uploadingKind} accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" label={currentRecord?.workPaperDocumentVersionId?'Reemplazar plancha':'Cargar plancha'} onFile={file=>void uploadDocument('work_paper',file)} /></>}
          {currentRecord?.workPaperDocumentVersionId && <small>Plancha vinculada al registro.</small>}
        </div>
        <div className="secretariat-document-card">
          <strong>Extracto</strong><span>PDF · obligatorio para remitir una Tenida a Gran Secretaría.</span>
          <FileButton disabled={!!uploadingKind} accept=".pdf,application/pdf" label={currentRecord?.extractDocumentVersionId?'Reemplazar extracto PDF':'Cargar extracto PDF'} onFile={file=>void uploadDocument('extract',file)} />
          {currentRecord?.extractDocumentVersionId && <small>Extracto PDF disponible.</small>}
        </div>
        <div className="secretariat-document-card">
          <strong>Acta completa</strong><span>Opcional · PDF o Word · siempre privada del Taller.</span>
          <FileButton disabled={!!uploadingKind} accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" label={currentRecord?.fullMinuteDocumentVersionId?'Reemplazar acta completa':'Cargar acta completa'} onFile={file=>void uploadDocument('full_minute',file)} />
          {currentRecord?.fullMinuteDocumentVersionId && <small>Acta completa vinculada.</small>}
        </div>
      </div>}

      {recordType==='tenida' && currentSource && <div className="lodge-secretariat-submit"><button type="button" className="regularity-primary" disabled={working||!canSubmitToGrandSecretariat||currentRecord?.status==='submitted'||currentRecord?.status==='received'} onClick={()=>void submitExtract()}>{currentRecord?.status==='received'?'Recibido por Gran Secretaría':currentRecord?.status==='submitted'?'Remitido a Gran Secretaría':'Remitir extracto a Gran Secretaría'}</button><small>Solo se remiten datos básicos de la Tenida y el extracto PDF.</small></div>}
    </article>
  </section>
}

function Field({label,children}:{label:string;children:ReactNode}){return <label className="regularity-field"><span>{label}</span>{children}</label>}
function FileButton({label,accept,disabled,onFile}:{label:string;accept:string;disabled:boolean;onFile:(file:File)=>void}){return <label className={disabled?'regularity-secondary disabled':'regularity-secondary'}>{label}<input hidden type="file" accept={accept} disabled={disabled} onChange={e=>{const file=e.target.files?.[0];if(file)onFile(file);e.currentTarget.value=''}} /></label>}
function today(){return new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())}
function formatDate(value:string){const [y,m,d]=value.split('-');return y&&m&&d?`${d}-${m}-${y}`:value}
function degreeLabel(value:string){return value==='master'?'Maestro':value==='fellowcraft'?'Compañero':'Aprendiz'}
function intakeStatusLabel(value:string){return value==='draft'?'Borrador':value==='submitted'?'Enviado a RI':value==='observed'?'Observado por RI':value==='approved'?'Validado por RI':'Rechazado'}
function meetingTypeLabel(value:string){return value==='regular'?'Tenida Regular':value==='solemn'?'Tenida Solemne':value==='instruction'?'Tenida de Instrucción':value==='anniversary'?'Tenida de Aniversario':value==='funeral'?'Tenida Fúnebre':'Tenida Especial'}
function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible completar la operación.'}
function downloadBlob(blob:Blob,fileName:string){const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=fileName;a.click();URL.revokeObjectURL(url)}
function isPdfOrWord(file:File){return file.type==='application/pdf'||file.type==='application/vnd.openxmlformats-officedocument.wordprocessingml.document'}
function normalizeInstitutionalFile(file:File){
  if(file.type) return file
  const lower=file.name.toLowerCase()
  const type=lower.endsWith('.pdf')?'application/pdf':lower.endsWith('.docx')?'application/vnd.openxmlformats-officedocument.wordprocessingml.document':''
  return type?new File([file],file.name,{type,lastModified:file.lastModified}):file
}
