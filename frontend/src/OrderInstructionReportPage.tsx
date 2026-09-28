import { useEffect, useMemo, useState } from 'react'
import type { LodgeApiClient, LodgeGrade, OrderInstructionReport } from './api/lodgeApi'
import './lodgeManagement.css'

type Grade = Exclude<LodgeGrade, 'all'>
const labels: Record<Grade, string> = { apprentice: 'Aprendices', fellowcraft: 'Compañeros', master: 'Maestros' }

export default function OrderInstructionReportPage({ lodgeApi, allowedGrades, canReadAll }: {
  lodgeApi: LodgeApiClient
  allowedGrades: Grade[]
  canReadAll: boolean
}) {
  const grades = useMemo(() => allowedGrades, [allowedGrades])
  const [grade, setGrade] = useState<Grade | 'all'>(canReadAll ? 'all' : grades[0] ?? 'apprentice')
  const [organizationId, setOrganizationId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [report, setReport] = useState<OrderInstructionReport | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    if (grade !== 'all' && !grades.includes(grade)) setGrade(grades[0] ?? 'apprentice')
  }, [grade, grades])
  useEffect(() => {
    let active = true
    setError('')
    lodgeApi.getOrderInstructionReport({ grade, organizationId: organizationId || undefined, from: from || undefined, to: to || undefined })
      .then(value => { if (active) setReport(value) })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible consultar el reporte.') })
    return () => { active = false }
  }, [lodgeApi, grade, organizationId, from, to])

  return <div className="lodge-product-page">
    <section className="lodge-product-heading"><div><p className="lodge-product-breadcrumb">Orden <span>›</span> Docencia</p><h1>Instrucciones por Taller</h1><p>Consulta de solo lectura de las sesiones realizadas y la asistencia registrada.</p></div><span className="lodge-live-chip">Solo consulta</span></section>
    {error && <div className="error-banner" role="alert">{error}</div>}
    <section className="lodge-instruction-workspace">
      <div className="lodge-instruction-heading"><div><p className="lodge-kicker">Reporte de Orden</p><h2>Actividad de docencia</h2><p>El reporte no incluye nombres ni datos personales de los hermanos.</p></div></div>
      <div className="lodge-instruction-form">
        <label><span>Grado</span><select value={grade} onChange={event => setGrade(event.target.value as Grade | 'all')} disabled={!canReadAll}><option value="all">Todos los grados</option>{grades.map(item => <option key={item} value={item}>{labels[item]}</option>)}</select></label>
        <label><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Todos los Talleres</option>{report?.workshops.map(item => <option key={item.organizationId} value={item.organizationId}>{item.organizationName}{item.organizationNumber ? ` Nº ${item.organizationNumber}` : ''}</option>)}</select></label>
        <label><span>Desde</span><input type="date" value={from} onChange={event => setFrom(event.target.value)} /></label>
        <label><span>Hasta</span><input type="date" value={to} onChange={event => setTo(event.target.value)} /></label>
      </div>
      <div className="lodge-instruction-heading"><h2>{report?.total ?? 0} instrucciones realizadas</h2></div>
      <div className="table-scroll"><table><thead><tr><th>Taller</th><th>Grado</th><th>Sesiones</th><th>Presentes</th><th>Ausentes</th></tr></thead><tbody>{report?.summary.filter(row => grade === 'all' || row.grade === grade).map(row => <tr key={`${row.organizationId}-${row.grade}`}><td>{row.organizationName}{row.organizationNumber ? ` Nº ${row.organizationNumber}` : ''}</td><td>{labels[row.grade]}</td><td>{row.sessionCount}</td><td>{row.present}</td><td>{row.absent}</td></tr>)}</tbody></table></div>
      <div className="lodge-instruction-history">{report?.items.map(item => <article className="lodge-instruction-history-row" key={item.instructionId}><div><strong>{item.topic}</strong><span>{item.organizationName}{item.organizationNumber ? ` Nº ${item.organizationNumber}` : ''} · {labels[item.grade]} · {formatDate(item.instructionDate)}</span></div><div><small>{item.present} presentes · {item.absent} ausentes</small></div></article>)}{report?.items.length === 0 && <p className="lodge-empty-copy">No hay instrucciones realizadas para los filtros seleccionados.</p>}</div>
    </section>
  </div>
}

function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
