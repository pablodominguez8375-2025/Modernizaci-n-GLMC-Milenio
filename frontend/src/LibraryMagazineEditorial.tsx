import { Fragment, useCallback, useEffect, useState, type FormEvent } from 'react'
import { ActionBar, ActionDrawer, ConfirmAction } from './actionKit'
import { DocumentApiHttpError, type DocumentApiClient, type EditorialMagazine, type EditorialType } from './api/documentApi'

const statusLabel: Record<string, string> = { draft: 'Borrador', active: 'Listo para publicar', published: 'Publicada', retired: 'Retirada' }
const typeLabel: Record<EditorialType, string> = { magazine: 'Revista', bulletin: 'Boletín' }
const degreeOptions = [{ v: '', t: 'General (todos los grados)' }, { v: '1', t: '1° grado' }, { v: '2', t: '2° grado' }, { v: '3', t: '3° grado' }]
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago' }).format(new Date())
const currentMonth = () => today().slice(0, 7)
const isBulletin = (value: string) => ['bulletin', 'bulletins', 'boletin', 'boletines', 'boletín'].includes(value.trim().toLowerCase())
const periodLabel = (value: string | null) => {
  if (!value) return ''
  const text = new Intl.DateTimeFormat('es-CL', { month: 'long', year: 'numeric', timeZone: 'America/Santiago' }).format(new Date(`${value}T12:00:00-03:00`))
  return text.charAt(0).toUpperCase() + text.slice(1)
}
const message = (reason: unknown) => reason instanceof DocumentApiHttpError && reason.status === 409
  ? `${reason.message || 'La operación entra en conflicto con el estado actual.'} Actualice la lista e intente de nuevo.`
  : reason instanceof DocumentApiHttpError && reason.status === 415
    ? 'El Boletín se sube sólo en formato PDF.'
    : reason instanceof Error ? reason.message : 'No fue posible completar la operación.'

type Props = {
  documentApi: DocumentApiClient
  onChanged: () => void
  /** Bibliotecario fija el grado; Comunicaciones publica como General. */
  editorRole?: 'librarian' | 'communications' | null
}

/** Revista y Boletín reales (API editorial). Solo se muestra a quien tiene permiso editorial; nunca simula éxito local. */
export default function LibraryMagazineEditorial({ documentApi, onChanged, editorRole = null }: Props) {
  const [allowed, setAllowed] = useState<boolean | null>(null)
  const [items, setItems] = useState<EditorialMagazine[]>([])
  const [busy, setBusy] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)
  const [fileName, setFileName] = useState('')
  const [kind, setKind] = useState<EditorialType>(editorRole === 'communications' ? 'bulletin' : 'magazine')
  const canSetDegree = editorRole === 'librarian'

  const load = useCallback(async () => {
    try {
      const response = await documentApi.listEditableMagazines()
      setItems(response.items); setAllowed(true)
    } catch (reason) {
      if (reason instanceof DocumentApiHttpError && (reason.status === 403 || reason.status === 401)) setAllowed(false)
      else { setAllowed(true); setNotice({ tone: 'error', text: message(reason) }) }
    }
  }, [documentApi])
  useEffect(() => { void load() }, [load])

  async function stageFile(documentId: string, file: File, documentType: string) {
    if (isBulletin(documentType) && file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf'))
      throw new Error('El Boletín se sube sólo en formato PDF.')
    const version = await documentApi.createVersion(documentId, { originalFileName: file.name, contentType: file.type || 'application/pdf', sizeBytes: file.size })
    await documentApi.uploadVersionContent(version.id, file)
    const scanned = await documentApi.scanVersionContent(version.id)
    if (scanned.processingStatus !== 'available') throw new Error('El archivo no quedó disponible tras el análisis de seguridad.')
    return version.id
  }

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const file = (form.elements.namedItem('file') as HTMLInputElement).files?.[0]
    const text = (key: string) => String(data.get(key) ?? '').trim() || null
    setBusy('create'); setNotice(null)
    let documentId: string | null = null
    try {
      const collections = await documentApi.getCollections()
      const collection = collections.items.find(item => item.scope === 'order' && item.status === 'active')
      if (!collection) throw new Error('No hay una colección activa de la Orden para publicaciones. Avise al administrador.')
      const created = await documentApi.createEditorialMagazine(collection.id, text('title') ?? typeLabel[kind], kind)
      documentId = created.documentId
      const degree = canSetDegree ? text('degree') : null
      await documentApi.setLibraryCatalogMetadata(documentId, kind === 'bulletin'
        ? { minimumDegreeRequired: degree ? Number(degree) : null, authorName: text('author'), documentDate: `${text('month') ?? currentMonth()}-01`, shortDescription: text('description'), abstractText: text('summary') }
        : { minimumDegreeRequired: degree ? Number(degree) : null, authorName: text('author'), documentDate: text('date') ?? today(), topic: text('topic'), edition: text('edition'), shortDescription: text('description'), abstractText: text('summary') })
      if (file) await stageFile(documentId, file, kind)
      setNotice({ tone: 'ok', text: file ? `${typeLabel[kind]} guardado como borrador con su archivo. Publíquelo desde la lista cuando esté listo.` : 'Borrador guardado. Falta cargar el archivo (botón «Cargar archivo» en la lista).' })
      form.reset(); setFileName('')
    } catch (reason) {
      setNotice({ tone: 'error', text: `${documentId ? 'El borrador se creó, pero un paso falló: ' : ''}${message(reason)}` })
    } finally { setBusy(null); await load() }
  }

  async function attach(item: EditorialMagazine, file: File) {
    setBusy(item.documentId); setNotice(null)
    try { await stageFile(item.documentId, file, item.documentType); setNotice({ tone: 'ok', text: `Archivo cargado y verificado para «${item.title}».` }) }
    catch (reason) { setNotice({ tone: 'error', text: message(reason) }) }
    finally { setBusy(null); await load() }
  }

  async function publish(item: EditorialMagazine) {
    setBusy(item.documentId); setNotice(null)
    try {
      const versions = await documentApi.listMagazineVersions(item.documentId)
      const ready = versions.find(version => version.processingStatus === 'available')
      if (!ready) throw new Error('Aún no hay un archivo verificado y disponible. Cargue el archivo primero.')
      await documentApi.publishEditorialMagazine(item.documentId, ready.id)
      setNotice({ tone: 'ok', text: `«${item.title}» publicado. Ya lo ven los hermanos según su grado.` }); onChanged()
    } catch (reason) { setNotice({ tone: 'error', text: reason instanceof DocumentApiHttpError && reason.status === 403 ? 'Su perfil no tiene permiso para publicar. Puede preparar el borrador y cargar el archivo; publica el Bibliotecario o Comunicaciones.' : message(reason) }) }
    finally { setBusy(null); await load() }
  }

  async function unpublish(item: EditorialMagazine) {
    setBusy(item.documentId); setNotice(null)
    try { await documentApi.unpublishEditorialMagazine(item.documentId); setNotice({ tone: 'ok', text: `«${item.title}» ya no está publicado. El historial se conserva.` }); onChanged() }
    catch (reason) { setNotice({ tone: 'error', text: reason instanceof DocumentApiHttpError && reason.status === 403 ? 'Su perfil no tiene permiso para retirar esta publicación.' : message(reason) }) }
    finally { setBusy(null); await load() }
  }

  if (!allowed) return null
  return <>
    {notice && <p className={notice.tone === 'ok' ? 'library-admin-notice' : 'error-banner'} role={notice.tone === 'ok' ? 'status' : 'alert'}>{notice.text}</p>}
    <ActionBar>
      <ActionDrawer label="Subir revista o boletín" wide title="Subir revista o boletín" description="Horizonte Masónico o el Boletín mensual de actividades de la Orden (PDF). Se guarda como borrador; se publica desde «Revistas y boletines».">
        <form className="library-admin-form" onSubmit={event => void create(event)}>
          <label><span>Tipo de publicación</span><select value={kind} onChange={event => { setKind(event.target.value as EditorialType); setFileName('') }}><option value="bulletin">Boletín mensual</option><option value="magazine">Revista Horizonte Masónico</option></select></label>
          <label><span>Archivo PDF</span><input name="file" type="file" accept="application/pdf,.pdf" required={kind === 'bulletin'} onChange={event => setFileName(event.target.files?.[0]?.name ?? '')} />{fileName && <small>Seleccionado: {fileName}</small>}</label>
          <label><span>Título</span><input name="title" required maxLength={200} placeholder={kind === 'bulletin' ? 'Boletín de la Orden · Octubre 2026' : 'Horizonte Masónico · N° 4'} /></label>
          {kind === 'bulletin' ? <Fragment key="bulletin">
            <label><span>Mes y año que informa</span><input name="month" type="month" required defaultValue={currentMonth()} /></label>
            <label><span>Responsable</span><input name="author" maxLength={160} defaultValue="Comunicaciones" /></label>
          </Fragment> : <Fragment key="magazine">
            <label><span>N° de edición</span><input name="edition" maxLength={60} placeholder="N° 4" /></label>
            <label><span>Tema principal</span><input name="topic" maxLength={120} /></label>
            <label><span>Autor o responsable</span><input name="author" maxLength={160} /></label>
            <label><span>Fecha</span><input name="date" type="date" defaultValue={today()} /></label>
          </Fragment>}
          {canSetDegree && <label><span>Grado mínimo de acceso</span><select name="degree" defaultValue="">{degreeOptions.map(option => <option key={option.v} value={option.v}>{option.t}</option>)}</select></label>}
          <label className="wide"><span>{kind === 'bulletin' ? 'Resumen de actividades del mes' : 'Descripción breve'}</span><textarea name="description" rows={2} maxLength={800} required={kind === 'bulletin'} /></label>
          {kind === 'magazine' && <label className="wide"><span>En este número</span><textarea name="summary" rows={3} maxLength={1200} /></label>}
          {!canSetDegree && <p className="library-admin-empty">Se publica para todos los grados (General). El grado mínimo lo ajusta el Bibliotecario si corresponde.</p>}
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy === 'create'}>{busy === 'create' ? 'Guardando…' : 'Guardar borrador'}</button></div>
        </form>
      </ActionDrawer>
      <ActionDrawer label={`Revistas y boletines${items.length ? ` (${items.length})` : ''}`} wide title="Revistas y boletines en preparación y publicados" description="Estado de cada número. Publican y retiran el Bibliotecario y Comunicaciones.">
        <div className="library-admin-list">
          {items.length === 0 && <p className="library-admin-empty">Aún no hay publicaciones cargadas.</p>}
          {items.map(item => {
            const bulletin = isBulletin(item.documentType)
            return <article key={item.documentId} className="library-admin-card">
              <div><strong>{item.title}</strong><span>{bulletin ? 'Boletín' : 'Revista'} · {statusLabel[item.status] ?? item.status}{!bulletin && item.edition ? ` · ${item.edition}` : ''}{item.documentDate ? ` · ${bulletin ? periodLabel(item.documentDate) : item.documentDate}` : ''}</span>{item.shortDescription && <p>{item.shortDescription}</p>}</div>
              <div className="library-admin-row-actions">
                {item.status !== 'published' && <label className="action-trigger secondary library-file-button">Cargar archivo<input type="file" accept="application/pdf,.pdf" hidden disabled={busy === item.documentId} onChange={event => { const file = event.target.files?.[0]; if (file) void attach(item, file); event.target.value = '' }} /></label>}
                {item.status !== 'published' && <ConfirmAction label="Publicar" message="Quedará visible para los hermanos autorizados según su grado." disabled={busy === item.documentId} onConfirm={() => void publish(item)} />}
                {item.status === 'published' && <ConfirmAction label="Retirar publicación" message="Dejará de verse en la Biblioteca. El historial se conserva y se puede volver a publicar." disabled={busy === item.documentId} onConfirm={() => void unpublish(item)} />}
              </div>
            </article>
          })}
        </div>
      </ActionDrawer>
    </ActionBar>
  </>
}
