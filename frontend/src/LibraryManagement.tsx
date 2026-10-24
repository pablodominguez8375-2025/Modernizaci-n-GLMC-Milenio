import { useState, type FormEvent } from 'react'
import { ActionBar, ActionDrawer, ConfirmAction, HelpNote } from './actionKit'
import type { LibraryCatalogItem, WorkPaperReview } from './api/documentApi'

export type LibraryCategory = { value: string; label: string; base: boolean; future?: boolean; active: boolean }
export type LibrarySubmission = { id: string; kind: 'taller' | 'borrador'; title: string; documentType: string; author: string; lodge?: string; degree: number | null; description: string; sentAt: string; item: LibraryCatalogItem }

type Props = {
  /** Bibliotecario: toda la Biblioteca. Comunicaciones: sólo revista y Boletín mensual. */
  mode?: 'librarian' | 'communications'
  categories: LibraryCategory[]
  categoryActions: { create: (label: string) => Promise<void>; patch: (code: string, patch: { label?: string; active?: boolean }) => Promise<void>; reorder: (codes: string[]) => Promise<void> }
  counts: Record<string, number>
  submissions: LibrarySubmission[]
  onSubmission: (next: LibrarySubmission[]) => void
  onPublish: (item: LibraryCatalogItem) => void
  /** #588: planchas enviadas por los Talleres, esperando al Bibliotecario (datos reales del servidor). */
  reviews?: WorkPaperReview[]
  reviewActions?: { approve: (review: WorkPaperReview) => Promise<void>; returnWith: (review: WorkPaperReview, reason: string) => Promise<void>; download: (review: WorkPaperReview) => Promise<void> }
  /** En producción la carga de documentos nuevos aún no está habilitada: solo revisión y categorías. */
  uploadEnabled?: boolean
  collectionId: string
  notify: (message: string) => void
}

const editorialCategories = ['magazine', 'bulletin']
const currentMonth = () => today().slice(0, 7)
const degreeOptions = [{ v: '', t: 'General (todos los grados)' }, { v: '1', t: '1° grado' }, { v: '2', t: '2° grado' }, { v: '3', t: '3° grado' }]
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago' }).format(new Date())

export default function LibraryManagement({ mode = 'librarian', categories, categoryActions, counts, submissions, onSubmission, onPublish, reviews = [], reviewActions, uploadEnabled = true, collectionId, notify }: Props) {
  const isCommunications = mode === 'communications'
  const active = categories.filter(c => c.active && !c.future && (!isCommunications || editorialCategories.includes(c.value)))
  const [type, setType] = useState(active[0]?.value ?? 'work_paper')
  const [fileName, setFileName] = useState('')
  const [reject, setReject] = useState<{ id: string; reason: string } | null>(null)
  const [newCategory, setNewCategory] = useState('')

  function buildItem(form: FormData, id: string): LibraryCatalogItem {
    const g = (k: string) => String(form.get(k) ?? '').trim() || null
    // Revista y Boletín: siempre General (todos los grados).
    const degree = editorialCategories.includes(type) ? null : g('degree')
    const month = g('month')
    return {
      id, collectionId, title: g('title') ?? 'Sin título', documentType: type, collectionName: 'Biblioteca Virtual', versionNumber: 1,
      contentType: 'application/pdf', sizeBytes: 1_200_000, publishedAtUtc: new Date().toISOString(), minimumDegreeRequired: degree ? Number(degree) : null,
      authorName: g('author'), authorLodgeName: g('lodge'), documentDate: type === 'bulletin' ? `${month ?? currentMonth()}-01` : g('date') ?? today(), topic: g('topic'), edition: g('edition'),
      shortDescription: g('description'), abstractText: g('description'), officialDocumentType: g('officialType'),
    }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null
    const item = buildItem(form, `demo-new-${Date.now()}`)
    if (submitter?.value === 'draft') {
      onSubmission([...submissions, { id: item.id, kind: 'borrador', title: item.title, documentType: type, author: item.authorName ?? (isCommunications ? 'Comunicaciones' : 'Bibliotecario'), degree: item.minimumDegreeRequired ?? null, description: item.shortDescription ?? '', sentAt: today(), item }])
      notify(`Borrador guardado. Aún no es visible para los hermanos: publíquelo desde «${isCommunications ? 'Mis borradores' : 'Por revisar y borradores'}».`)
    } else {
      onPublish(item)
      notify(`«${item.title}» publicado en ${categories.find(c => c.value === type)?.label ?? 'la Biblioteca'}. Queda visible según el grado indicado y registrado en auditoría.`)
    }
    setFileName('')
    event.currentTarget.reset()
  }

  const pending = reviews
  const drafts = submissions.filter(s => s.kind === 'borrador')
  const drop = (id: string) => onSubmission(submissions.filter(s => s.id !== id))

  const managed = categories.filter(c => !c.future && c.value !== 'official_document')
  function move(value: string, dir: -1 | 1) {
    const i = managed.findIndex(c => c.value === value); const j = i + dir
    if (j < 0 || j >= managed.length) return
    const next = managed.map(c => c.value);[next[i], next[j]] = [next[j], next[i]]; void categoryActions.reorder(next)
  }
  function addCategory(event: FormEvent) {
    event.preventDefault()
    const label = newCategory.trim(); if (!label) return
    void categoryActions.create(label); setNewCategory('')
  }

  const label = (v: string) => categories.find(c => c.value === v)?.label ?? v
  return <>
    <ActionBar>
      {uploadEnabled && <ActionDrawer label={isCommunications ? 'Subir revista o boletín' : 'Subir a la biblioteca'} wide title={isCommunications ? 'Subir revista o boletín' : 'Subir a la biblioteca'} description={isCommunications ? 'Comunicaciones publica la revista Horizonte Masónico y el Boletín mensual de actividades de la Orden (PDF). Puede guardarlo como borrador o publicarlo.' : 'Planchas, libros, revistas, boletines, documentos oficiales o la categoría que usted cree. Puede guardarlo como borrador o publicarlo.'}>
        <form className="library-admin-form" onSubmit={submit}>
          <label><span>Categoría</span><select value={type} onChange={e => setType(e.target.value)}>{active.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}</select></label>
          {type === 'bulletin'
            ? <label><span>Archivo PDF del boletín</span><input name="file" type="file" required accept="application/pdf,.pdf" onChange={e => setFileName(e.target.files?.[0]?.name ?? '')} />{fileName && <small>Seleccionado: {fileName}</small>}</label>
            : <label><span>Archivo (PDF, Word u otro permitido)</span><input type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx" onChange={e => setFileName(e.target.files?.[0]?.name ?? '')} />{fileName && <small>Seleccionado: {fileName}</small>}</label>}
          <label><span>Título</span><input name="title" required maxLength={200} placeholder={type === 'magazine' ? 'Horizonte Masónico · N° 4' : type === 'bulletin' ? 'Boletín de la Orden · Octubre 2026' : ''} /></label>
          <label><span>{type === 'work_paper' ? 'Hermano autor' : type === 'official_document' ? 'Emisor' : type === 'bulletin' ? 'Responsable' : 'Autor o fuente'}</span><input name="author" maxLength={160} defaultValue={type === 'bulletin' ? 'Comunicaciones' : undefined} key={type} /></label>
          {type === 'work_paper' && <label><span>Taller</span><input name="lodge" maxLength={160} /></label>}
          {type === 'magazine' && <label><span>N° de edición</span><input name="edition" maxLength={60} placeholder="N° 4" /></label>}
          {type === 'book' && <label><span>Edición / editorial / año</span><input name="edition" maxLength={120} /></label>}
          {type === 'official_document' && <label><span>Tipo de documento (reglamento, manual, ritual…)</span><input name="officialType" maxLength={80} /></label>}
          {(type === 'work_paper' || type === 'book' || type === 'magazine') && <label><span>{type === 'magazine' ? 'Tema principal' : 'Tema'}</span><input name="topic" maxLength={120} /></label>}
          {type === 'bulletin'
            ? <label key="month"><span>Mes y año que informa</span><input name="month" type="month" required defaultValue={currentMonth()} /></label>
            : <label key="date"><span>Fecha</span><input name="date" type="date" defaultValue={today()} /></label>}
          {editorialCategories.includes(type)
            ? <p className="library-admin-empty">La revista y el boletín son visibles para todos los grados (General).</p>
            : <label><span>Grado mínimo de acceso</span><select name="degree" defaultValue="">{degreeOptions.map(o => <option key={o.v} value={o.v}>{o.t}</option>)}</select></label>}
          <label className="wide"><span>{type === 'magazine' ? 'En este número' : type === 'bulletin' ? 'Resumen de actividades del mes' : 'Descripción breve'}</span><textarea name="description" rows={3} maxLength={800} required={type === 'bulletin'} /></label>
          <div className="library-admin-actions"><button type="submit" value="publish" className="action-trigger primary">Publicar</button><button type="submit" value="draft" className="action-trigger secondary">Guardar borrador</button></div>
        </form>
      </ActionDrawer>}
      <ActionDrawer label={isCommunications ? `Mis borradores${drafts.length ? ` (${drafts.length})` : ''}` : uploadEnabled ? `Por revisar y borradores${pending.length + drafts.length ? ` (${pending.length + drafts.length})` : ''}` : `Planchas por revisar${pending.length ? ` (${pending.length})` : ''}`} wide title={isCommunications ? 'Mis borradores' : uploadEnabled ? 'Por revisar y borradores' : 'Planchas por revisar'} description={isCommunications ? 'Revistas y boletines guardados como borrador. Nada es visible para los hermanos hasta publicarlo.' : 'Planchas enviadas por los Talleres. Nada es visible para los hermanos hasta que usted la apruebe.'}>
        <div className="library-admin-list">
          {!isCommunications && <>
          <h3>Planchas enviadas por Talleres</h3>
          {pending.length === 0 && <p className="library-admin-empty">No hay planchas esperando revisión.</p>}
          {pending.map(r => <article key={r.id} className="library-admin-card">
            <div><strong>{r.title}</strong><span>{r.authorName ?? 'Autor sin registrar'}{r.lodge ? ` · ${r.lodge}` : ''} · enviada el {r.submittedAtUtc.slice(0, 10)}{r.degree ? ` · ${r.degree}° grado` : ''}{r.replacesPublished ? ' · nueva versión de una plancha ya publicada' : ''}</span><p>{r.description}</p></div>
            {reject?.id === r.id ? <div className="library-admin-reject"><label><span>Motivo de la devolución (el Taller lo verá)</span><textarea rows={2} required minLength={10} maxLength={500} value={reject.reason} onChange={e => setReject({ id: r.id, reason: e.target.value })} /></label>
              <div><button type="button" className="action-trigger secondary" onClick={() => setReject(null)}>Volver</button><button type="button" className="action-trigger secondary" disabled={reject.reason.trim().length < 10} onClick={() => { const reason = reject.reason; setReject(null); void reviewActions?.returnWith(r, reason) }}>Devolver</button></div></div>
              : <div className="library-admin-row-actions"><button type="button" className="action-trigger secondary" onClick={() => void reviewActions?.download(r)}>Leer la plancha</button><ConfirmAction label="Aprobar y publicar" message="La plancha quedará visible según el grado de su autor y se registrará quién la aprobó." onConfirm={() => void reviewActions?.approve(r)} />
                <button type="button" className="action-trigger secondary" onClick={() => setReject({ id: r.id, reason: '' })}>Devolver con motivo</button></div>}
          </article>)}
          </>}
          {uploadEnabled && <>
          <h3>Mis borradores</h3>
          {drafts.length === 0 && <p className="library-admin-empty">No tiene borradores.</p>}
          {drafts.map(s => <article key={s.id} className="library-admin-card">
            <div><strong>{s.title}</strong><span>{label(s.documentType)} · guardado el {s.sentAt}</span></div>
            <div className="library-admin-row-actions"><ConfirmAction label="Publicar" message="Quedará visible para los hermanos autorizados." onConfirm={() => { onPublish(s.item); drop(s.id); notify(`«${s.title}» publicado.`) }} /><ConfirmAction label="Descartar" message="Se eliminará el borrador (no es un documento publicado)." onConfirm={() => drop(s.id)} /></div>
          </article>)}
          </>}
        </div>
      </ActionDrawer>
      {!isCommunications && <ActionDrawer label="Administrar categorías" title="Administrar categorías" description="Cree, renombre, ordene o desactive las categorías de la Biblioteca Virtual.">
        <div className="library-admin-list">
          {managed.map((c, i) => <article key={c.value} className="library-admin-card">
            <div className="library-admin-cat">
              <label><span>Nombre{c.base ? ' · categoría base' : ''}</span><input key={c.label} defaultValue={c.label} maxLength={60} onBlur={e => { const v = e.target.value.trim(); if (v && v !== c.label) void categoryActions.patch(c.value, { label: v }) }} /></label>
              <small>{counts[c.value] ?? 0} publicaciones · {c.active ? 'activa' : 'desactivada'}</small>
            </div>
            <div className="library-admin-row-actions">
              <button type="button" className="action-trigger secondary" disabled={i === 0} aria-label={`Subir ${c.label}`} onClick={() => move(c.value, -1)}>↑</button>
              <button type="button" className="action-trigger secondary" aria-label={`Bajar ${c.label}`} onClick={() => move(c.value, 1)}>↓</button>
              {c.active
                ? <button type="button" className="action-trigger secondary" disabled={c.base || (counts[c.value] ?? 0) > 0} title={(counts[c.value] ?? 0) > 0 ? 'Mueva o retire sus publicaciones antes de desactivarla' : undefined} onClick={() => categoryActions.patch(c.value, { active: false })}>Desactivar</button>
                : <button type="button" className="action-trigger secondary" onClick={() => categoryActions.patch(c.value, { active: true })}>Activar</button>}
            </div>
          </article>)}
          <form className="library-admin-new" onSubmit={addCategory}>
            <label><span>Nueva categoría</span><input value={newCategory} maxLength={60} onChange={e => setNewCategory(e.target.value)} placeholder="Ej.: Manuales de oficio" required /></label>
            <button type="submit" className="action-trigger primary">Crear categoría</button>
          </form>
          <HelpNote>Una categoría con publicaciones no se puede desactivar: primero se retiran o se mueven. Las categorías base no se eliminan.</HelpNote>
        </div>
      </ActionDrawer>}
    </ActionBar>
  </>
}
