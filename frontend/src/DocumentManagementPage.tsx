import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { type DocumentApiClient, type DocumentCollection, type DocumentListItem, type InstitutionalDocument } from './api/documentApi'
import { type OrganizationOption, type PmgmApiClient, type SessionProfile } from './api/pmgmApi'
import './documents.css'

export default function DocumentManagementPage({ api, documentApi }: { api: PmgmApiClient; documentApi: DocumentApiClient }) {
  const [accessScope, setAccessScope] = useState<SessionProfile['accessScope']>('authenticated')
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [scopeOrganizationId, setScopeOrganizationId] = useState('')
  const [collections, setCollections] = useState<DocumentCollection[]>([])
  const [collectionId, setCollectionId] = useState('')
  const [documents, setDocuments] = useState<DocumentListItem[]>([])
  const [documentId, setDocumentId] = useState('')
  const [selected, setSelected] = useState<InstitutionalDocument | null>(null)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const applyCollections = (items: DocumentCollection[]) => {
    setCollections(items)
    setCollectionId(previous => items.some(item => item.id === previous) ? previous : items[0]?.id ?? '')
  }

  const reloadCollections = async () => {
    const response = await documentApi.getCollections(scopeOrganizationId || undefined)
    applyCollections(response.items)
  }

  useEffect(() => {
    let active = true
    Promise.all([api.getOrganizationOptions(), api.getSessionProfile()])
      .then(async ([organizationResponse, profile]) => {
        if (!active) return
        const workshops = organizationResponse.items.filter(item => item.type.toLowerCase() !== 'order')
        const scopedId = profile.accessScope === 'organization' ? workshops[0]?.id ?? '' : ''
        setAccessScope(profile.accessScope); setOrganizations(workshops); setScopeOrganizationId(scopedId)
        const collectionResponse = await documentApi.getCollections(scopedId || undefined)
        if (active) applyCollections(collectionResponse.items)
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, documentApi])

  useEffect(() => {
    if (!collectionId) { setDocuments([]); setDocumentId(''); setSelected(null); return }
    let active = true
    documentApi.getDocuments(collectionId)
      .then(response => { if (active) { setDocuments(response.items); setDocumentId(previous => response.items.some(item => item.id === previous) ? previous : response.items[0]?.id ?? '') } })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [collectionId, documentApi])

  useEffect(() => {
    if (!documentId) { setSelected(null); return }
    let active = true
    documentApi.getDocument(documentId)
      .then(value => { if (active) setSelected(value) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [documentId, documentApi])

  const selectedCollection = collections.find(item => item.id === collectionId) ?? null
  const visibleOrganizations = accessScope === 'organization' ? organizations.filter(item => item.id === scopeOrganizationId) : organizations

  const run = async (operation: () => Promise<void>, success: string) => {
    setWorking(true); setError(null); setMessage(null)
    try { await operation(); setMessage(success) } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  return <>
    <section className="page-heading"><div><p className="eyebrow">Repositorio institucional</p><h1>Gestor Documental</h1><p>Administra colecciones, documentos y versiones sin exponer las claves físicas del almacenamiento.</p></div><span className="count-badge">{loading ? 'cargando…' : `${collections.length} colecciones`}</span></section>
    {error && <div className="error-banner" role="alert"><strong>Operación documental no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <section className="document-management-grid">
      <article className="panel"><p className="eyebrow">Estructura</p><h2>Colecciones</h2><CollectionForm organizations={visibleOrganizations} orderScope={accessScope === 'order'} defaultOrganizationId={scopeOrganizationId} disabled={working} onCreate={payload => run(async () => { await documentApi.createCollection(payload); await reloadCollections() }, 'Colección creada y auditada.')} /><label className="document-field"><span>Colección activa</span><select value={collectionId} onChange={event => { setCollectionId(event.target.value); setSelected(null) }}><option value="">Seleccione…</option>{collections.map(item => <option key={item.id} value={item.id}>{item.name} · {item.scope === 'order' ? 'Orden' : 'Taller'}</option>)}</select></label></article>

      <article className="panel"><p className="eyebrow">Documento lógico</p><h2>Registrar documento</h2>{selectedCollection ? <DocumentForm disabled={working} onCreate={payload => run(async () => { const created = await documentApi.createDocument(selectedCollection.id, payload); const response = await documentApi.getDocuments(selectedCollection.id); setDocuments(response.items); setDocumentId(created.id); setSelected(created) }, 'Documento creado. Ahora puede registrar su primera versión.')} /> : <p className="document-muted">Seleccione o cree una colección para continuar.</p>}</article>

      <article className="panel document-wide"><div className="panel-heading"><div><p className="eyebrow">Contenido</p><h2>Documentos de la colección</h2></div><span className="count-badge">{documents.length}</span></div>{documents.length === 0 ? <div className="empty-state"><strong>Sin documentos registrados.</strong></div> : <div className="document-table">{documents.map(item => <button type="button" key={item.id} className={documentId === item.id ? 'document-row selected' : 'document-row'} onClick={() => setDocumentId(item.id)}><span><strong>{item.title}</strong><small>{item.documentType.replaceAll('_', ' ')}</small></span><span className={`document-state ${item.status}`}>{statusLabel(item.status)}</span></button>)}</div>}</article>

      <article className="panel document-wide"><p className="eyebrow">Versionado inmutable</p><h2>{selected?.title ?? 'Detalle del documento'}</h2>{!selected ? <p className="document-muted">Seleccione un documento para revisar sus versiones.</p> : <DocumentDetail document={selected} disabled={working} onCreateVersion={payload => run(async () => { await documentApi.createVersion(selected.id, payload); setSelected(await documentApi.getDocument(selected.id)) }, 'Metadata de versión registrada. El archivo queda pendiente del almacenamiento y escaneo.')} onPublish={versionId => run(async () => { await documentApi.publishDocument(selected.id, versionId); setSelected(await documentApi.getDocument(selected.id)); setDocuments((await documentApi.getDocuments(selected.collectionId)).items) }, 'Versión publicada en Biblioteca.')} onUnpublish={() => run(async () => { await documentApi.unpublishDocument(selected.id); setSelected(await documentApi.getDocument(selected.id)); setDocuments((await documentApi.getDocuments(selected.collectionId)).items) }, 'Publicación retirada de Biblioteca.')} />}</article>
    </section>
  </>
}

function CollectionForm({ organizations, orderScope, defaultOrganizationId, disabled, onCreate }: { organizations: OrganizationOption[]; orderScope: boolean; defaultOrganizationId: string; disabled: boolean; onCreate: (payload: { code: string; name: string; description: string | null; scope: 'order' | 'organization'; organizationId: string | null }) => void }) {
  const initialScope: 'order' | 'organization' = orderScope ? 'order' : 'organization'
  const [code, setCode] = useState(''); const [name, setName] = useState(''); const [description, setDescription] = useState(''); const [scope, setScope] = useState<'order' | 'organization'>(initialScope); const [organizationId, setOrganizationId] = useState(defaultOrganizationId)
  useEffect(() => { if (!orderScope) { setScope('organization'); setOrganizationId(defaultOrganizationId) } }, [defaultOrganizationId, orderScope])
  const submit = (event: FormEvent) => { event.preventDefault(); onCreate({ code, name, description: description.trim() || null, scope, organizationId: scope === 'organization' ? organizationId || null : null }); setCode(''); setName(''); setDescription('') }
  return <form className="document-form" onSubmit={submit}><Field label="Código"><input required maxLength={120} value={code} onChange={event => setCode(event.target.value)} placeholder="BIB-HIST" /></Field><Field label="Nombre"><input required maxLength={240} value={name} onChange={event => setName(event.target.value)} /></Field>{orderScope && <Field label="Alcance"><select value={scope} onChange={event => setScope(event.target.value as 'order' | 'organization')}><option value="order">Toda la Orden</option><option value="organization">Taller específico</option></select></Field>}{scope === 'organization' && <Field label="Taller"><select required value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}{item.number ? ` · Nº ${item.number}` : ''}</option>)}</select></Field>}<Field label="Descripción"><textarea rows={2} maxLength={2000} value={description} onChange={event => setDescription(event.target.value)} /></Field><button className="document-primary" disabled={disabled} type="submit">Crear colección</button></form>
}

function DocumentForm({ disabled, onCreate }: { disabled: boolean; onCreate: (payload: { title: string; documentType: string; classification: 'internal' | 'confidential' | 'sensitive' | 'restricted'; accessPolicy: 'library_authenticated' | 'organization_authenticated' | 'management_only' }) => void }) {
  const [title, setTitle] = useState(''); const [documentType, setDocumentType] = useState('publication'); const [classification, setClassification] = useState<'internal' | 'confidential' | 'sensitive' | 'restricted'>('internal'); const [accessPolicy, setAccessPolicy] = useState<'library_authenticated' | 'organization_authenticated' | 'management_only'>('library_authenticated')
  const submit = (event: FormEvent) => { event.preventDefault(); onCreate({ title, documentType, classification, accessPolicy }); setTitle('') }
  return <form className="document-form" onSubmit={submit}><Field label="Título"><input required maxLength={500} value={title} onChange={event => setTitle(event.target.value)} /></Field><Field label="Tipo documental"><input required maxLength={120} value={documentType} onChange={event => setDocumentType(event.target.value)} /></Field><Field label="Clasificación"><select value={classification} onChange={event => setClassification(event.target.value as typeof classification)}><option value="internal">Interno</option><option value="confidential">Confidencial</option><option value="sensitive">Sensible</option><option value="restricted">Restringido</option></select></Field><Field label="Política de acceso"><select value={accessPolicy} onChange={event => setAccessPolicy(event.target.value as typeof accessPolicy)}><option value="library_authenticated">Biblioteca · autenticados</option><option value="organization_authenticated">Sólo Taller</option><option value="management_only">Sólo gestión · no publicable</option></select></Field><button className="document-primary" disabled={disabled} type="submit">Registrar documento</button></form>
}

function DocumentDetail({ document, disabled, onCreateVersion, onPublish, onUnpublish }: { document: InstitutionalDocument; disabled: boolean; onCreateVersion: (payload: { originalFileName: string; contentType: string; sizeBytes: number }) => void; onPublish: (versionId: string) => void; onUnpublish: () => void }) {
  const [fileName, setFileName] = useState(''); const [contentType, setContentType] = useState('application/pdf'); const [sizeBytes, setSizeBytes] = useState('')
  const available = useMemo(() => document.versions.filter(item => item.processingStatus === 'available'), [document.versions])
  const submit = (event: FormEvent) => { event.preventDefault(); onCreateVersion({ originalFileName: fileName, contentType, sizeBytes: Number(sizeBytes) }); setFileName(''); setSizeBytes('') }
  return <div className="document-detail"><div className="document-summary"><span className={`document-state ${document.status}`}>{statusLabel(document.status)}</span><span>{document.collectionCode}</span><span>{document.classification}</span><span>{accessLabel(document.accessPolicy)}</span>{document.status === 'published' && <button className="document-secondary" disabled={disabled} type="button" onClick={onUnpublish}>Retirar publicación</button>}</div><form className="document-version-form" onSubmit={submit}><Field label="Nombre original"><input required value={fileName} onChange={event => setFileName(event.target.value)} placeholder="documento.pdf" /></Field><Field label="MIME"><input required value={contentType} onChange={event => setContentType(event.target.value)} /></Field><Field label="Tamaño en bytes"><input required min={1} type="number" value={sizeBytes} onChange={event => setSizeBytes(event.target.value)} /></Field><button className="document-secondary" disabled={disabled} type="submit">Registrar versión</button></form><p className="document-hint">Registrar una versión crea metadata inmutable en estado <strong>pending_upload</strong>. No se simula la carga: el binario se conectará mediante `IDocumentObjectStore`.</p><div className="version-list">{document.versions.length === 0 ? <div className="empty-state"><strong>Sin versiones.</strong></div> : document.versions.map(version => <div className="version-row" key={version.id}><div><strong>v{version.versionNumber} · {version.originalFileName}</strong><small>{version.contentType} · {formatBytes(version.sizeBytes)} · {formatChile(version.createdAtUtc)}</small></div><span className={`document-state ${version.processingStatus}`}>{processingLabel(version.processingStatus)}</span>{version.processingStatus === 'available' && document.publishedVersionId !== version.id && <button className="document-primary compact" disabled={disabled} type="button" onClick={() => onPublish(version.id)}>Publicar</button>}{document.publishedVersionId === version.id && <span className="published-marker">Publicada</span>}</div>)}</div>{available.length === 0 && <div className="document-info-strip"><strong>Pipeline pendiente</strong><span>Ninguna versión está disponible para publicar hasta completar almacenamiento, SHA-256 y escaneo antimalware.</span></div>}</div>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="document-field"><span>{label}</span>{children}</label> }
function statusLabel(value: string) { return value === 'published' ? 'Publicado' : value === 'active' ? 'Activo' : value === 'retired' ? 'Retirado' : 'Borrador' }
function processingLabel(value: string) { return value === 'pending_upload' ? 'Pendiente de carga' : value === 'uploaded' ? 'Cargado' : value === 'scanning' ? 'Escaneando' : value === 'available' ? 'Disponible' : 'Rechazado' }
function accessLabel(value: string) { return value === 'library_authenticated' ? 'Biblioteca' : value === 'organization_authenticated' ? 'Sólo Taller' : 'Sólo gestión' }
function formatBytes(value: number) { if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`; return `${(value / 1024 / 1024).toFixed(1)} MB` }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
