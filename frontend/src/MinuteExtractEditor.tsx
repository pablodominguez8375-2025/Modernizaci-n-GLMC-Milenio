import { useState } from 'react'
import './minuteExtractEditor.css'

export interface MinuteNarrativeFields {
  openingTime: string; previousMinuteDate: string; previousMinuteApproved: 'Sí' | 'No' | 'Pendiente'
  correspondence: string; decrees: string; proposalBag: string; workAuthor: string; workTitle: string
  contributions: string; generalGood: string; charityAmount: string; closingTime: string; chainCloser: string
}

const initialFields: MinuteNarrativeFields = {
  openingTime: '', previousMinuteDate: '', previousMinuteApproved: 'Pendiente', correspondence: '', decrees: '', proposalBag: '',
  workAuthor: '', workTitle: '', contributions: '', generalGood: '', charityAmount: '', closingTime: '', chainCloser: '',
}

export default function MinuteExtractEditor({ baseContent, onApply }: { baseContent: string; onApply: (content: string) => void }) {
  const [fields, setFields] = useState(initialFields)
  const update = <K extends keyof MinuteNarrativeFields>(key: K, value: MinuteNarrativeFields[K]) => setFields(current => ({ ...current, [key]: value }))
  return <details className="minute-editor" open>
    <summary>Completar antecedentes del Extracto 2026</summary>
    <div className="minute-editor-grid">
      <Field label="Hora de apertura"><input type="time" value={fields.openingTime} onChange={event => update('openingTime', event.target.value)} /></Field>
      <Field label="Fecha del acta anterior"><input type="date" value={fields.previousMinuteDate} onChange={event => update('previousMinuteDate', event.target.value)} /></Field>
      <Field label="Acta anterior aprobada"><select value={fields.previousMinuteApproved} onChange={event => update('previousMinuteApproved', event.target.value as MinuteNarrativeFields['previousMinuteApproved'])}><option>Pendiente</option><option>Sí</option><option>No</option></select></Field>
      <Field label="Correspondencia"><input value={fields.correspondence} onChange={event => update('correspondence', event.target.value)} /></Field>
      <Field label="Decretos · sólo números"><input value={fields.decrees} onChange={event => update('decrees', event.target.value)} /></Field>
      <Field label="Saco de proposiciones"><input value={fields.proposalBag} onChange={event => update('proposalBag', event.target.value)} /></Field>
      <Field label="Autor/a del trabajo"><input value={fields.workAuthor} onChange={event => update('workAuthor', event.target.value)} /></Field>
      <Field label="Título del trabajo"><input value={fields.workTitle} onChange={event => update('workTitle', event.target.value)} /></Field>
      <Field label="Aportes · sólo nombres"><input value={fields.contributions} onChange={event => update('contributions', event.target.value)} /></Field>
      <Field label="Bien General · sólo nombres"><input value={fields.generalGood} onChange={event => update('generalGood', event.target.value)} /></Field>
      <Field label="Tronco de beneficencia · CLP"><input inputMode="numeric" value={fields.charityAmount} onChange={event => update('charityAmount', event.target.value.replace(/[^0-9]/g, ''))} /></Field>
      <Field label="Hora de clausura"><input type="time" value={fields.closingTime} onChange={event => update('closingTime', event.target.value)} /></Field>
      <Field label="Cierre de la cadena"><input value={fields.chainCloser} onChange={event => update('chainCloser', event.target.value)} /></Field>
    </div>
    <p className="form-note">Asistencia y escrutinios se mantienen desde el sistema. Estos campos completan únicamente los antecedentes narrativos del formulario oficial.</p>
    <button className="regularity-secondary" type="button" disabled={!baseContent.trim()} onClick={() => onApply(buildCompletedMinuteExtract(baseContent, fields))}>Aplicar al borrador</button>
  </details>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label><span>{label}</span>{children}</label> }
function value(text: string, empty = 'Sin registros') { return text.trim() || empty }

export function buildCompletedMinuteExtract(baseContent: string, fields: MinuteNarrativeFields) {
  const automated = baseContent.split('\nApertura:', 1)[0].trimEnd()
  const amount = fields.charityAmount ? new Intl.NumberFormat('es-CL').format(Number(fields.charityAmount)) : '________'
  return `${automated}\n\nANTECEDENTES DE LA TENIDA\n` +
    `Apertura: ${value(fields.openingTime, '________')} horas\n` +
    `Acta anterior: ${value(fields.previousMinuteDate, '________')} · Aprobada: ${fields.previousMinuteApproved}\n` +
    `Correspondencia: ${value(fields.correspondence)}\nDecretos: ${value(fields.decrees)}\nSaco de proposiciones: ${value(fields.proposalBag)}\n\n` +
    `TRABAJO\nAutor/a: ${value(fields.workAuthor)}\nTítulo: ${value(fields.workTitle)}\nAportes: ${value(fields.contributions)}\nBien General: ${value(fields.generalGood)}\n` +
    `Tronco de beneficencia: $ ${amount} m.p.\nClausura: ${value(fields.closingTime, '________')} horas\nCierre de la cadena: ${value(fields.chainCloser)}\n\n` +
    'FIRMAS\nVenerable Maestro/a: ____________________\nSecretario/a: ____________________\nOrador/a: ____________________'
}
