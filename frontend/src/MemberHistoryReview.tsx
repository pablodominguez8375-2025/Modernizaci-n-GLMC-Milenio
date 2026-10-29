import { useCallback, useEffect, useState } from 'react'
import { ActionDrawer, ConfirmAction } from './actionKit'
import { officeLabel } from './officeLabels'
import { historyMilestones, type PendingHistoryItem, type PendingHistoryProposal } from './api/memberHistory'
import type { InternalAffairsApiClient } from './api/internalAffairsApi'
import './memberHistory.css'

const fmt = (value?: string | null) => value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value.slice(0, 10)}T12:00:00Z`)) : '—'
const toMessage = (reason: unknown) => reason instanceof Error ? reason.message : 'No fue posible completar la operación.'

function lines(item: PendingHistoryItem): { title: string; before?: string; after: string } {
  if (item.kind === 'degree') {
    const label = historyMilestones.find(x => x.type === item.eventType)?.label ?? item.eventType ?? 'Hito'
    return { title: label, before: item.before?.eventDate ? fmt(item.before.eventDate) : 'Sin fecha registrada', after: fmt(item.eventDate) }
  }
  const after = `${officeLabel(item.officeType ?? '')} · ${item.period ?? ''}`
  if (item.action === 'replace') {
    const b = item.before
    return { title: 'Corrección de cargo', before: b ? `${officeLabel(b.officeType ?? '')} · ${b.period ?? ''}` : 'Cargo vigente', after }
  }
  return { title: 'Cargo agregado', after: `${after} (${fmt(item.startDate)} → ${item.endDate ? fmt(item.endDate) : 'vigente'})` }
}

/** Régimen Interior aprueba, observa o rechaza lo que propone la Secretaría del Taller sobre la historia de un hermano. */
export default function MemberHistoryReview({ internalAffairsApi, organizationId, onResolved }: { internalAffairsApi: InternalAffairsApiClient; organizationId?: string; onResolved?: () => void }) {
  const [items, setItems] = useState<PendingHistoryProposal[]>([])
  const [notes, setNotes] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    internalAffairsApi.getPendingMemberHistory(organizationId).then(r => setItems(r.items)).catch(r => setError(toMessage(r)))
  }, [internalAffairsApi, organizationId])
  useEffect(load, [load])

  async function resolve(item: PendingHistoryProposal, decision: 'approve' | 'observe' | 'reject') {
    setBusy(item.id); setError(null)
    try { await internalAffairsApi.reviewMemberHistory(item.id, decision, notes[item.id]?.trim() || null); load(); onResolved?.() }
    catch (r) { setError(toMessage(r)) } finally { setBusy(null) }
  }

  return <section className="panel internal-control-table-panel">
    <div className="panel-heading"><div><p className="eyebrow">Historia de los hermanos</p><h2>Cambios de cargos y fechas por aprobar</h2></div><span className="count-badge">{items.length} pendientes</span></div>
    <p className="form-note">La Secretaría del Taller propone completar o corregir cargos, iniciación, aumento de salario y exaltación. Nada cambia en la ficha hasta su aprobación.</p>
    {error && <p role="alert" className="member-history-error">{error}</p>}
    <div className="member-history-list">
      {items.map(item => <article key={item.id} className="member-history-card is-submitted">
        <div><strong>{item.memberName ?? 'Hermano'}{item.institutionalNumber ? ` · ${item.institutionalNumber}` : ''}</strong><small>{item.organizationName} · Respaldo: {item.evidenceReference}</small><small>Motivo: {item.reason}</small></div>
        <ul>{item.items.map((x, i) => { const l = lines(x); return <li key={i}><strong>{l.title}:</strong> {l.before && <><span className="member-history-before">{l.before}</span> → </>}<span>{l.after}</span></li> })}</ul>
        <div className="historical-review-actions">
          <ConfirmAction label="Aprobar y actualizar ficha" message={`Se aplicarán estos cambios a la ficha de ${item.memberName ?? 'el hermano'}.`} confirmLabel="Sí, aprobar" disabled={busy !== null} onConfirm={() => void resolve(item, 'approve')} />
          <ActionDrawer label="Observar o rechazar" tone="secondary" title={`Observar o rechazar — ${item.memberName ?? 'Hermano'}`} description="El motivo es obligatorio y se informa a Secretaría del Taller." keepOpen>
            <div className="historical-review-drawer">
              <label><span>Observación de Régimen Interior</span><textarea value={notes[item.id] ?? ''} onChange={e => setNotes(c => ({ ...c, [item.id]: e.target.value }))} placeholder="Obligatoria al observar o rechazar" /></label>
              <div className="historical-review-actions">
                <button type="button" disabled={busy !== null || !(notes[item.id] ?? '').trim()} onClick={() => void resolve(item, 'observe')}>Observar</button>
                <button type="button" disabled={busy !== null || !(notes[item.id] ?? '').trim()} onClick={() => void resolve(item, 'reject')}>Rechazar</button>
              </div>
            </div>
          </ActionDrawer>
        </div>
      </article>)}
      {items.length === 0 && <div className="empty-state"><strong>No hay cambios de historia pendientes.</strong></div>}
    </div>
  </section>
}
