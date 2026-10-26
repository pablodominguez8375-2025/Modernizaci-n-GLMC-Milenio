import { type FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import {
  assemblyReadinessStateLabel, assemblyReviewActionLabels, assemblyStatusLabel,
  type AssemblyClosureReadiness, type AssemblyListing, type AssemblyListingRow, type AssemblyReviewAction, type AssemblySummary,
} from './api/assemblyApi'
import { ActionDrawer } from './actionKit'

/** Padrón de la Gran Asamblea: listados PRELIMINARES de asambleístas y sufragantes habilitados.
 *  El cierre y la exportación oficial siguen bloqueados por el servidor hasta contar con las fuentes oficiales. */
export default function AssemblyPadronPanel({ api }: { api: PmgmApiClient }) {
  const [assemblies, setAssemblies] = useState<AssemblySummary[]>([])
  const [assemblyId, setAssemblyId] = useState('')
  const [listing, setListing] = useState<AssemblyListing | null>(null)
  const [readiness, setReadiness] = useState<AssemblyClosureReadiness | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const fail = (reason: unknown, fallback: string) => setError(reason instanceof Error ? reason.message : fallback)

  const loadListing = useCallback(async (id: string) => {
    if (!id) { setListing(null); setReadiness(null); return }
    setLoading(true); setError(null)
    try {
      const [rows, state] = await Promise.all([api.getAssemblyListing(id), api.getAssemblyClosureReadiness(id)])
      setListing(rows); setReadiness(state)
    }
    catch (reason) { fail(reason, 'No fue posible cargar los listados.') }
    finally { setLoading(false) }
  }, [api])

  useEffect(() => {
    let active = true
    api.getAssemblies()
      .then(result => { if (!active) return; setAssemblies(result.items); setAssemblyId(result.items[0]?.id ?? ''); if (!result.items.length) setLoading(false) })
      .catch(reason => { if (active) { fail(reason, 'No fue posible cargar las Asambleas.'); setLoading(false) } })
    return () => { active = false }
  }, [api])

  useEffect(() => { void loadListing(assemblyId) }, [assemblyId, loadListing])

  async function createAssembly(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setError(null); setMessage(null)
    try {
      const created = await api.createAssembly({ name: String(data.get('name') ?? ''), meetingDate: String(data.get('meetingDate') ?? ''), cutoffDate: String(data.get('cutoffDate') ?? '') })
      setAssemblies(current => [created, ...current]); setAssemblyId(created.id)
      setMessage('Asamblea preliminar creada. Aún no tiene asambleístas acreditados.')
    } catch (reason) { fail(reason, 'No fue posible crear la Asamblea.') }
  }

  async function debtBlock(event: FormEvent<HTMLFormElement>, organizationId: string, action: 'lift' | 'revoke') {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setError(null); setMessage(null)
    try {
      await api.decideAssemblyDebtBlock(assemblyId, organizationId, { action, reason: String(data.get('reason') ?? ''), evidenceReference: String(data.get('evidence') ?? '') })
      setMessage(action === 'lift' ? 'Bloqueo levantado por Régimen Interior, con motivo y referencia registrados en la auditoría.' : 'Bloqueo restablecido; el Taller vuelve a quedar sin sufragio.')
      await loadListing(assemblyId)
    } catch (reason) { fail(reason, 'No fue posible registrar la decisión.') }
  }

  async function memberDecision(event: FormEvent<HTMLFormElement>, row: AssemblyListingRow) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setError(null); setMessage(null)
    try {
      await api.decideAssemblyMember(assemblyId, row.personId, { action: String(data.get('action')) as AssemblyReviewAction, reason: String(data.get('reason') ?? ''), evidenceReference: String(data.get('evidence') ?? '') })
      setMessage(`Gestión registrada para ${row.name}. Queda en la bitácora de Régimen Interior.`)
      await loadListing(assemblyId)
    } catch (reason) { fail(reason, 'No fue posible registrar la gestión.') }
  }

  const selected = assemblies.find(a => a.id === assemblyId)
  const pendingCount = useMemo(() => listing?.asambleistas.filter(r => r.status === 'pendiente').length ?? 0, [listing])

  return <>
    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}

    <section className="panel assembly-toolbar">
      <label className="field"><span>Asamblea</span>
        <select aria-label="Asamblea" value={assemblyId} onChange={e => setAssemblyId(e.target.value)} disabled={!assemblies.length}>
          {assemblies.length === 0 && <option value="">Aún no hay Asambleas</option>}
          {assemblies.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
        </select>
      </label>
      <ActionDrawer label="Nueva Asamblea" title="Nueva Asamblea" description="Crea el corte de la Asamblea. No acredita asambleístas ni concede sufragio por sí sola.">
        <form className="assembly-form" onSubmit={createAssembly}>
          <label className="field"><span>Nombre</span><input name="name" required maxLength={240} placeholder="Gran Asamblea Ordinaria 2026" /></label>
          <label className="field"><span>Fecha de la Asamblea</span><input name="meetingDate" type="date" required /></label>
          <label className="field"><span>Fecha de corte del padrón</span><input name="cutoffDate" type="date" required /></label>
          <button type="submit" className="action-trigger primary">Crear Asamblea</button>
        </form>
      </ActionDrawer>
      {selected && <p className="muted assembly-dates">Asamblea: {formatDate(selected.meetingDate)} · Corte del padrón: {formatDate(selected.cutoffDate)}</p>}
    </section>

    <p className="assembly-note" role="note"><strong>Listados preliminares.</strong> El cierre del padrón y su exportación oficial siguen bloqueados mientras falten las fuentes oficiales (Venerables Maestros, Gran Tesorería, Régimen Interior y Tribunal de Honor).</p>

    {readiness && <section className="panel report-wide assembly-readiness" aria-label="Qué falta para cerrar el padrón">
      <div className="panel-heading"><div><p className="eyebrow">Cierre del Padrón</p><h2>Qué falta para cerrar el padrón</h2>
        <p>Cada paso muestra si su fuente oficial ya está lista. El padrón solo se cierra cuando todos estén completos.</p></div>
        <span className="count-badge">{readiness.items.filter(i => i.state === 'ok').length} de {readiness.items.length} listos</span></div>
      <ol className="assembly-readiness-list">
        {readiness.items.map(item => <li key={item.code} className={`assembly-readiness-item ${item.state}`}>
          <span className={`status-pill ${item.state === 'ok' ? 'complete' : 'attention'}`}>{assemblyReadinessStateLabel[item.state]}</span>
          <div><strong>{item.title}</strong><p>{item.detail}</p></div>
        </li>)}
      </ol>
    </section>}

    {loading && !listing ? <div className="panel"><p>Cargando listados…</p></div> : null}
    {listing && listing.asambleistas.length === 0 && !loading && <div className="panel"><p className="muted">Esta Asamblea aún no tiene asambleístas acreditados. Aparecerán cuando los Talleres y Gran Logia entreguen sus fuentes oficiales.</p></div>}

    {listing && listing.talleresConBloqueo.length > 0 && <section className="panel report-wide" aria-label="Talleres con saldo pendiente">
      <div className="panel-heading"><div><p className="eyebrow">Gran Tesorería · regla del Padrón</p><h2>Talleres con saldo pendiente</h2>
        <p>Mientras un Taller tenga cualquier saldo pendiente con Gran Tesorería, sus asambleístas no pueden sufragar. Solo Régimen Interior puede levantar el bloqueo.</p></div>
        <span className="count-badge">{listing.talleresConBloqueo.length} Taller(es)</span></div>
      <div className="table-wrap"><table><thead><tr><th>Taller</th><th>Asambleístas afectados</th><th>Estado</th><th>Acción</th></tr></thead><tbody>
        {listing.talleresConBloqueo.map(t => <tr key={t.organizationId}>
          <td><strong>{t.workshop}</strong></td><td>{t.affected} de {t.members}</td>
          <td><span className={t.blockLifted ? 'status-pill complete' : 'status-pill attention'}>{t.blockLifted ? 'Bloqueo levantado por Régimen Interior' : 'Sufragio bloqueado'}</span></td>
          <td>{t.blockLifted
            ? <ActionDrawer label="Restablecer bloqueo" tone="secondary" title={`Restablecer bloqueo · ${t.workshop}`} description="Vuelve a bloquear el sufragio del Taller. Queda en la auditoría.">
                <DebtForm submitLabel="Restablecer bloqueo" onSubmit={e => debtBlock(e, t.organizationId, 'revoke')} />
              </ActionDrawer>
            : <ActionDrawer label="Levantar bloqueo" title={`Levantar bloqueo · ${t.workshop}`} description="Decisión de Régimen Interior: permite sufragar a los asambleístas del Taller pese al saldo pendiente. Queda en la auditoría.">
                <DebtForm submitLabel="Levantar bloqueo" onSubmit={e => debtBlock(e, t.organizationId, 'lift')} />
              </ActionDrawer>}</td>
        </tr>)}
      </tbody></table></div>
    </section>}

    {listing && listing.asambleistas.length > 0 && <section className="panel report-wide" aria-label="Asambleístas">
      <div className="panel-heading"><div><p className="eyebrow">Gran Asamblea · listado 1</p><h2>Asambleístas</h2>
        <p>Orden alfabético por apellidos. El RUT se muestra parcialmente protegido.</p></div>
        <span className="count-badge">{listing.asambleistas.length} asambleístas{pendingCount ? ` · ${pendingCount} pendientes` : ''}</span></div>
      <div className="table-wrap"><table className="assembly-table"><thead><tr><th>N°</th><th>Apellidos y nombres</th><th>RUT</th><th>Taller</th><th>Estado</th><th>Firma</th><th>Gestión</th></tr></thead><tbody>
        {listing.asambleistas.map(r => <tr key={r.personId}>
          <td>{r.number}</td><td><strong>{r.name}</strong><small>{r.category}</small></td><td>{r.rut}</td><td>{r.workshop}</td>
          <td><span className={r.mayVote ? 'status-pill complete' : 'status-pill attention'}>{assemblyStatusLabel(r)}</span></td>
          <td><span className="assembly-signature" aria-label="Espacio para la firma" /></td>
          <td>{r.mayVote ? <span className="muted">—</span> : <ActionDrawer label="Registrar gestión" tone="secondary" title={`Gestión · ${r.name}`} description="Registra la gestión de Régimen Interior sobre este asambleísta. No confirma sufragio.">
            <form className="assembly-form" onSubmit={e => memberDecision(e, r)}>
              <label className="field"><span>Gestión</span><select name="action" defaultValue="request_correction">{(Object.keys(assemblyReviewActionLabels) as AssemblyReviewAction[]).map(k => <option key={k} value={k}>{assemblyReviewActionLabels[k]}</option>)}</select></label>
              <label className="field"><span>Motivo</span><textarea name="reason" required maxLength={2000} rows={3} /></label>
              <label className="field"><span>Referencia del respaldo</span><input name="evidence" required maxLength={500} placeholder="ACTA-RI-2026-001" /></label>
              <button type="submit" className="action-trigger primary">Registrar gestión</button>
            </form>
          </ActionDrawer>}</td>
        </tr>)}
      </tbody></table></div>
    </section>}

    {listing && listing.asambleistas.length > 0 && <section className="panel report-wide" aria-label="Sufragantes habilitados">
      <div className="panel-heading"><div><p className="eyebrow">Gran Asamblea · listado 2</p><h2>Sufragantes habilitados</h2>
        <p>Solo quienes hoy figuran habilitados para sufragar. No es el Padrón oficial hasta su cierre.</p></div>
        <span className="count-badge">{listing.sufragantes.length} habilitados</span></div>
      {listing.sufragantes.length === 0 ? <p className="muted">Aún no hay sufragantes habilitados.</p> :
        <div className="table-wrap"><table className="assembly-table"><thead><tr><th>N°</th><th>Apellidos y nombres</th><th>RUT</th><th>Taller</th><th>Firma</th></tr></thead><tbody>
          {listing.sufragantes.map(r => <tr key={r.personId}><td>{r.number}</td><td><strong>{r.name}</strong></td><td>{r.rut}</td><td>{r.workshop}</td><td><span className="assembly-signature" aria-label="Espacio para la firma" /></td></tr>)}
        </tbody></table></div>}
    </section>}

    <p className="report-footnote">El Padrón de la Gran Asamblea comprende exclusivamente a electores vigentes habilitados. Esta revisión nunca registra ni permite reconstruir cómo votó una persona.</p>
  </>
}

function DebtForm({ submitLabel, onSubmit }: { submitLabel: string; onSubmit: (event: FormEvent<HTMLFormElement>) => void }) {
  return <form className="assembly-form" onSubmit={onSubmit}>
    <label className="field"><span>Motivo (mínimo 10 caracteres)</span><textarea name="reason" required minLength={10} maxLength={2000} rows={3} /></label>
    <label className="field"><span>Referencia del acta o resolución</span><input name="evidence" required maxLength={500} placeholder="ACTA-RI-2026-001" /></label>
    <button type="submit" className="action-trigger primary">{submitLabel}</button>
  </form>
}

function formatDate(value: string) {
  const [y, m, d] = value.split('-')
  return y && m && d ? `${d}-${m}-${y}` : value
}
