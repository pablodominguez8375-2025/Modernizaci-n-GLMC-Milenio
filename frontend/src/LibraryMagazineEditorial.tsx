import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ActionBar, ActionDrawer, ConfirmAction } from './actionKit'
import { DocumentApiHttpError, type DocumentApiClient, type EditorialMagazine } from './api/documentApi'

const statusLabel: Record<string, string> = { draft: 'Borrador', active: 'Listo para publicar', published: 'Publicada', retired: 'Retirada' }
const degreeOptions = [{ v: '', t: 'General (todos los grados)' }, { v: '1', t: '1° grado' }, { v: '2', t: '2° grado' }, { v: '3', t: '3° grado' }]
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago' }).format(new Date())
const message = (reason: unknown) => reason instanceof DocumentApiHttpError && reason.status === 409
  ? `${reason.message || 'La operación entra en conflicto con el estado actual.'} Actualice la lista e intente de nuevo.`
  : reason instanceof Error ? reason.message : 'No fue posible completar la operación.'

/** Revistas reales (API editorial). Solo se muestra a quien tiene permiso editorial; nunca simula éxito local. */
export default function LibraryMagazineEditorial({ documentApi, onChanged }: { documentApi: DocumentApiClient; onChanged: () => void }) {
  const [allowed, setAllowed] = useState<boolean | null>(null)
  const [items, setItems] = useState<EditorialMagazine[]>([])
  const [busy, setBusy] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)
  const [fileName, setFileName] = useState('')

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

  async function stageFile(documentId: string, file: File) {
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
      if (!collection) throw new Error('No hay una colección activa de la Orden para revistas. Avise al administrador.')
      const created = await documentApi.createEditorialMagazine(collection.id, text('title') ?? 'Revista')
      documentId = created.documentId
      const degree = text('degree')
      await documentApi.setLibraryCatalogMetadata(documentId, { minimumDegreeRequired: degree ? Number(degree) : null, authorName: text('author'), documentDate: text('date') ?? today(), topic: text('topic'), edition: text('edition'), shortDescription: text('description'), abstractText: text('summary') })
      if (file) await stageFile(documentId, file)
      setNotice({ tone: 'ok', text: file ? 'Número guardado como borrador con su archivo. Publíquelo desde la lista cuando esté listo.' : 'Borrador guardado. Falta cargar el archivo (botón «Cargar archivo» en la lista).' })
      form.reset(); setFileName('')
    } catch (reason) {
      setNotice({ tone: 'error', text: `${documentId ? 'El borrador se creó, pero un paso falló: ' : ''}${message(reason)}` })
    } finally { setBusy(null); await load() }
  }

  async function attach(item: EditorialMagazine, file: File) {
    setBusy(item.documentId); setNotice(null)
    try { await stageFile(item.documentId, file); setNotice({ tone: 'ok', text: `Archivo cargado y verificado para «${item.title}».` }) }
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
      setNotice({ tone: 'ok', text: `«${item.title}» publicada. Ya la ven los hermanos según su grado.` }); onChanged()
    } catch (reason) { setNotice({ tone: 'error', text: reason instanceof DocumentApiHttpError && reason.status === 403 ? 'Su perfil no tiene permiso para publicar este número. Puede preparar el borrador y cargar el archivo; la publicación la realiza quien tenga ese permiso.' : message(reason) }) }
    finally { setBusy(null); await load() }
  }

  async function unpublish(item: EditorialMagazine) {
    setBusy(item.documentId); setNotice(null)
    try { await documentApi.unpublishEditorialMagazine(item.documentId); setNotice({ tone: 'ok', text: `«${item.title}» ya no está publicada. El historial se conserva.` }); onChanged() }
    catch (reason) { setNotice({ tone: 'error', text: reason instanceof DocumentApiHttpError && reason.status === 403 ? 'Su perfil no tiene permiso para retirar esta publicación.' : message(reason) }) }
    finally { setBusy(null); await load() }
  }

  if (!allowed) return null
  return <>
    {notice && <p className={notice.tone === 'ok' ? 'library-admin-notice' : 'error-banner'} role={notice.tone === 'ok' ? 'status' : 'alert'}>{notice.text}</p>}
    <ActionBar>
      <ActionDrawer label="Subir número de revista" wide title="Subir número de revista" description="Horizonte Masónico. Se guarda como borrador; se publica desde «Mis revistas» según su permiso.">
        <form className="library-admin-form" onSubmit={event => void create(event)}>
          <label><span>Archivo PDF</span><input name="file" type="file" accept="application/pdf,.pdf" onChange={event => setFileName(event.target.files?.[0]?.name ?? '')} />{fileName && <small>Seleccionado: {fileName}</small>}</label>
          <label><span>Título</span><input name="title" required maxLength={200} placeholder="Horizonte Masónico · N° 4" /></label>
          <label><span>N° de edición</span><input name="edition" maxLength={60} placeholder="N° 4" /></label>
          <label><span>Tema principal</span><input name="topic" maxLength={120} /></label>
          <label><span>Autor o responsable</span><input name="author" maxLength={160} /></label>
          <label><span>Fecha</span><input name="date" type="date" defaultValue={today()} /></label>
          <label><span>Grado mínimo de acceso</span><select name="degree" defaultValue="">{degreeOptions.map(option => <option key={option.v} value={option.v}>{option.t}</option>)}</select></label>
          <label className="wide"><span>Descripción breve</span><textarea name="description" rows={2} maxLength={800} /></label>
          <label className="wide"><span>En este número</span><textarea name="summary" rows={3} maxLength={1200} /></label>
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy === 'create'}>{busy === 'create' ? 'Guardando…' : 'Guardar borrador'}</button></div>
        </form>
      </ActionDrawer>
      <ActionDrawer label={`Mis revistas${items.length ? ` (${items.length})` : ''}`} wide title="Revistas en preparación y publicadas" description="Estado de cada número. Publicar y retirar dependen de su permiso (el Bibliotecario puede con todas; Comunicaciones con las propias).">
        <div className="library-admin-list">
          {items.length === 0 && <p className="library-admin-empty">Aún no hay números cargados.</p>}
          {items.map(item => <article key={item.documentId} className="library-admin-card">
            <div><strong>{item.title}</strong><span>{statusLabel[item.status] ?? item.status}{item.edition ? ` · ${item.edition}` : ''}{item.documentDate ? ` · ${item.documentDate}` : ''}</span>{item.shortDescription && <p>{item.shortDescription}</p>}</div>
            <div className="library-admin-row-actions">
              {item.status !== 'published' && <label className="action-trigger secondary library-file-button">Cargar archivo<input type="file" accept="application/pdf,.pdf" hidden disabled={busy === item.documentId} onChange={event => { const file = event.target.files?.[0]; if (file) void attach(item, file); event.target.value = '' }} /></label>}
              {item.status !== 'published' && <ConfirmAction label="Publicar" message="Quedará visible para los hermanos autorizados según su grado." disabled={busy === item.documentId} onConfirm={() => void publish(item)} />}
              {item.status === 'published' && <ConfirmAction label="Retirar publicación" message="Dejará de verse en la Biblioteca. El historial se conserva y se puede volver a publicar." disabled={busy === item.documentId} onConfirm={() => void unpublish(item)} />}
            </div>
          </article>)}
        </div>
      </ActionDrawer>
    </ActionBar>
  </>
}
