import { useCallback, useEffect, useMemo, useState } from 'react'
import { ActionDrawer, HelpNote } from './actionKit'
import type { PmgmApiClient } from './api/pmgmApi'
import {
  WORKSHOP_ACTION_LABELS, availableWorkshopActions, workshopActionDescription, workshopConfirmationText, workshopLabel, workshopOriginLabel, workshopRequestValid,
  workshopRegistryFor, type WorkshopRegistry, type WorkshopRegistryAction, type WorkshopRegistryItem,
} from './api/workshopRegistry'
import './account-inventory.css'
import './workshop-registry.css'

const dateLabel = (value: string) => { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('es-CL') }
const errorText = (reason: unknown) => reason instanceof Error && reason.message ? reason.message : 'No se pudo completar la acción.'

function ActionForm({ api, item, action, policy, onDone }: { api: PmgmApiClient; item: WorkshopRegistryItem; action: WorkshopRegistryAction; policy: WorkshopRegistry; onDone: (message: string) => void }) {
  const [reason, setReason] = useState('')
  const [typed, setTyped] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const needsConfirmation = action !== 'reactivate'
  const confirmation = workshopConfirmationText(item)
  const ready = workshopRequestValid(action, item, { reason, confirmation: typed }, policy)
  const submit = async () => {
    setBusy(true); setError('')
    try {
      await workshopRegistryFor(api).run(item.id, action, { reason: reason.trim(), confirmation: needsConfirmation ? typed.trim() : undefined })
      onDone(action === 'delete' ? `Se eliminó el Taller ${workshopLabel(item)}.` : action === 'retire' ? `El Taller ${workshopLabel(item)} quedó de baja.` : `El Taller ${workshopLabel(item)} volvió a estar activo.`)
    } catch (reason) { setError(errorText(reason)) } finally { setBusy(false) }
  }
  return <form onSubmit={event => { event.preventDefault(); void submit() }}>
    <p>{workshopActionDescription(action, item)}</p>
    <p>Queda registrado quién lo hizo, cuándo y por qué.</p>
    {error && <p role="alert">{error}</p>}
    <label><span>Motivo ({policy.reasonMinLength} a {policy.reasonMaxLength} caracteres)</span>
      <input required minLength={policy.reasonMinLength} maxLength={policy.reasonMaxLength} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} placeholder={action === 'delete' ? 'Ej.: Creado por error en la Carga inicial' : 'Ej.: El Taller abatió columnas'} /></label>
    {needsConfirmation && <label><span>Para confirmar, escriba {item.number ? 'el número del Taller' : 'el código interno'} <strong>{confirmation}</strong></span>
      <input required autoComplete="off" inputMode={item.number ? 'numeric' : 'text'} spellCheck={false} value={typed} disabled={busy} onChange={event => setTyped(event.target.value)} /></label>}
    <button className={action === 'delete' ? 'account-lifecycle-danger' : undefined} disabled={busy || !ready}>{busy ? 'Procesando…' : WORKSHOP_ACTION_LABELS[action]}</button>
  </form>
}

function ActionButton({ api, item, action, policy, onDone }: { api: PmgmApiClient; item: WorkshopRegistryItem; action: WorkshopRegistryAction; policy: WorkshopRegistry; onDone: (message: string) => void }) {
  const [open, setOpen] = useState(false)
  const label = WORKSHOP_ACTION_LABELS[action]
  return <ActionDrawer label={label} title={`${label} · ${workshopLabel(item)}`} confirmMessage={action === 'delete' ? `Se eliminará para siempre el Taller ${workshopLabel(item)}. No se puede deshacer.` : undefined} tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
    {open && <ActionForm api={api} item={item} action={action} policy={policy} onDone={message => { setOpen(false); onDone(message) }} />}
  </ActionDrawer>
}

/** Sistema › Talleres: registro de Talleres para el Máster y los superusuarios (PO 09-11-2026, opciones A y A). */
export default function WorkshopRegistryPanel({ api }: { api: PmgmApiClient }) {
  const [registry, setRegistry] = useState<WorkshopRegistry | null>(null)
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [query, setQuery] = useState('')
  const [reload, setReload] = useState(0)
  const [downloading, setDownloading] = useState(false)
  useEffect(() => {
    let live = true
    setError('')
    void workshopRegistryFor(api).registry().then(result => { if (live) setRegistry(result) }).catch(reason => { if (live) { setRegistry(null); setError(errorText(reason)) } })
    return () => { live = false }
  }, [api, reload])
  const finished = useCallback((message: string) => { setDone(message); setReload(value => value + 1) }, [])
  const visible = useMemo(() => {
    const term = query.trim().toLowerCase()
    const items = registry?.items ?? []
    return term ? items.filter(item => [item.number ?? '', item.name, item.shortCode, item.city ?? ''].some(value => value.toLowerCase().includes(term))) : items
  }, [registry, query])
  const download = async () => {
    setDownloading(true); setError('')
    try {
      const { blob, fileName } = await workshopRegistryFor(api).downloadCsv()
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url)
    } catch (reason) { setError(errorText(reason)) } finally { setDownloading(false) }
  }

  if (!registry) return <section className="workshop-registry">{error ? <p role="alert" className="error-banner">{error}</p> : <p role="status">Consultando los Talleres registrados…</p>}</section>
  const active = registry.items.filter(item => item.status === 'active' && item.type !== 'grand_lodge').length
  const retired = registry.items.filter(item => item.status === 'retired').length
  return <section className="workshop-registry" aria-labelledby="talleres-registrados">
    <div className="workshop-registry-head">
      <div><h3 id="talleres-registrados">Talleres registrados</h3>
        <p className="workshop-registry-summary"><strong>{active}</strong> Talleres activos{retired > 0 && <> · <strong>{retired}</strong> de baja</>}</p></div>
      <button type="button" className="secondary listing-export" disabled={downloading} onClick={() => void download()}>{downloading ? 'Preparando…' : 'Descargar CSV'}</button>
    </div>
    <HelpNote>{registry.canManage
      ? 'Puede eliminar un Taller creado por error solo si no tiene datos asociados. Si ya tiene datos, se da de baja: queda oculto y sin operación, y su historia se conserva. Cada acción pide motivo y queda en la auditoría.'
      : 'Puede ver y descargar el registro. Eliminar o dar de baja un Taller lo hace solo el Máster.'}</HelpNote>
    {done && <p role="status" className="account-lifecycle-done">{done}</p>}
    {error && <p role="alert" className="error-banner">{error}</p>}
    <label className="workshop-registry-search"><span>Buscar por número, nombre, código o ciudad</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} /></label>
    {visible.length === 0 && <p>No hay Talleres que coincidan.</p>}
    <ul className="workshop-registry-list">
      {visible.map(item => {
        const actions = availableWorkshopActions(item, registry.canManage)
        return <li key={item.id} className={`system-user-row workshop-registry-row${item.status === 'retired' ? ' is-retired' : ''}`}>
          <strong>{workshopLabel(item)}</strong>
          <div className="workshop-registry-badges">
            {item.protected && <span className="account-protected-badge">Protegido</span>}
            {item.status === 'retired' && <span className="workshop-badge-retired">De baja{item.retiredAtUtc ? ` desde ${dateLabel(item.retiredAtUtc)}` : ''}</span>}
            {!item.protected && item.status === 'active' && <span className={item.hasData ? 'workshop-badge-data' : 'workshop-badge-empty'}>{item.hasData ? 'Con datos asociados' : 'Sin datos asociados'}</span>}
          </div>
          <small>Código interno: <code>{item.shortCode}</code></small>
          <small>{[item.city, item.orienteCode && `Oriente ${item.orienteCode}`].filter(Boolean).join(' · ') || 'Sin Oriente registrado'}</small>
          <small>Origen: {workshopOriginLabel(item.origin)} · creado el {dateLabel(item.createdAtUtc)}</small>
          {actions.length > 0 && <div className="action-bar">{actions.map(action => <ActionButton key={action} api={api} item={item} action={action} policy={registry} onDone={finished} />)}</div>}
        </li>
      })}
    </ul>
  </section>
}
