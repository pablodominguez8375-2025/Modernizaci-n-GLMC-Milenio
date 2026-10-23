import { useEffect, useState, type FormEvent } from 'react'
import { ActionDrawer } from './actionKit'

/** 12.345.678-5 → 12.345.***-5 (conserva los primeros dígitos y el verificador). */
export function maskRut(rut: string): string {
  const clean = rut.replace(/[^0-9kK]/g, '').toUpperCase()
  if (clean.length < 2) return '***'
  const body = clean.slice(0, -1); const dv = clean.slice(-1)
  const visible = body.slice(0, Math.max(0, body.length - 3))
  const grouped = visible.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  return `${grouped}.***-${dv}`
}

export const rutRevealReasons = [
  { code: 'identity_verification', label: 'Verificación de identidad' },
  { code: 'authorized_procedure', label: 'Trámite autorizado por el cargo' },
  { code: 'support_with_consent', label: 'Soporte con autorización del usuario' },
] as const

export type RutRevealReason = typeof rutRevealReasons[number]['code']
export interface RutRevealResult { rut: string; expiresInSeconds: number }

/**
 * RUT enmascarado por defecto. «Ver completo» pide un motivo al servidor, que registra la auditoría ANTES
 * de responder: el RUT completo solo se muestra tras esa respuesta y se oculta solo.
 */
export default function MaskedRut({ masked, onReveal }: { masked: string; onReveal: (reason: RutRevealReason) => Promise<RutRevealResult> }) {
  const [full, setFull] = useState<string | null>(null)
  const [left, setLeft] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (left <= 0) { if (full) setFull(null); return }
    const t = window.setTimeout(() => setLeft(value => value - 1), 1000)
    return () => window.clearTimeout(t)
  }, [left, full])
  async function reveal(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '') as RutRevealReason
    if (busy || !reason) return
    setBusy(true); setError(null)
    try {
      const result = await onReveal(reason)
      setFull(result.rut); setLeft(result.expiresInSeconds)
    } catch (reasonError) {
      setError(reasonError instanceof Error ? reasonError.message : 'No fue posible mostrar el RUT.')
    } finally { setBusy(false) }
  }
  return <span className="masked-rut">
    <strong>{full ?? masked}</strong>
    {full
      ? <small>Se oculta en {left} s</small>
      : <ActionDrawer label="Ver completo" title="Ver RUT completo" description="Solo para verificar identidad. Queda registrado quién lo vio, cuándo y por qué.">
        <form className="library-admin-form" onSubmit={event => void reveal(event)}>
          <label><span>Motivo</span><select name="reason" required defaultValue=""><option value="" disabled>Seleccione un motivo</option>{rutRevealReasons.map(r => <option key={r.code} value={r.code}>{r.label}</option>)}</select></label>
          {error && <p role="alert" className="form-error">{error}</p>}
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy}>{busy ? 'Registrando…' : 'Mostrar RUT'}</button></div>
        </form>
      </ActionDrawer>}
  </span>
}
