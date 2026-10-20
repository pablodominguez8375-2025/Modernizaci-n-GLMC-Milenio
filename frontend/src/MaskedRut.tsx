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

const reasons = ['Verificación de identidad', 'Trámite autorizado por el cargo', 'Soporte con autorización del usuario']

/** RUT enmascarado por defecto; «Ver completo» exige un motivo (queda en auditoría) y se oculta solo. */
export default function MaskedRut({ rut, revealSeconds = 10, onReveal }: { rut: string; revealSeconds?: number; onReveal?: (reason: string) => void }) {
  const [left, setLeft] = useState(0)
  useEffect(() => {
    if (left <= 0) return
    const t = window.setTimeout(() => setLeft(value => value - 1), 1000)
    return () => window.clearTimeout(t)
  }, [left])
  function reveal(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '')
    onReveal?.(reason)
    setLeft(revealSeconds)
  }
  return <span className="masked-rut">
    <strong>{left > 0 ? rut : maskRut(rut)}</strong>
    {left > 0
      ? <small>Se oculta en {left} s</small>
      : <ActionDrawer label="Ver completo" title="Ver RUT completo" description="Solo para verificar identidad. Queda registrado quién lo vio, cuándo y por qué.">
        <form className="library-admin-form" onSubmit={reveal}>
          <label><span>Motivo</span><select name="reason" required defaultValue="">{<option value="" disabled>Seleccione un motivo</option>}{reasons.map(r => <option key={r}>{r}</option>)}</select></label>
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary">Mostrar RUT</button></div>
        </form>
      </ActionDrawer>}
  </span>
}
