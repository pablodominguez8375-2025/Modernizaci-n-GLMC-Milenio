import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { type LodgeAnonymousBallot, type LodgeBallotType, type LodgeApiClient } from './api/lodgeApi'
import './lodgeBallot.css'

export default function LodgeBallotPanel({ meetingId, attendeeCount, enabled, api, onError }: { meetingId: string; attendeeCount: number; enabled: boolean; api: LodgeApiClient; onError: (message: string) => void }) {
  const [items, setItems] = useState<LodgeAnonymousBallot[]>([])
  const [type, setType] = useState<LodgeBallotType>('white_black')
  const [procedureNumber, setProcedureNumber] = useState<1 | 2 | 3>(1)
  const [subject, setSubject] = useState('Admisión del insinuado')
  const [eligible, setEligible] = useState(attendeeCount)
  const [positive, setPositive] = useState(attendeeCount)
  const [negative, setNegative] = useState(0)
  const [observation, setObservation] = useState('')
  const [working, setWorking] = useState(false)
  const counted = positive + negative
  const difference = eligible - counted
  const labels = useMemo(() => type === 'white_black' ? ['Balotas blancas', 'Balotas negras'] : type === 'candidate' ? ['Votos para el candidato', 'Votos negativos'] : ['Votos positivos', 'Votos negativos'], [type])

  useEffect(() => { setEligible(attendeeCount); setPositive(attendeeCount) }, [attendeeCount, meetingId])
  useEffect(() => { api.getAnonymousBallots(meetingId).then(response => setItems(response.items)).catch(reason => onError(toMessage(reason))) }, [api, meetingId, onError])

  const submit = async (event: FormEvent) => {
    event.preventDefault(); setWorking(true)
    try {
      await api.recordAnonymousBallot(meetingId, { ballotType: type, procedureNumber: type === 'white_black' ? procedureNumber : null, subject, eligibleCount: eligible, positiveCount: positive, negativeCount: negative, recountObservation: observation || null })
      setItems((await api.getAnonymousBallots(meetingId)).items); setObservation('')
    } catch (reason) { onError(toMessage(reason)) } finally { setWorking(false) }
  }

  return <section className="lodge-section lodge-ballot-panel"><div><p className="eyebrow">Escrutinio anónimo</p><h3>Balotaje y votaciones</h3><small>La asistencia determina cantidades. Centenario no registra ni puede reconstruir cómo votó una persona.</small></div>
    <form className="lodge-ballot-form" onSubmit={submit}>
      <label><span>Modalidad</span><select value={type} disabled={!enabled} onChange={event => setType(event.target.value as LodgeBallotType)}><option value="white_black">Balotas blancas y negras</option><option value="positive_negative">Voto positivo o negativo</option><option value="candidate">Votación por candidato</option></select></label>
      {type === 'white_black' && <label><span>Trámite</span><select value={procedureNumber} disabled={!enabled} onChange={event => setProcedureNumber(Number(event.target.value) as 1 | 2 | 3)}><option value={1}>Primer trámite</option><option value={2}>Segundo trámite</option><option value={3}>Tercer trámite</option></select></label>}
      <label className="wide"><span>Asunto o candidato</span><input required maxLength={500} disabled={!enabled} value={subject} onChange={event => setSubject(event.target.value)} /></label>
      <div className="lodge-ballot-count"><small>Asistentes presentes</small><strong>{attendeeCount}</strong><span>Calculado desde asistencia</span></div>
      <label><span>Habilitados</span><input type="number" min="0" max={attendeeCount} disabled={!enabled} value={eligible} onChange={event => setEligible(Number(event.target.value))} /></label>
      <label><span>{labels[0]}</span><input type="number" min="0" disabled={!enabled} value={positive} onChange={event => setPositive(Number(event.target.value))} /></label>
      <label><span>{labels[1]}</span><input type="number" min="0" disabled={!enabled} value={negative} onChange={event => setNegative(Number(event.target.value))} /></label>
      <div className={difference === 0 ? 'lodge-ballot-check correct' : 'lodge-ballot-check observed'}><strong>{counted} contabilizados</strong><span>{difference === 0 ? 'Recuento cuadrado' : `Diferencia: ${difference}`}</span></div>
      {difference !== 0 && <label className="wide"><span>Explicación obligatoria para el acta</span><textarea required maxLength={2000} disabled={!enabled} value={observation} onChange={event => setObservation(event.target.value)} /></label>}
      <button className="regularity-primary wide" type="submit" disabled={!enabled || working || !subject.trim()}>{working ? 'Guardando…' : 'Cerrar escrutinio agregado'}</button>
    </form>
    <div className="lodge-ballot-history">{items.length === 0 ? <small>Sin votaciones registradas.</small> : items.map(item => <article key={item.id} className={item.status}><div><strong>{item.subject}</strong><small>{procedureLabel(item.procedureNumber)}{ballotTypeLabel(item.ballotType)} · versión {item.version}</small></div><dl><div><dt>Asistentes</dt><dd>{item.attendeeCount}</dd></div><div><dt>Habilitados</dt><dd>{item.eligibleCount}</dd></div><div><dt>{item.ballotType === 'white_black' ? 'Blancas' : 'Positivos'}</dt><dd>{item.positiveCount}</dd></div><div><dt>{item.ballotType === 'white_black' ? 'Negras' : 'Negativos'}</dt><dd>{item.negativeCount}</dd></div></dl><span>{item.status === 'closed' ? 'Vigente' : 'Reemplazada'}</span></article>)}</div>
  </section>
}

function ballotTypeLabel(value: LodgeBallotType) { return value === 'white_black' ? 'Balotaje B/N' : value === 'candidate' ? 'Por candidato' : 'Positivo/negativo' }
function procedureLabel(value: 1 | 2 | 3 | null) { return value === 1 ? 'Primer trámite · ' : value === 2 ? 'Segundo trámite · ' : value === 3 ? 'Tercer trámite · ' : '' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible registrar el escrutinio.' }
