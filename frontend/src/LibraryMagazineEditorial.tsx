import { Fragment, useCallback, useEffect, useState, type FormEvent } from 'react'
import { ActionBar, ActionDrawer, ConfirmAction } from './actionKit'
import { DocumentApiHttpError, type DocumentApiClient, type EditorialMagazine, type EditorialType } from './api/documentApi'

const statusLabel: Record<string, string> = { draft: 'Borrador', active: 'Listo para publicar', published: 'Publicada', retired: 'Retirada' }
const typeLabel: Record<EditorialType, string> = { magazine: 'Revista', bulletin: 'Boletín' }
const profileLabel: Record<string, string> = { librarian: 'el Bibliotecario', communications: 'Comunicaciones' }
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
  /** Revista y Boletín son siempre General (todos los grados), para cualquier perfil. */
  editorRole?: 'librarian' | 'communications' | null
}

/** Revista y Boletín reales (API editorial). Solo se muestra a quien tiene permiso editorial; nunca simula éxito local. */
export default function LibraryMagazineEditorial({ documentApi, onChanged, editorRole = null }: Props) {
  const [allowed, setAllowed] = useState<boolean | null>(null)
  // En demostración la titularidad sigue al perfil elegido; con servidor real decide `canCorrect`.
  const canCorrect = (item: EditorialMagazine) => documentApi.useMocks && editorRole ? item.publisherProfile === editorRole : item.canCorrect !== false
  const [items, setItems] = useState<EditorialMagazine[]>([])
  const [busy, setBusy] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)
  const [fileName, setFileName] = useState('')
  const [kind, setKind] = useState<EditorialType>(editorRole === 'communications' ? 'bulletin' : 'magazine')

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
      await documentApi.setLibraryCatalogMetadata(documentId, kind === 'bulletin'
        ? { minimumDegreeRequired: null, authorName: text('author'), documentDate: `${text('month') ?? currentMonth()}-01`, shortDescription: text('description'), abstractText: text('summary') }
        : { minimumDegreeRequired: null, authorName: text('author'), documentDate: text('date') ?? today(), topic: text('topic'), edition: text('edition'), shortDescription: text('description'), abstractText: text('summary') })
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

  /** Reemplaza el archivo de una publicación vigente sin despublicarla: sigue visible con la versión actual hasta publicar la nueva. */
  async function replaceFile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const item = items.find(entry => entry.documentId === String(data.get('document')))
    const file = (form.elements.namedItem('file') as HTMLInputElement).files?.[0]
    const reason = String(data.get('reason') ?? '').trim()
    if (!item || !file) return
    if (reason.length < 10) { setNotice({ tone: 'error', text: 'Escriba el motivo del reemplazo (al menos 10 caracteres).' }); return }
    setBusy(item.documentId); setNotice(null)
    try {
      const versionId = await stageFile(item.documentId, file, item.documentType)
      await documentApi.publishEditorialMagazine(item.documentId, versionId, reason)
      setNotice({ tone: 'ok', text: `«${item.title}» ahora muestra el archivo nuevo. La versión anterior se conserva en el historial.` }); form.reset(); setFileName(''); onChanged()
    } catch (reason2) { setNotice({ tone: 'error', text: reason2 instanceof DocumentApiHttpError && reason2.status === 403 ? 'Solo el perfil que publicó puede corregir esta publicación.' : message(reason2) }) }
    finally { setBusy(null); await load() }
  }

  async function unpublish(item: EditorialMagazine) {
    setBusy(item.documentId); setNotice(null)
    try { await documentApi.unpublishEditorialMagazine(item.documentId); setNotice({ tone: 'ok', text: `«${item.title}» ya no está publicado. El historial se conserva.` }); onChanged() }
    catch (reason) { setNotice({ tone: 'error', text: reason instanceof DocumentApiHttpError && reason.status === 403 ? 'Su perfil no tiene permiso para retirar esta publicación.' : message(reason) }) }
    finally { setBusy(null); await load() }
  }

  if (!allowed) return null
  const replaceable = items.filter(item => item.status === 'published' && canCorrect(item))
  return <>
    {notice && <p className={notice.tone === 'ok' ? 'library-admin-notice' : 'error-banner'} role={notice.tone === 'ok' ? 'status' : 'alert'}>{notice.text}</p>}
    <ActionBar>
      {!documentApi.useMocks && <ActionDrawer label="Subir revista o boletín" wide title="Subir revista o boletín" description="Horizonte Masónico o el Boletín mensual de actividades de la Orden (PDF). Se guarda como borrador; se publica desde «Revistas y boletines».">
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
          <label className="wide"><span>{kind === 'bulletin' ? 'Resumen de actividades del mes' : 'Descripción breve'}</span><textarea name="description" rows={2} maxLength={800} required={kind === 'bulletin'} /></label>
          {kind === 'magazine' && <label className="wide"><span>En este número</span><textarea name="summary" rows={3} maxLength={1200} /></label>}
          <p className="library-admin-empty">La revista y el boletín son visibles para todos los grados (General).</p>
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy === 'create'}>{busy === 'create' ? 'Guardando…' : 'Guardar borrador'}</button></div>
        </form>
      </ActionDrawer>}
      <ActionDrawer label={`Revistas y boletines${items.length ? ` (${items.length})` : ''}`} wide title="Revistas y boletines en preparación y publicados" description="Estado de cada número. Cada perfil corrige solo lo que publicó: el Bibliotecario sus publicaciones y Comunicaciones las suyas.">
        <div className="library-admin-list">
          {items.length === 0 && <p className="library-admin-empty">Aún no hay publicaciones cargadas.</p>}
          {items.map(item => {
            const bulletin = isBulletin(item.documentType)
            return <article key={item.documentId} className="library-admin-card">
              <div><strong>{item.title}</strong><span>{bulletin ? 'Boletín' : 'Revista'} · {statusLabel[item.status] ?? item.status}{!bulletin && item.edition ? ` · ${item.edition}` : ''}{item.documentDate ? ` · ${bulletin ? periodLabel(item.documentDate) : item.documentDate}` : ''}</span>{item.shortDescription && <p>{item.shortDescription}</p>}</div>
              {!canCorrect(item) ? <p className="library-admin-empty">Publicada por {profileLabel[item.publisherProfile ?? ''] ?? 'otro perfil'}. Solo ese perfil puede corregirla.</p> : <div className="library-admin-row-actions">
                {item.status !== 'published' && <label className="action-trigger secondary library-file-button">Cargar archivo<input type="file" accept="application/pdf,.pdf" hidden disabled={busy === item.documentId} onChange={event => { const file = event.target.files?.[0]; if (file) void attach(item, file); event.target.value = '' }} /></label>}
                {item.status !== 'published' && <ConfirmAction label="Publicar" message="Quedará visible para los hermanos autorizados según su grado." disabled={busy === item.documentId} onConfirm={() => void publish(item)} />}
                {item.status === 'published' && <ConfirmAction label="Retirar publicación" message="Dejará de verse en la Biblioteca. El historial se conserva y se puede volver a publicar." disabled={busy === item.documentId} onConfirm={() => void unpublish(item)} />}
              </div>}
            </article>
          })}
        </div>
      </ActionDrawer>
      {replaceable.length > 0 && <ActionDrawer label="Reemplazar archivo publicado" tone="secondary" wide title="Reemplazar el archivo de una publicación" description="La publicación sigue visible con el archivo actual hasta que el nuevo esté listo. El archivo anterior queda guardado en el historial.">
        <form className="library-admin-form" onSubmit={event => void replaceFile(event)}>
          <label className="wide"><span>Publicación</span><select name="document" required defaultValue="">
            <option value="" disabled>Elija la publicación…</option>
            {replaceable.map(item => <option key={item.documentId} value={item.documentId}>{item.title}</option>)}
          </select></label>
          <label className="wide"><span>Archivo PDF corregido</span><input name="file" type="file" accept="application/pdf,.pdf" required /></label>
          <label className="wide"><span>Motivo del reemplazo</span><textarea name="reason" rows={3} required minLength={10} maxLength={2000} placeholder="Ej.: se corrigió una fecha en la página 3." /></label>
          <p className="library-admin-empty">Solo aparecen las publicaciones que usted publicó. Queda registrado quién, cuándo y por qué.</p>
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy !== null}>{busy ? 'Reemplazando…' : 'Reemplazar archivo'}</button></div>
        </form>
      </ActionDrawer>}
    </ActionBar>
  </>
}
