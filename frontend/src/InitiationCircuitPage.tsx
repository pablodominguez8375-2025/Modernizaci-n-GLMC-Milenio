import { useMemo, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { DemoProfileKey } from './demoProfiles'
import './initiationCircuit.css'

const stages = [
  { title: 'Ingreso del insinuado', owner: 'Secretaría del Taller', profile: 'lodge', evidence: 'Ficha 2026, fotografía, fecha de ingreso y secretario responsable' },
  { title: 'Presentación y deliberación inicial', owner: 'Taller · 1.er grado', profile: 'lodge', evidence: 'Acta de presentación, espera mínima de 7 días y acuerdo unánime' },
  { title: 'Publicación institucional', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Publicación en intranet por 20 días corridos' },
  { title: 'Entrevistas y antecedentes', owner: 'Maestros entrevistadores', profile: 'lodge', evidence: 'Tres informes, cuestionario confidencial y autobiografía' },
  { title: 'Revisión en 3.er grado', owner: 'Maestros del Taller', profile: 'lodge', evidence: 'Votación abierta y extracto de acta' },
  { title: 'Balotaje definitivo', owner: 'Taller · 1.er grado', profile: 'lodge', evidence: 'Balotaje anónimo, balotas blancas/negras y extracto de acta' },
  { title: 'Solicitud de Iniciación', owner: 'Venerable Maestro y Secretaría', profile: 'lodge', evidence: 'Solicitud vinculada al mismo expediente, sin redigitación' },
  { title: 'Aprobación de Régimen Interior', owner: 'Régimen Interior', profile: 'regimen', evidence: 'Procedimiento, documentos, tenidas, asistencia y balotaje válidos' },
  { title: 'Aprobación de Gran Tesorería', owner: 'Gran Tesorero', profile: 'treasury', evidence: 'Regularidad del Taller, derecho de Iniciación y conciliación' },
  { title: 'Aprobación de Gran Hospitalaria', owner: 'Gran Hospitalaria', profile: 'hospitalaria', evidence: 'Reposiciones, Fondo de Defunción y aportes aplicables' },
  { title: 'Control administrativo', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Expediente íntegro, aprobaciones vigentes, fecha y lugar' },
  { title: 'Visto bueno institucional', owner: 'Gran Maestra', profile: 'grandMaster', evidence: 'Decisión final sobre el expediente completo y trazable' },
  { title: 'Plancha y programación', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Plancha de autorización, fecha, Taller y reserva si corresponde' },
  { title: 'Ceremonia y activación', owner: 'Taller', profile: 'lodge', evidence: 'Acta de Iniciación; alta como miembro activo y Aprendiz' },
] as const

export default function InitiationCircuitPage({ api, demoProfileKey }: { api: PmgmApiClient; demoProfileKey?: DemoProfileKey }) {
  const [completed, setCompleted] = useState(() => {
    if (!api.useMocks || typeof window === 'undefined') return 0
    const stored = Number(window.localStorage.getItem('centenario.demo.initiation.completed') ?? '0')
    return Number.isInteger(stored) ? Math.max(0, Math.min(stored, stages.length)) : 0
  })
  const [selected, setSelected] = useState(0)
  const [history, setHistory] = useState<string[]>([])
  const [decision, setDecision] = useState<'approved' | 'rejected' | 'observed' | null>(null)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const current = stages[selected]
  const canDecideCurrent = !demoProfileKey || demoProfileKey === 'grandLodge' || demoProfileKey === current.profile
  const finished = completed === stages.length
  const progress = Math.round((completed / stages.length) * 100)
  const status = useMemo(() => finished ? 'Hermano activo · Aprendiz' : completed >= 10 ? 'Ceremonia autorizada' : completed >= 6 ? 'Candidato aprobado' : 'Insinuado en tramitación', [completed, finished])

  const advance = async () => {
    if (finished) return
    setWorking(true); setError(null)
    try {
      if (!canDecideCurrent) throw new Error(`Esta etapa corresponde a ${current.owner}. Cambie al perfil aprobante indicado.`)
      if (completed === stages.length - 1) await api.registerInitiation('eeeeeeee-2222-2222-2222-222222222222', '2026-09-12', 'ACTA-INI-DEMO-2026-001')
    const stage = stages[completed]
    setDecision('approved')
    setHistory(items => [`${stage.title} — evidencia registrada`, ...items])
    const next = completed + 1
    setCompleted(next)
    if (api.useMocks && typeof window !== 'undefined') window.localStorage.setItem('centenario.demo.initiation.completed', String(next))
    setSelected(Math.min(completed + 1, stages.length - 1))
    setDecision(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible registrar la etapa.') } finally { setWorking(false) }
  }

  const reject = () => {
    if (selected !== completed || finished) return
    const stage = stages[selected]
    setDecision('rejected')
    setHistory(items => [`${stage.title} — rechazado por el aprobante; el expediente queda detenido`, ...items])
  }

  const observe = () => {
    if (selected !== completed || finished) return
    const stage = stages[selected]
    setDecision('observed')
    setHistory(items => [`${stage.title} — observado; se solicita subsanar antecedentes`, ...items])
  }

  const restart = () => { setCompleted(0); setSelected(0); setHistory([]); setDecision(null); if (api.useMocks && typeof window !== 'undefined') window.localStorage.removeItem('centenario.demo.initiation.completed') }

  return <div className="initiation-circuit">
    <header className="initiation-hero">
      <div><p className="eyebrow">Expediente único · datos ficticios</p><h1>Circuito completo de Iniciación</h1><p>Desde el ingreso por el Taller hasta la ceremonia efectiva y el alta como Aprendiz.</p></div>
      <div className="initiation-case"><small>EXPEDIENTE</small><strong>INI-DEMO-2026-001</strong><span>{status}</span></div>
    </header>

    <section className="initiation-progress" aria-label="Avance del expediente"><div><strong>{progress}% completado</strong><span>{completed}/{stages.length} etapas cerradas</span></div><div className="initiation-track"><span style={{ width: `${progress}%` }} /></div></section>

    <div className="initiation-layout">
      <ol className="initiation-steps">
        {stages.map((stage, index) => <li key={stage.title}><button type="button" className={index < completed ? 'done' : index === completed ? 'current' : ''} onClick={() => setSelected(index)}><span>{index < completed ? '✓' : index + 1}</span><div><strong>{stage.title}</strong><small>{stage.owner}</small></div></button></li>)}
      </ol>

      <section className="initiation-detail">
        {error && <div className="error-banner" role="alert">{error}</div>}
        <p className="eyebrow">Etapa {selected + 1}</p><h2>{current.title}</h2>
        <dl><div><dt>Responsable / aprobante</dt><dd>{current.owner}</dd></div><div><dt>Evidencia exigida</dt><dd>{current.evidence}</dd></div><div><dt>Estado</dt><dd>{selected < completed ? 'Completada' : selected === completed ? (decision === 'rejected' ? 'Rechazada · requiere corrección' : decision === 'observed' ? 'Observada · pendiente de subsanar' : 'Pendiente de decisión') : 'Bloqueada por etapa anterior'}</dd></div></dl>
        {selected === completed && !finished && <div className="initiation-approval-note"><strong>Esta etapa requiere aprobación</strong><span>El aprobante revisa el expediente completo, actividad, tenidas y documentos antes de resolver.</span></div>}
        {selected === completed && !finished && !canDecideCurrent && <div className="error-banner" role="status"><strong>Cambio de perfil requerido.</strong><span>Seleccione “{current.owner}” en Perfil QA para resolver esta tarea.</span></div>}
        {selected === 0 && <div className="initiation-fields"><label>Insinuado<input value="Persona Demostrativa Centenario" readOnly /></label><label>Taller<input value="Taller Demostrativo Nº 23" readOnly /></label><label>Fecha de ingreso<input value="12-09-2026" readOnly /></label><label>Secretario responsable<input value="Secretario Demostrativo" readOnly /></label></div>}
        {selected === 12 && <div className="initiation-document"><span>PLANCHA</span><strong>{completed > 12 ? 'AUT-CER-DEMO-2026-001' : 'Se genera únicamente tras el visto bueno'}</strong><small>Permanece vinculada al expediente y a las validaciones congeladas.</small></div>}
        {selected === 13 && <div className="initiation-member"><strong>{finished ? 'Aprendiz activado' : 'Activación todavía bloqueada'}</strong><span>{finished ? 'Persona Demostrativa Centenario · Miembro activo · 1.er grado' : 'La autorización no convierte por sí sola al candidato en hermano.'}</span></div>}
        <div className="initiation-actions"><button type="button" className="regularity-primary" disabled={working || finished || selected !== completed || decision === 'rejected' || !canDecideCurrent} onClick={advance}>{working ? 'Registrando…' : completed === 12 ? 'Aprobar y emitir Plancha' : completed === 13 ? 'Registrar ceremonia y activar Aprendiz' : 'Registrar etapa y continuar'}</button>{selected === completed && !finished && completed < 13 && <><button type="button" className="regularity-secondary" disabled={working || !canDecideCurrent} onClick={observe}>Observar</button><button type="button" className="regularity-secondary" disabled={working || !canDecideCurrent} onClick={reject}>Rechazar</button></>}<button type="button" className="regularity-secondary" disabled={working} onClick={restart}>Reiniciar caso de prueba</button></div>
      </section>
    </div>

    <section className="initiation-audit"><div><p className="eyebrow">Trazabilidad</p><h2>Bitácora del expediente</h2></div>{history.length === 0 ? <p>Aún no hay etapas registradas en esta ejecución de prueba.</p> : <ul>{history.map((item, index) => <li key={`${item}-${index}`}><span>✓</span>{item}</li>)}</ul>}</section>
    <p className="initiation-rule"><strong>Regla de integridad:</strong> candidato aprobado ≠ ceremonia autorizada ≠ hermano iniciado. La membresía y el grado Aprendiz sólo nacen al registrar la ceremonia efectivamente realizada.</p>
  </div>
}
