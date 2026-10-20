import { type FormEvent, useState } from 'react'
import { ActionDrawer } from './actionKit'
import { type PmgmApiClient, type SystemSetting, type SystemSettingVersion } from './api/pmgmApi'

const todayChile = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago' }).format(new Date())
const messageOf = (e: unknown) => e instanceof Error ? e.message : 'No fue posible guardar la configuración.'
const stateLabel = (s: string) => s === 'default' ? 'Valor base' : s === 'scheduled' ? 'Programado' : 'Vigente'

/** Una categoría de parámetros = una tarjeta compacta + un único formulario de edición (no una tarjeta por parámetro). */
export default function SystemSettingsCategoryPanel({ api, category, items, onSaved, onMessage, onError }: {
  api: PmgmApiClient; category: string; items: SystemSetting[]
  onSaved: (saved: SystemSetting[]) => void; onMessage: (text: string | null) => void; onError: (text: string | null) => void
}) {
  const [values, setValues] = useState<Record<string, string>>({})
  const [effectiveFrom, setEffectiveFrom] = useState(todayChile())
  const [reference, setReference] = useState('')
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  const [historyCode, setHistoryCode] = useState<string | null>(null)
  const [history, setHistory] = useState<SystemSettingVersion[]>([])
  const valueOf = (item: SystemSetting) => values[item.code] ?? item.value
  const changed = items.filter(item => valueOf(item) !== item.value)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (changed.length === 0) { onError(null); onMessage('No hay cambios que guardar.'); return }
    setBusy(true); onError(null); onMessage(null)
    const saved: SystemSetting[] = []
    try {
      for (const item of changed) saved.push(await api.createSystemSettingVersion(item.code, { value: valueOf(item), effectiveFrom, sourceReference: reference.trim() }))
      onMessage(`Cambios guardados como nueva versión auditada (${saved.length} ${saved.length === 1 ? 'parámetro' : 'parámetros'}).`)
      setValues({}); setReference('')
    } catch (e) {
      onError(`${messageOf(e)}${saved.length ? ` Ya se guardaron ${saved.length} de ${changed.length}.` : ''}`)
    } finally { if (saved.length) onSaved(saved); setBusy(false) }
  }
  const showHistory = async (item: SystemSetting) => {
    if (historyCode === item.code) { setHistoryCode(null); return }
    onError(null)
    try { setHistory((await api.getSystemSettingVersions(item.code)).items); setHistoryCode(item.code) } catch (e) { onError(messageOf(e)) }
  }
  const restore = (item: SystemSetting, version: SystemSettingVersion) => {
    setValues(current => ({ ...current, [item.code]: version.value }))
    setEffectiveFrom(todayChile()); setReference(`Restauración de versión ${version.effectiveFrom}: ${version.sourceReference}`)
    setOpen(true); onMessage('Versión cargada en el formulario. Revise la fecha y guarde para crear una restauración auditada.')
  }

  return <article className="panel system-category-panel">
    <header>
      <div><p className="eyebrow">Parámetros versionados</p><h2>{category}</h2></div>
      <ActionDrawer label="Editar parámetros" open={open} onOpenChange={setOpen} title={`Editar: ${category}`}
        description="Cambie lo que necesite y guarde una sola vez. Cada parámetro modificado queda como nueva versión auditada."
        confirmMessage="Se creará una nueva versión de cada parámetro modificado. Los expedientes iniciados con la regla anterior no cambian.">
        <form className="system-setting-form" onSubmit={event => void submit(event)}>
          {items.map(item => <label key={item.code}><span>{item.label}{valueOf(item) !== item.value ? ' · modificado' : ''}</span>
            {item.valueType === 'text'
              ? <textarea rows={3} value={valueOf(item)} onChange={e => setValues(c => ({ ...c, [item.code]: e.target.value }))} />
              : <input type={item.valueType === 'integer' ? 'number' : 'text'} value={valueOf(item)} onChange={e => setValues(c => ({ ...c, [item.code]: e.target.value }))} />}
            {item.valueType === 'list' && <small>Separe los valores con el carácter |</small>}</label>)}
          <div className="system-setting-row">
            <label><span>Vigente desde</span><input type="date" required value={effectiveFrom} onChange={e => setEffectiveFrom(e.target.value)} /></label>
            <label><span>Fundamento / referencia</span><input required value={reference} onChange={e => setReference(e.target.value)} placeholder="Ej.: acuerdo o decreto que lo respalda" /></label>
          </div>
          <button type="submit" disabled={busy || changed.length === 0}>{busy ? 'Guardando…' : changed.length ? `Guardar ${changed.length} ${changed.length === 1 ? 'cambio' : 'cambios'}` : 'Sin cambios'}</button>
        </form>
      </ActionDrawer>
    </header>
    <dl className="system-category-list">
      {items.map(item => <div key={item.code} className="system-category-row">
        <dt>{item.label}</dt>
        <dd><strong>{item.value || '—'}</strong><small>Desde {item.effectiveFrom}</small></dd>
        <div className="system-category-meta"><span className={`system-setting-state ${item.status}`}>{stateLabel(item.status)}</span>
          <button type="button" className="secondary" aria-expanded={historyCode === item.code} onClick={() => void showHistory(item)}>Historial</button></div>
        {historyCode === item.code && <div className="system-history"><h3>Historial y cambios programados</h3>
          {history.length === 0 ? <p>Sin versiones guardadas.</p> : history.map(version => <div className="system-history-row" key={version.id}>
            <div><strong>{version.effectiveFrom}</strong><span>{version.status === 'scheduled' ? 'Programado' : version.status === 'retired' ? 'Retirado' : 'Vigente'} · {version.sourceReference}</span></div>
            <button className="secondary" type="button" onClick={() => restore(item, version)}>Restaurar como nueva</button></div>)}</div>}
      </div>)}
    </dl>
  </article>
}
