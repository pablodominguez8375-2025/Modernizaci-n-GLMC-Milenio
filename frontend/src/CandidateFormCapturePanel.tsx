import { useEffect, useMemo, useState } from 'react'
import type { CandidateIntakeApiClient } from './api/candidateIntakeApi'
import {
  capturedValueTypes, isActiveDependency, isProvided,
  prepareCandidateFormCapture, visibleCandidateFormFields,
  type ActiveCandidateFormSchema, type CandidateFormCaptureDetail,
  type CandidateFormCaptureInfo, type FormFieldDefinition, type FormValue, type FormValues,
} from './api/candidateFormTypes'
import './CandidateFormCapturePanel.css'

interface CandidateFormCapturePanelProps {
  api: CandidateIntakeApiClient
  requestId: string
  editable: boolean
}

export default function CandidateFormCapturePanel({ api, requestId, editable }: CandidateFormCapturePanelProps) {
  const [schema, setSchema] = useState<ActiveCandidateFormSchema | null>(null)
  const [history, setHistory] = useState<CandidateFormCaptureInfo[]>([])
  const [selected, setSelected] = useState<CandidateFormCaptureDetail | null>(null)
  const [draft, setDraft] = useState<FormValues>({})
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [revision, setRevision] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    setSchema(null); setHistory([]); setSelected(null); setDraft({}); setLoading(true)
    setError(null)
    async function load() {
      try {
        // A capture remains readable after the active scheme changes, so fetch
        // the history independently of optional current schema availability.
        const [definition, past] = await Promise.all([
          api.getCandidateFormSchema(requestId).catch(reason => {
            if (httpStatus(reason) === 404) return null
            throw reason
          }),
          api.getCandidateFormCaptureHistory(requestId),
        ])
        const latest = past.captures[0]
        const historical = latest
          ? await api.getCandidateFormCapture(requestId, latest.captureRevision)
          : null
        if (!live) return
        setSchema(definition)
        setHistory(past.captures)
        setSelected(historical)
        if (definition && historical?.schemaVersion === definition.schemaVersion)
          setDraft({ ...historical.values })
      } catch (reason) {
        if (live) setError(toError(reason))
      } finally {
        if (live) setLoading(false)
      }
    }
    void load()
    return () => { live = false }
  }, [api, requestId, revision])

  const fields = useMemo(() => visibleCandidateFormFields(schema?.fields ?? []), [schema])
  const writable = editable && Boolean(schema) && fields.some(field =>
    field.editable && capturedValueTypes.has(field.typeCode))

  async function showRevision(next: number) {
    setError(null)
    try {
      const capture = await api.getCandidateFormCapture(requestId, next)
      setSelected(capture)
    } catch (reason) {
      setError(toError(reason))
    }
  }

  async function exportRevision(format: 'csv' | 'docx' | 'pdf') {
    if (!selected || exporting) return
    const captureRevision = selected.captureRevision
    setExporting(true)
    setError(null)
    try {
      const blob = format === 'csv'
        ? await api.exportCandidateFormCaptureCsv(requestId, captureRevision)
        : format === 'docx'
          ? await api.exportCandidateFormCaptureDocx(requestId, captureRevision)
          : await api.exportCandidateFormCapturePdf(requestId, captureRevision)
      const objectUrl = URL.createObjectURL(blob)
      try {
        const link = document.createElement('a')
        link.href = objectUrl
        link.download = 'insinuado-campos-adicionales-' + requestId +
          '-r' + captureRevision + '.' + format
        link.style.display = 'none'
        document.body.appendChild(link)
        try { link.click() } finally { link.remove() }
      } finally {
        // Short-lived object URLs; source data is not written to local storage.
        window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)
      }
      setMessage('Exportación privada ' + format.toUpperCase() + ' de revisión ' +
        captureRevision + ' preparada. El documento contiene antecedentes reservados: ' +
        'guárdelo únicamente en un lugar institucional autorizado.')
    } catch (reason) {
      setError(toError(reason))
    } finally {
      setExporting(false)
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!schema || !writable || saving) return
    const result = prepareCandidateFormCapture(fields, draft)
    if (result.errors.length > 0) { setError(result.errors.join(' ')); return }
    setSaving(true); setError(null); setMessage(null)
    try {
      const saved = await api.saveCandidateFormCapture(requestId, {
        schemaVersion: schema.schemaVersion,
        expectedCaptureRevision: schema.expectedCaptureRevision,
        values: result.values,
      })
      setMessage('Campos adicionales guardados como revisión ' + saved.captureRevision +
        '. La ficha institucional y las revisiones anteriores no fueron modificadas.')
      setRevision(old => old + 1)
    } catch (reason) {
      setError(httpStatus(reason) === 409
        ? 'La ficha o su configuración cambió. Recargue antes de volver a guardar. ' + toError(reason)
        : toError(reason))
    } finally {
      setSaving(false)
    }
  }

  return <section className="candidate-product-card form-capture-panel" aria-label="Campos configurables privados">
    <div className="candidate-section-title">
      <span aria-hidden="true">⚙</span>
      <h2>Campos adicionales configurables</h2>
      {schema && <em>Esquema v{schema.schemaVersion}</em>}
    </div>
    <p className="form-capture-intro">Los datos institucionales de identidad, Taller y estado permanecen en la ficha oficial. Esta sección guarda únicamente información complementaria privada, con versión e historial independientes.</p>
    {loading ? <p role="status">Consultando definición e historial autorizado…</p> : <>
      {error && <div className="error-banner" role="alert">{error}</div>}
      {message && <div className="candidate-protected-notice" role="status">{message}</div>}
      {!schema && <p className="form-capture-empty">No existe todavía una definición de campos adicionales vigente para este expediente. Puede continuar usando la ficha institucional habitual.</p>}
      {schema && fields.length === 0 && <p className="form-capture-empty">La versión vigente no contiene campos adicionales visibles.</p>}
      {schema && fields.length > 0 && <>
        <p className="form-capture-meta">Versión vigente {schema.schemaVersion} · Ámbito institucional {schema.scopeKey === 'order' ? 'Gran Logia' : 'Taller'}. Última revisión {schema.expectedCaptureRevision}.</p>
        {editable ? <form onSubmit={event => { void submit(event) }} className="form-capture-form">
          <div className="form-capture-fields">
            {fields.filter(field => isActiveDependency(field, draft)).map(field =>
              <CaptureInput key={field.key} field={field} value={draft[field.key]}
                onChange={value => setDraft(current => ({ ...current, [field.key]: value }))}
                disabled={saving || !field.editable} />)}
          </div>
          <div className="form-capture-actions">
            <small>Cada guardado crea una nueva revisión; no reemplaza ni publica la ficha. Los adjuntos se vinculan exclusivamente desde Gestión Documental.</small>
            <button className="candidate-primary-button" type="submit" disabled={!writable || saving}>
              {saving ? 'Guardando revisión…' : 'Guardar campos adicionales'}
            </button>
          </div>
        </form> : <p className="form-capture-meta">Vista de consulta privada. Gran Secretaría no modifica estos antecedentes desde la bandeja de revisión.</p>}
      </>}
      {history.length > 0 && <div className="form-capture-history">
        <h3>Historial privado de capturas</h3>
        <label htmlFor={'capture-revision-' + requestId}>Revisión guardada</label>
        <select id={'capture-revision-' + requestId} value={selected?.captureRevision ?? ''}
          onChange={event => { void showRevision(Number(event.target.value)) }}>
          {history.map(item => <option key={item.captureRevision} value={item.captureRevision}>
            Revisión {item.captureRevision} · esquema v{item.schemaVersion} · {formatRecorded(item.capturedAtUtc)}
          </option>)}
        </select>
        {selected && <div className="form-capture-history-actions">
          <button className="candidate-secondary-button" type="button"
            disabled={exporting} onClick={() => { void exportRevision('csv') }}>
            {exporting ? 'Preparando descarga…' : 'Exportar revisión CSV'}
          </button>
          <button className="candidate-secondary-button" type="button"
            disabled={exporting} onClick={() => { void exportRevision('docx') }}>
            {exporting ? 'Preparando descarga…' : 'Exportar revisión Word'}
          </button>
          <button className="candidate-secondary-button" type="button"
            disabled={exporting} onClick={() => { void exportRevision('pdf') }}>
            {exporting ? 'Preparando descarga…' : 'Exportar revisión PDF'}
          </button>
          <small>Exportación individual y auditable a CSV, Word o PDF. Los archivos contienen
            antecedentes privados y deben permanecer bajo custodia institucional.</small>
        </div>}
        {selected && <div className="form-capture-historical">
          <p>Lectura histórica de revisión {selected.captureRevision}, bajo la versión {selected.schemaVersion} del formulario. Los valores permanecen protegidos.</p>
          <dl>{Object.entries(selected.values).map(([key, value]) => {
            const field = selected.fields?.find(item => item.key === key)
            return <div key={key}><dt>{field?.label ?? key}</dt><dd>{displayValue(value)}</dd></div>
          })}</dl>
        </div>}
      </div>}
    </>}
  </section>
}

function CaptureInput({ field, value, onChange, disabled }: {
  field: FormFieldDefinition
  value: FormValue | undefined
  onChange: (next: FormValue) => void
  disabled: boolean
}) {
  const id = 'capture-field-' + field.key
  const unsupported = !capturedValueTypes.has(field.typeCode)
  if (unsupported) return <div className="form-capture-field form-capture-unsupported">
    <strong>{field.label}</strong>
    <small>Este tipo se vincula desde el Gestor Documental, con autorización y antivirus; no se permite adjuntarlo directamente.</small>
  </div>
  const required = field.required
  const textValue = typeof value === 'string' || typeof value === 'number' ? String(value) : ''
  return <div className="form-capture-field">
    <label htmlFor={id}>{field.label}{required ? ' *' : ''}</label>
    {field.typeCode === 'multiline'
      ? <textarea id={id} rows={4} maxLength={10000} value={textValue}
          disabled={disabled} onChange={event => onChange(event.target.value)} />
      : field.typeCode === 'boolean'
        ? <select id={id} value={typeof value === 'boolean' ? String(value) : ''}
            disabled={disabled} onChange={event => onChange(event.target.value === '' ? null : event.target.value === 'true')}>
            <option value="">Sin respuesta</option><option value="true">Sí</option><option value="false">No</option>
          </select>
        : field.typeCode === 'single_select'
          ? <select id={id} value={textValue} disabled={disabled}
              onChange={event => onChange(event.target.value || null)}>
              <option value="">Seleccione…</option>
              {(field.options ?? []).map(option => <option key={option} value={option}>{option}</option>)}
            </select>
          : field.typeCode === 'multi_select'
            ? <div className="form-capture-options" id={id} role="group" aria-label={field.label}>
                {(field.options ?? []).map(option => <label key={option}>
                  <input type="checkbox" checked={Array.isArray(value) && value.includes(option)}
                    disabled={disabled}
                    onChange={event => {
                      const selected = Array.isArray(value) ? value : []
                      onChange(event.target.checked ? [...selected, option] : selected.filter(x => x !== option))
                    }} />{option}
                </label>)}
              </div>
            : <input id={id} type={inputType(field.typeCode)} value={textValue}
                min={field.typeCode === 'percentage' ? '0' : undefined}
                max={field.typeCode === 'percentage' ? '100' : undefined}
                step={['number', 'currency', 'percentage'].includes(field.typeCode) ? 'any' : undefined}
                maxLength={['email', 'phone'].includes(field.typeCode) ? 254 : 2000}
                disabled={disabled} onChange={event => onChange(event.target.value)} />}
    {field.helpText && <small>{field.helpText}</small>}
    {disabled && <small>Campo informativo de solo lectura.</small>}
  </div>
}

function inputType(type: FormFieldDefinition['typeCode']): string {
  if (type === 'email') return 'email'
  if (type === 'phone') return 'tel'
  if (type === 'date') return 'date'
  if (type === 'time') return 'time'
  if (['number', 'currency', 'percentage'].includes(type)) return 'number'
  return 'text'
}
function displayValue(value: FormValue): string {
  if (value === null || !isProvided(value)) return 'No informado'
  if (value === true) return 'Sí'
  if (value === false) return 'No'
  return Array.isArray(value) ? value.join(', ') : String(value)
}
function formatRecorded(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('es-CL', { dateStyle: 'short', timeStyle: 'short' })
}
function toError(reason: unknown): string {
  return reason instanceof Error ? reason.message : 'No fue posible consultar o guardar los campos privados.'
}
function httpStatus(reason: unknown): number | null {
  if (reason && typeof reason === 'object' && 'status' in reason &&
      typeof (reason as { status: unknown }).status === 'number')
    return (reason as { status: number }).status
  return null
}
