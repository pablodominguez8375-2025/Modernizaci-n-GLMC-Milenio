import { useEffect, useState } from 'react'
import type { DocumentApiClient, MemberWorkPaper } from './api/documentApi'
import './member-work-papers.css'

export default function MemberWorkPapersPanel({ api, organizationId, enabled }: { api: DocumentApiClient; organizationId: string; enabled: boolean }) {
  const [items, setItems] = useState<MemberWorkPaper[]>([])
  const [loading, setLoading] = useState(enabled)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [replacing, setReplacing] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const reload = async () => setItems(await api.getMyWorkPapers())
  useEffect(() => {
    let active = true
    setItems([])
    setError(null)
    if (!enabled) { setLoading(false); return }
    setLoading(true)
    api.getMyWorkPapers().then(result => { if (active) setItems(result) }).catch(reason => { if (active) setError(toMessage(reason)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, enabled])

  const submit = async () => {
    if (!file || !title.trim() || !description.trim()) { setError('Completa título, descripción breve y archivo PDF o DOCX.'); return }
    setBusy(true); setError(null); setMessage(null)
    try {
      if (replacing) await api.replaceWorkPaper(replacing, { title, shortDescription: description, file })
      else await api.submitWorkPaper({ organizationId, title, shortDescription: description, file })
      await reload(); setMessage('La nueva versión superó integridad y antivirus y ya es la versión vigente en Biblioteca Virtual.')
      setTitle(''); setDescription(''); setFile(null); setReplacing(null)
      const input = document.getElementById('member-work-paper-file') as HTMLInputElement | null
      if (input) input.value = ''
    } catch (reason) { setError(toMessage(reason)) } finally { setBusy(false) }
  }

  const startReplace = (item: MemberWorkPaper) => { setReplacing(item.id); setTitle(item.title); setDescription(item.shortDescription); setFile(null); setError(null); setMessage(null) }

  return <section className="member-card member-work-papers" aria-labelledby="member-work-papers-title">
    <div className="member-card-title-row"><div><p className="member-card-kicker">Biblioteca Virtual · Planchas de Trabajo</p><h2 id="member-work-papers-title">Mis planchas</h2><p>Consulta tus trabajos y reemplaza una versión propia sin borrar su historial.</p></div><span className="member-lock-badge">Sólo tus planchas</span></div>
    {error && <div className="error-banner" role="alert">{error}</div>}{message && <div className="member-live-notice" role="status">{message}</div>}
    {enabled && <div className="member-work-paper-form">
      <strong>{replacing ? 'Cargar nueva versión' : 'Subir una plancha de trabajo'}</strong>
      <label><span>Título</span><input maxLength={240} value={title} onChange={event => setTitle(event.target.value)} /></label>
      <label><span>Descripción breve de referencia</span><textarea maxLength={300} rows={3} value={description} onChange={event => setDescription(event.target.value)} placeholder="En pocas palabras, ¿de qué trata este trabajo?" /></label>
      <label><span>Archivo PDF o DOCX</span><input id="member-work-paper-file" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" onChange={event => setFile(event.target.files?.[0] ?? null)} /></label>
      <div className="member-edit-actions"><button type="button" disabled={busy} onClick={() => void submit()}>{busy ? 'Validando archivo…' : replacing ? 'Enviar nueva versión' : 'Subir plancha'}</button>{replacing && <button type="button" className="document-secondary" disabled={busy} onClick={() => { setReplacing(null); setTitle(''); setDescription(''); setFile(null) }}>Cancelar</button>}<small>Se publica en Biblioteca Virtual según tu grado efectivo después de verificar integridad y antivirus. Si el control falla, la versión anterior sigue vigente.</small></div>
    </div>}
    {!enabled ? <div className="empty-state"><strong>El autoservicio requiere una membresía activa en un Taller.</strong></div> : loading ? <div className="loading-rows"><span /><span /></div> : items.length === 0 ? <div className="empty-state"><strong>Aún no tienes planchas cargadas.</strong><p>También pueden ser cargadas por Secretaría del Taller a tu nombre.</p></div> : <div className="member-work-paper-list">{items.map(item => <article key={item.id} className="member-work-paper-item"><div><span className="document-chip">{item.status === 'published' ? 'En Biblioteca Virtual' : 'Pendiente de análisis'}</span><h3>{item.title}</h3><p>{item.shortDescription}</p><small>Grado mínimo de acceso: {item.minimumDegreeRequired}° · {item.versions.length} {item.versions.length === 1 ? 'versión' : 'versiones'}</small>{item.versions.map(version => <small key={version.id} className="member-work-paper-version">v{version.versionNumber} · {version.originalFileName} · {version.isCurrent ? 'vigente' : version.processingStatus}</small>)}</div><button type="button" className="document-secondary" disabled={busy} onClick={() => startReplace(item)}>Reemplazar mi plancha</button></article>)}</div>}
  </section>
}

function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
