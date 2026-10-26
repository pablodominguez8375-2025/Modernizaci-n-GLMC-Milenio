import { type FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import {
  assemblyAttestationsByStep, assemblyAttestationTitles, assemblyReadinessStateLabel, assemblyReviewActionLabels, assemblyStatusLabel,
  type AssemblyAttestationKind, type AssemblyAttestationStatus, type AssemblyClosureReadiness, type AssemblyListing, type AssemblyListingRow, type AssemblyReviewAction, type AssemblySummary,
} from './api/assemblyApi'
import { ActionDrawer } from './actionKit'

/** Padrón de la Gran Asamblea: listados PRELIMINARES de asambleístas y sufragantes habilitados.
 *  El cierre y la exportación oficial siguen bloqueados por el servidor hasta contar con las fuentes oficiales. */
export default function AssemblyPadronPanel({ api }: { api: PmgmApiClient }) {
  const [assemblies, setAssemblies] = useState<AssemblySummary[]>([])
  const [assemblyId, setAssemblyId] = useState('')
  const [listing, setListing] = useState<AssemblyListing | null>(null)
  const [readiness, setReadiness] = useState<AssemblyClosureReadiness | null>(null)
  const [attestations, setAttestations] = useState<AssemblyAttestationStatus[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const fail = (reason: unknown, fallback: string) => setError(reason instanceof Error ? reason.message : fallback)

  const loadListing = useCallback(async (id: string) => {
    if (!id) { setListing(null); setReadiness(null); setAttestations([]); return }
    setLoading(true); setError(null)
    try {
      const [rows, state, atts] = await Promise.all([api.getAssemblyListing(id), api.getAssemblyClosureReadiness(id), api.getAssemblyAttestations(id)])
      setListing(rows); setReadiness(state); setAttestations(atts.items)
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

  async function attest(event: FormEvent<HTMLFormElement>, kind: AssemblyAttestationKind, action: 'attest' | 'revoke') {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setError(null); setMessage(null)
    try {
      await api.recordAssemblyAttestation(assemblyId, { kind, action, actaReference: String(data.get('acta') ?? ''), statement: String(data.get('statement') ?? '') })
      setMessage(action === 'attest' ? 'Constancia registrada por Régimen Interior, con su acta, en la auditoría.' : 'Constancia retirada; el paso vuelve a quedar pendiente.')
      await loadListing(assemblyId)
    } catch (reason) { fail(reason, 'No fue posible registrar la constancia.') }
  }

  async function runStep(event: FormEvent<HTMLFormElement>, work: () => Promise<string>) {
    event.preventDefault()
    setError(null); setMessage(null)
    try { setMessage(await work()); await loadListing(assemblyId) }
    catch (reason) { fail(reason, 'No fue posible completar el paso.') }
  }
  const stepForm = (e: FormEvent<HTMLFormElement>) => new FormData(e.currentTarget)
  const accredit = (e: FormEvent<HTMLFormElement>) => runStep(e, async () => { await api.accreditAssembly(assemblyId); return 'Asambleístas acreditados desde las fichas, con las constancias de Régimen Interior. Aún no tienen habilitación: falta evaluarlos.' })
  const evaluate = (e: FormEvent<HTMLFormElement>) => runStep(e, async () => { const r = await api.evaluateAssembly(assemblyId); return `Evaluación registrada: ${r.evaluated} asambleístas (${Object.entries(r.counts).map(([k, v]) => `${v} ${k.replace('_', ' ')}`).join(', ')}).` })
  async function downloadOfficial(list: 'asambleistas' | 'sufragantes', format: 'pdf' | 'csv') {
    setError(null); setMessage(null)
    try {
      const { blob, fileName } = await api.downloadAssemblyOfficialRoster(assemblyId, list, format)
      const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = fileName; a.click(); URL.revokeObjectURL(url)
      setMessage('Descarga lista. La descarga del padrón oficial queda en la auditoría.')
    } catch (reason) { fail(reason, 'No fue posible descargar el padrón oficial.') }
  }
  const closeRoster = (e: FormEvent<HTMLFormElement>) => { const d = stepForm(e); return runStep(e, async () => {
    const r = await api.closeAssembly(assemblyId, String(d.get('acta') ?? ''))
    return `Padrón cerrado con ${r.voters} sufragantes habilitados. Resumen de verificación ${r.digest.slice(0, 12)}…` }) }

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
  const isClosed = readiness?.status === 'closed' || listing?.officiallyClosed === true
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
          <div><strong>{item.title}</strong><p>{item.detail}</p>
            {item.code === 'roster' && item.state !== 'ok' && readiness.status === 'draft' &&
              <ActionDrawer label="Acreditar asambleístas" title="Acreditar asambleístas" description="Toma de las fichas a los Venerables Maestros (actuales y anteriores) activos al corte, con las constancias de Régimen Interior ya registradas. No los habilita: después se evalúan.">
                <form className="assembly-form" onSubmit={accredit}><p>Requiere las constancias de períodos de Venerable Maestro y de calidad de miembro activo. Si algo no cuadra, no se acredita a nadie.</p><button type="submit" className="action-trigger primary">Acreditar asambleístas</button></form>
              </ActionDrawer>}
            {item.code === 'grand_treasury' && item.state !== 'ok' && readiness.status === 'draft' &&
              <p className="assembly-note" role="note">Los saldos y pagos solo los valida su responsable: el Gran Tesorero (cuotas) y el Gran Hospitalario (reposiciones por fallecimiento y cuota anual). Se necesita el de ambos; Régimen Interior no los ingresa ni los modifica.</p>}
            {item.code === 'evaluation' && item.state !== 'ok' && item.count !== undefined && readiness.status === 'draft' &&
              <ActionDrawer label="Evaluar asambleístas" title="Evaluar asambleístas" description="Aplica las reglas de habilitación con la evidencia registrada. Si falta algo, el asambleísta queda pendiente. No cierra el padrón.">
                <form className="assembly-form" onSubmit={evaluate}><p>Requiere las cuatro constancias de Régimen Interior. Queda en la auditoría y se puede repetir si cambia la evidencia.</p><button type="submit" className="action-trigger primary">Evaluar asambleístas</button></form>
              </ActionDrawer>}
            {item.code === 'closure' && readiness.mayClose &&
              <ActionDrawer label="Cerrar padrón" title="Cerrar el Padrón de la Gran Asamblea" description="El cierre es definitivo: el padrón queda congelado y auditado. El sistema vuelve a verificar toda la evidencia antes de cerrar." confirmMessage="¿Cerrar definitivamente el padrón? No podrá modificarse después.">
                <form className="assembly-form" onSubmit={closeRoster}>
                  <label className="field"><span>Referencia del acta de cierre</span><input name="acta" required maxLength={500} placeholder="ACTA-RI-CIERRE-2026" /></label>
                  <button type="submit" className="action-trigger primary">Cerrar padrón</button>
                </form>
              </ActionDrawer>}
            {item.code === 'closure' && readiness.closureDigest && <p className="muted">Resumen de verificación (SHA-256): <code>{readiness.closureDigest}</code></p>}
            {(assemblyAttestationsByStep[item.code] ?? []).map(kind => {
              const current = attestations.find(a => a.kind === kind)
              const title = assemblyAttestationTitles[kind]
              return <div key={kind} className="assembly-attestation">
                {current?.attested && <span className="muted">Constancia registrada · acta {current.actaReference}</span>}
                {isClosed ? null : current?.attested
                  ? <ActionDrawer label="Retirar constancia" tone="secondary" title={`Retirar constancia · ${title}`} description="Deja el paso otra vez pendiente. Queda en la auditoría.">
                      <AttestationForm submitLabel="Retirar constancia" onSubmit={e => attest(e, kind, 'revoke')} />
                    </ActionDrawer>
                  : <ActionDrawer label={`Registrar constancia · ${title}`} title={`Constancia · ${title}`} description="Declaración de Régimen Interior, con el acta que la respalda, de que esta fuente está completa y verificada al corte. No concede sufragio ni cierra el padrón.">
                      <AttestationForm submitLabel="Registrar constancia" onSubmit={e => attest(e, kind, 'attest')} />
                    </ActionDrawer>}
              </div>
            })}
          </div>
        </li>)}
      </ol>
    </section>}

    {loading && !listing ? <div className="panel"><p>Cargando listados…</p></div> : null}
    {listing && listing.asambleistas.length === 0 && !loading && <div className="panel"><p className="muted">Esta Asamblea aún no tiene asambleístas acreditados. Aparecerán cuando los Talleres y Gran Logia entreguen sus fuentes oficiales.</p></div>}

    {isClosed && listing && <section className="panel report-wide assembly-official" aria-label="Padrón oficial">
      <div className="panel-heading"><div><p className="eyebrow">Padrón oficial · cerrado</p><h2>Descargar el padrón oficial</h2>
        <p>Los dos listados oficiales de la Asamblea cerrada, en orden alfabético por apellidos, con el RUT parcialmente protegido y la columna de firma en blanco. Cada descarga queda en la auditoría.</p></div></div>
      <div className="assembly-official-grid">
        {([['sufragantes', 'Sufragantes habilitados', 'Quienes pueden votar'], ['asambleistas', 'Asambleístas', 'Todos los asambleístas del corte']] as const).map(([key, title, hint]) => <div key={key} className="assembly-official-item">
          <strong>{title}</strong><small>{hint}</small>
          <div className="assembly-official-actions"><button type="button" className="action-trigger primary" onClick={() => void downloadOfficial(key, 'pdf')}>Descargar PDF</button>
            <button type="button" className="action-trigger secondary" onClick={() => void downloadOfficial(key, 'csv')}>Descargar planilla (CSV)</button></div>
        </div>)}
      </div>
    </section>}

    {listing && listing.talleresConBloqueo.length > 0 && <section className="panel report-wide" aria-label="Talleres con saldo pendiente">
      <div className="panel-heading"><div><p className="eyebrow">Gran Tesorería · regla del Padrón</p><h2>Talleres con saldo pendiente</h2>
        <p>Mientras un Taller tenga cualquier saldo pendiente con Gran Tesorería, sus asambleístas no pueden sufragar. Solo Régimen Interior puede levantar el bloqueo.</p></div>
        <span className="count-badge">{listing.talleresConBloqueo.length} Taller(es)</span></div>
      <div className="table-wrap"><table><thead><tr><th>Taller</th><th>Asambleístas afectados</th><th>Estado</th><th>Acción</th></tr></thead><tbody>
        {listing.talleresConBloqueo.map(t => <tr key={t.organizationId}>
          <td><strong>{t.workshop}</strong></td><td>{t.affected} de {t.members}</td>
          <td><span className={t.blockLifted ? 'status-pill complete' : 'status-pill attention'}>{t.blockLifted ? 'Bloqueo levantado por Régimen Interior' : 'Sufragio bloqueado'}</span></td>
          <td>{isClosed ? <span className="muted">Padrón cerrado</span> : t.blockLifted
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
          <td>{r.mayVote || isClosed ? <span className="muted">—</span> : <ActionDrawer label="Registrar gestión" tone="secondary" title={`Gestión · ${r.name}`} description="Registra la gestión de Régimen Interior sobre este asambleísta. No confirma sufragio.">
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

function AttestationForm({ submitLabel, onSubmit }: { submitLabel: string; onSubmit: (event: FormEvent<HTMLFormElement>) => void }) {
  return <form className="assembly-form" onSubmit={onSubmit}>
    <label className="field"><span>Referencia del acta</span><input name="acta" required maxLength={500} placeholder="ACTA-RI-2026-014" /></label>
    <label className="field"><span>Declaración (mínimo 10 caracteres)</span><textarea name="statement" required minLength={10} maxLength={2000} rows={3} /></label>
    <button type="submit" className="action-trigger primary">{submitLabel}</button>
  </form>
}

function formatDate(value: string) {
  const [y, m, d] = value.split('-')
  return y && m && d ? `${d}-${m}-${y}` : value
}
