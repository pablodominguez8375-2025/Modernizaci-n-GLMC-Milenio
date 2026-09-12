import { useMemo, useState } from 'react'
import './initiationCircuit.css'

const stages = [
  { title: 'Ingreso del insinuado', owner: 'Secretaría del Taller', evidence: 'Ficha 2026, fotografía, fecha de ingreso y secretario responsable' },
  { title: 'Presentación y deliberación inicial', owner: 'Taller · 1.er grado', evidence: 'Acta de presentación, espera mínima de 7 días y acuerdo unánime' },
  { title: 'Publicación institucional', owner: 'Gran Secretaría', evidence: 'Publicación en intranet por 20 días corridos' },
  { title: 'Entrevistas y antecedentes', owner: 'Maestros entrevistadores', evidence: 'Tres informes, cuestionario confidencial y autobiografía' },
  { title: 'Revisión en 3.er grado', owner: 'Maestros del Taller', evidence: 'Votación abierta y extracto de acta' },
  { title: 'Balotaje definitivo', owner: 'Taller · 1.er grado', evidence: 'Balotaje, resultado y extracto de acta' },
  { title: 'Solicitud de Iniciación', owner: 'Venerable Maestro y Secretaría', evidence: 'Solicitud vinculada al mismo expediente, sin redigitación' },
  { title: 'Validaciones institucionales', owner: 'Régimen Interior · Tesorería · Hospitalaria', evidence: 'Controles separados, trazables y vigentes' },
  { title: 'Autorización institucional', owner: 'Gran Maestría', evidence: 'Visto bueno sobre el expediente completo' },
  { title: 'Plancha y programación', owner: 'Gran Secretaría', evidence: 'Plancha de autorización, fecha, Taller y reserva si corresponde' },
  { title: 'Ceremonia y activación', owner: 'Taller', evidence: 'Acta de Iniciación; alta como miembro activo y Aprendiz' },
] as const

export default function InitiationCircuitPage() {
  const [completed, setCompleted] = useState(0)
  const [selected, setSelected] = useState(0)
  const [history, setHistory] = useState<string[]>([])
  const current = stages[selected]
  const finished = completed === stages.length
  const progress = Math.round((completed / stages.length) * 100)
  const status = useMemo(() => finished ? 'Hermano activo · Aprendiz' : completed >= 10 ? 'Ceremonia autorizada' : completed >= 6 ? 'Candidato aprobado' : 'Insinuado en tramitación', [completed, finished])

  const advance = () => {
    if (finished) return
    const stage = stages[completed]
    setHistory(items => [`${stage.title} — evidencia registrada`, ...items])
    setCompleted(value => value + 1)
    setSelected(Math.min(completed + 1, stages.length - 1))
  }

  const restart = () => { setCompleted(0); setSelected(0); setHistory([]) }

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
        <p className="eyebrow">Etapa {selected + 1}</p><h2>{current.title}</h2>
        <dl><div><dt>Responsable</dt><dd>{current.owner}</dd></div><div><dt>Evidencia exigida</dt><dd>{current.evidence}</dd></div><div><dt>Estado</dt><dd>{selected < completed ? 'Completada' : selected === completed ? 'Lista para operar' : 'Bloqueada por etapa anterior'}</dd></div></dl>
        {selected === 0 && <div className="initiation-fields"><label>Insinuado<input value="Persona Demostrativa Centenario" readOnly /></label><label>Taller<input value="Taller Demostrativo Nº 23" readOnly /></label><label>Fecha de ingreso<input value="12-09-2026" readOnly /></label><label>Secretario responsable<input value="Secretario Demostrativo" readOnly /></label></div>}
        {selected === 9 && <div className="initiation-document"><span>PLANCHA</span><strong>{completed > 9 ? 'AUT-CER-DEMO-2026-001' : 'Se genera únicamente tras el visto bueno'}</strong><small>Permanece vinculada al expediente y a las validaciones congeladas.</small></div>}
        {selected === 10 && <div className="initiation-member"><strong>{finished ? 'Aprendiz activado' : 'Activación todavía bloqueada'}</strong><span>{finished ? 'Persona Demostrativa Centenario · Miembro activo · 1.er grado' : 'La autorización no convierte por sí sola al candidato en hermano.'}</span></div>}
        <div className="initiation-actions"><button type="button" className="regularity-primary" disabled={finished || selected !== completed} onClick={advance}>{completed === 9 ? 'Emitir Plancha y programar' : completed === 10 ? 'Registrar ceremonia y activar Aprendiz' : 'Registrar etapa y continuar'}</button><button type="button" className="regularity-secondary" onClick={restart}>Reiniciar caso de prueba</button></div>
      </section>
    </div>

    <section className="initiation-audit"><div><p className="eyebrow">Trazabilidad</p><h2>Bitácora del expediente</h2></div>{history.length === 0 ? <p>Aún no hay etapas registradas en esta ejecución de prueba.</p> : <ul>{history.map((item, index) => <li key={`${item}-${index}`}><span>✓</span>{item}</li>)}</ul>}</section>
    <p className="initiation-rule"><strong>Regla de integridad:</strong> candidato aprobado ≠ ceremonia autorizada ≠ hermano iniciado. La membresía y el grado Aprendiz sólo nacen al registrar la ceremonia efectivamente realizada.</p>
  </div>
}
