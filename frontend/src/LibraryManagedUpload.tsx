import { useState, type FormEvent } from 'react'
import { ActionDrawer, ConfirmAction } from './actionKit'
import type { DocumentApiClient, ManagedPublication } from './api/documentApi'
import type { LibraryCategory } from './LibraryManagement'

/** Categorías que el Bibliotecario carga directamente: libros y las que cree. Las planchas llegan de los Talleres; revista y boletín son de Comunicaciones; los documentos oficiales, de Gestión Documental. */
const notManaged = ['work_paper', 'magazine', 'bulletin', 'official_document', 'video']
const degreeOptions = [{ v: '', t: 'General (todos los grados)' }, { v: '1', t: '1° grado' }, { v: '2', t: '2° grado' }, { v: '3', t: '3° grado' }]

type Props = { api: DocumentApiClient; categories: LibraryCategory[]; drafts: ManagedPublication[]; onChanged: () => void; onPublished?: (item: ManagedPublication) => void; notify: (message: string) => void }

export default function LibraryManagedUpload({ api, categories, drafts, onChanged, onPublished, notify }: Props) {
  const options = categories.filter(c => c.active && !c.future && !notManaged.includes(c.value))
  const [type, setType] = useState(options[0]?.value ?? 'book')
  const [fileName, setFileName] = useState('')
  const [busy, setBusy] = useState(false)
  const selected = options.some(c => c.value === type) ? type : options[0]?.value ?? 'book'
  const isBook = selected === 'book'
  const label = (code: string) => categories.find(c => c.value === code)?.label ?? code

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null
    const file = (form.elements.namedItem('file') as HTMLInputElement | null)?.files?.[0]
    const g = (k: string) => String(data.get(k) ?? '').trim() || undefined
    if (!file) { notify('Elija el archivo a subir.'); return }
    const publishNow = submitter?.value !== 'draft'
    setBusy(true)
    try {
      const degree = g('degree')
      await api.uploadManagedPublication({ categoryCode: selected, title: g('title') ?? '', authorName: g('author'), topic: g('topic'), edition: g('edition'), description: g('description'), minimumDegreeRequired: degree ? Number(degree) : null }, file, publishNow)
      notify(publishNow ? `«${g('title')}» publicado en ${label(selected)}. Quedó visible según el grado indicado y registrado en auditoría.` : 'Borrador guardado con su archivo ya verificado. Nada es visible hasta publicarlo desde «Mis borradores».')
      if (publishNow) onPublished?.({ id: crypto.randomUUID(), title: g('title') ?? '', categoryCode: selected, status: 'published', degree: degree ? Number(degree) : null, author: g('author') ?? null, topic: g('topic') ?? null, edition: g('edition') ?? null, description: g('description') ?? null, createdAtUtc: new Date().toISOString(), publishedAtUtc: new Date().toISOString() })
      form.reset(); setFileName(''); onChanged()
    } catch (reason) { notify(reason instanceof Error ? reason.message : 'No fue posible completar la carga.') } finally { setBusy(false) }
  }

  async function publish(item: ManagedPublication) {
    try { await api.publishManagedDraft(item.id); onPublished?.({ ...item, status: 'published' }); notify(`«${item.title}» publicado.`); onChanged() }
    catch (reason) { notify(reason instanceof Error ? reason.message : 'No fue posible publicar.') }
  }

  const pending = drafts.filter(d => d.status !== 'published')
  return <>
    <ActionDrawer label="Subir un libro o documento" wide title="Subir a la Biblioteca" description="Libros y las categorías que usted creó. Se verifica el archivo (integridad y antivirus) antes de publicarlo. Las planchas llegan desde los Talleres y la revista y el boletín los publica Comunicaciones.">
      <form className="library-admin-form" onSubmit={e => void submit(e)}>
        <label><span>Categoría</span><select value={selected} onChange={e => setType(e.target.value)}>{options.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}</select></label>
        <label><span>Archivo (PDF, Word u otro permitido)</span><input name="file" type="file" required accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx" onChange={e => setFileName(e.target.files?.[0]?.name ?? '')} />{fileName && <small>Seleccionado: {fileName}</small>}</label>
        <label><span>Título</span><input name="title" required maxLength={240} /></label>
        <label><span>Autor o fuente{isBook ? '' : ' (opcional)'}</span><input name="author" maxLength={320} required={isBook} /></label>
        <label><span>Tema{isBook ? '' : ' (opcional)'}</span><input name="topic" maxLength={240} required={isBook} /></label>
        <label><span>Edición / editorial / año{isBook ? '' : ' (opcional)'}</span><input name="edition" maxLength={120} required={isBook} /></label>
        <label><span>Grado mínimo de acceso</span><select name="degree" defaultValue="">{degreeOptions.map(o => <option key={o.v} value={o.v}>{o.t}</option>)}</select></label>
        <label className="wide"><span>{isBook ? 'Abstract' : 'Descripción breve'}{isBook ? '' : ' (opcional)'}</span><textarea name="description" rows={3} maxLength={4000} required={isBook} /></label>
        <div className="library-admin-actions"><button type="submit" value="publish" disabled={busy} className="action-trigger primary">{busy ? 'Verificando archivo…' : 'Publicar'}</button><button type="submit" value="draft" disabled={busy} className="action-trigger secondary">Guardar borrador</button></div>
      </form>
    </ActionDrawer>
    <ActionDrawer label={`Mis borradores${pending.length ? ` (${pending.length})` : ''}`} wide title="Mis borradores" description="Cargas guardadas sin publicar. Nada es visible para los hermanos hasta publicarlas.">
      <div className="library-admin-list">
        {pending.length === 0 && <p className="library-admin-empty">No tiene borradores.</p>}
        {pending.map(d => <article key={d.id} className="library-admin-card">
          <div><strong>{d.title}</strong><span>{label(d.categoryCode)} · guardado el {d.createdAtUtc.slice(0, 10)} · {d.degree ? `${d.degree}° grado` : 'General'}</span></div>
          <div className="library-admin-row-actions"><ConfirmAction label="Publicar" message="Quedará visible para los hermanos autorizados." onConfirm={() => void publish(d)} /></div>
        </article>)}
      </div>
    </ActionDrawer>
  </>
}
