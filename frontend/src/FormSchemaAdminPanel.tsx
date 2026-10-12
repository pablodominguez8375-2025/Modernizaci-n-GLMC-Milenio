import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import {
  coreCandidateFormFields, type FormClassification, type FormFieldDefinition,
  type FormFieldType, type FormSchemaVersionsResponse,
} from './api/candidateFormTypes'
import './FormSchemaAdminPanel.css'

const fieldTypes: { type: FormFieldType; name: string }[] = [
  { type: 'text', name: 'Texto corto' }, { type: 'multiline', name: 'Texto extenso' },
  { type: 'number', name: 'Número' }, { type: 'date', name: 'Fecha' },
  { type: 'time', name: 'Hora' }, { type: 'currency', name: 'Importe' },
  { type: 'percentage', name: 'Porcentaje' }, { type: 'boolean', name: 'Sí/No' },
  { type: 'single_select', name: 'Lista de una respuesta' },
  { type: 'multi_select', name: 'Lista de varias respuestas' },
  { type: 'email', name: 'Correo' }, { type: 'phone', name: 'Teléfono' },
  { type: 'document', name: 'Documento con vínculo autorizado' },
  { type: 'photo', name: 'Fotografía con vínculo autorizado' },
  { type: 'reference', name: 'Referencia institucional' },
]

const nextDayLocal = () => {
  const date = new Date(Date.now() + 24 * 60 * 60 * 1000)
  return [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0')].join('-') + 'T09:00'
}

export default function FormSchemaAdminPanel({ api }: { api: PmgmApiClient }) {
  const [history, setHistory] = useState<FormSchemaVersionsResponse | null>(null)
  const [fields, setFields] = useState<FormFieldDefinition[]>(() => coreCandidateFormFields.map(field => ({ ...field })))
  const [priorKeys, setPriorKeys] = useState<string[]>([])
  const [effectiveLocal, setEffectiveLocal] = useState(nextDayLocal)
  const [newKey, setNewKey] = useState('')
  const [newLabel, setNewLabel] = useState('')
  const [newType, setNewType] = useState<FormFieldType>('text')
  const [loading, setLoading] = useState(true)
  const [catalogVerified, setCatalogVerified] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    let active = true
    setLoading(true); setError(null); setCatalogVerified(false)
    async function load() {
      try {
        const [catalog, versions] = await Promise.all([api.getFormSchemaCatalog(), api.getFormSchemaVersions()])
        const expected = new Set(coreCandidateFormFields.map(item => item.key))
        const actual = catalog.forms.find(x => x.formCode === 'candidate_profile')?.trustedCoreKeys ?? []
        if (actual.length !== expected.size || actual.some(key => !expected.has(key)))
          throw new Error('El catálogo institucional del servidor cambió. Se bloqueó la edición para proteger los campos núcleo.')
        const current = versions.versions[0]
          ? await api.getFormSchemaVersion(versions.versions[0].version)
          : null
        if (!active) return
        setHistory(versions)
        setFields(current ? structuredClone(current.fields) : coreCandidateFormFields.map(field => ({ ...field })))
        setCatalogVerified(true)
        setPriorKeys(current?.fields.map(field => field.key) ?? [])
      } catch (reason) {
        if (active) setError(toMessage(reason))
      } finally {
        if (active) setLoading(false)
      }
    }
    void load()
    return () => { active = false }
  }, [api, refresh])

  const current = history?.versions[0]
  const expectedPreviousVersion = current?.version ?? 0
  const custom = fields.filter(field => !field.isCore)
  const updateField = (key: string, patch: Partial<FormFieldDefinition>) =>
    setFields(rows => rows.map(field => field.key === key && !field.isCore
      ? { ...field, ...patch } : field))

  function addField() {
    setMessage(null); setError(null)
    const key = newKey.trim()
    const label = newLabel.trim()
    if (!/^[a-z][a-z0-9_]{1,63}$/.test(key) || label.length < 1 || label.length > 120) {
      setError('Ingrese una clave estable en minúsculas (2–64 caracteres) y una etiqueta de 1–120 caracteres.')
      return
    }
    if (fields.some(field => field.key === key)) { setError('La clave del campo ya existe.'); return }
    const classification: FormClassification = newType === 'email' || newType === 'phone' ? 'sensitive' :
      newType === 'document' || newType === 'photo' ? 'restricted' : 'internal'
    const sortOrder = Math.min(10000, Math.max(100, ...fields.map(field => field.sortOrder)) + 10)
    setFields(rows => [...rows, {
      key, label, typeCode: newType, isCore: false, enabled: true, visible: true,
      required: false, editable: true, classification, sortOrder,
      options: newType === 'single_select' || newType === 'multi_select'
        ? ['Opción 1', 'Opción 2'] : null,
    }])
    setNewKey(''); setNewLabel(''); setNewType('text')
  }

  async function publish() {
    if (busy || loading || !catalogVerified) return
    if (fields.length <= coreCandidateFormFields.length) {
      setError('Agregue al menos un campo adicional antes de publicar la primera configuración.')
      return
    }
    const future = new Date(effectiveLocal)
    if (!Number.isFinite(future.getTime()) || future.getTime() <= Date.now()) {
      setError('La fecha y hora de entrada en vigencia debe ser futura.')
      return
    }
    setBusy(true); setError(null); setMessage(null)
    try {
      const published = await api.publishFormSchemaVersion({
        expectedPreviousVersion, version: expectedPreviousVersion + 1,
        effectiveFromUtc: future.toISOString(), fields,
      })
      setMessage('Configuración ' + published.version +
        ' programada con éxito. Las versiones históricas permanecen inmutables y los expedientes existentes no se reescriben.')
      setRefresh(x => x + 1)
    } catch (reason) {
      setError(toMessage(reason) + ' Si hubo otro cambio, recargue el historial.')
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <section className="panel form-schema-admin" role="status">Cargando catálogo institucional y versiones autorizadas…</section>
  return <section className="panel form-schema-admin" aria-label="Configuración institucional de formularios">
    <header className="form-schema-heading">
      <div><p className="eyebrow">Sistema · Formularios · PMGM-BLG-074</p>
        <h2>Ficha de Insinuado: campos adicionales</h2>
        <p>Solo Superadministración y Administración de Gran Logia pueden publicar. La API comprueba permisos y protege los campos núcleo.</p>
      </div>
      <span className="count-badge">Próxima v{expectedPreviousVersion + 1}</span>
    </header>
    {error && <div className="error-banner" role="alert">{error}</div>}
    {message && <p className="regularity-success" role="status">{message}</p>}
    <p className="form-schema-info">Las definiciones institucionales anteriores nunca se borran. Si un campo publicado deja de usarse, desactívelo; no cambie su identificador ni su tipo. Los datos sensibles no se publican en el portal.</p>
    <div className="form-schema-summary">
      <strong>{coreCandidateFormFields.length} campos núcleo protegidos</strong>
      <span>{custom.length} adicionales, {custom.filter(field => field.enabled).length} activos</span>
      <span>{history?.total ?? 0} versiones publicadas</span>
    </div>
    <details className="form-schema-core">
      <summary>Ver campos institucionales protegidos</summary>
      <ul>{coreCandidateFormFields.map(field => <li key={field.key}>{field.label} <code>{field.key}</code></li>)}</ul>
    </details>
    <h3>Campos configurables</h3>
    <div className="form-schema-cards">
      {custom.map(field => <article className="form-schema-card" key={field.key}>
        <div className="form-schema-card-head">
          <strong>{field.label}</strong>
          <code>{field.key}</code>
          <small>{priorKeys.includes(field.key) ? 'Campo histórico: clave y tipo bloqueados' : 'Campo nuevo'}</small>
        </div>
        <div className="form-schema-control-grid">
          <label><span>Etiqueta</span><input value={field.label} maxLength={120}
            onChange={event => updateField(field.key, { label: event.target.value })} /></label>
          <label><span>Tipo de dato (inmutable)</span><input disabled value={fieldTypes.find(x => x.type === field.typeCode)?.name ?? field.typeCode} /></label>
          <label><span>Clasificación de acceso</span><select value={field.classification}
            onChange={event => updateField(field.key, { classification: event.target.value as FormClassification })}>
              {(['internal', 'sensitive', 'restricted'] as const).filter(level =>
                ({ internal: 1, sensitive: 2, restricted: 3 }[level] >=
                 { internal: 1, sensitive: 2, restricted: 3 }[field.classification])).map(level =>
                <option key={level} value={level}>{level === 'internal' ? 'Interno' : level === 'sensitive' ? 'Sensible' : 'Restringido'}</option>)}
            </select></label>
          <label><span>Orden</span><input type="number" min={0} max={10000} value={field.sortOrder}
            onChange={event => updateField(field.key, { sortOrder: Number(event.target.value) })} /></label>
          <label className="form-schema-wide"><span>Texto de ayuda</span>
            <input value={field.helpText ?? ''} maxLength={500}
              onChange={event => updateField(field.key, { helpText: event.target.value || null })} /></label>
          {(field.typeCode === 'single_select' || field.typeCode === 'multi_select') &&
            <label className="form-schema-wide"><span>Opciones (una por línea)</span>
              <textarea rows={4} value={(field.options ?? []).join('\n')}
                onChange={event => updateField(field.key, {
                  options: event.target.value.split('\n').map(option => option.trim()).filter(Boolean),
                })} /></label>}
          <label className="form-schema-wide"><span>Mostrar si este campo adicional está informado (opcional)</span>
            <select value={field.dependsOnKey ?? ''} onChange={event => updateField(field.key, { dependsOnKey: event.target.value || null })}>
              <option value="">Sin condición</option>
              {custom.filter(other => other.key !== field.key && other.enabled && !other.dependsOnKey)
                .map(other => <option key={other.key} value={other.key}>{other.label}</option>)}
            </select></label>
        </div>
        <div className="form-schema-toggles">
          <label><input type="checkbox" checked={field.enabled}
            onChange={event => updateField(field.key, event.target.checked
              ? { enabled: true, editable: true } : { enabled: false, visible: false, editable: false, required: false })} /> Habilitado</label>
          <label><input type="checkbox" checked={field.visible} disabled={!field.enabled}
            onChange={event => updateField(field.key, event.target.checked
              ? { visible: true } : { visible: false, required: false })} /> Visible</label>
          <label><input type="checkbox" checked={field.required} disabled={!field.enabled || !field.visible}
            onChange={event => updateField(field.key, { required: event.target.checked })} /> Obligatorio</label>
          <label><input type="checkbox" checked={field.editable} disabled={!field.enabled}
            onChange={event => updateField(field.key, { editable: event.target.checked })} /> Editable</label>
        </div>
      </article>)}
      {custom.length === 0 && <p>No hay campos adicionales; puede crear el primero sin modificar la ficha institucional existente.</p>}
    </div>
    <fieldset className="form-schema-add">
      <legend>Agregar un campo complementario</legend>
      <div className="form-schema-control-grid">
        <label><span>Clave estable (sin espacios)</span><input value={newKey} maxLength={64} placeholder="custom_antecedente"
          onChange={event => setNewKey(event.target.value.toLowerCase())} /></label>
        <label><span>Nombre visible</span><input value={newLabel} maxLength={120} placeholder="Antecedente complementario"
          onChange={event => setNewLabel(event.target.value)} /></label>
        <label><span>Tipo</span><select value={newType} onChange={event => setNewType(event.target.value as FormFieldType)}>
          {fieldTypes.map(item => <option key={item.type} value={item.type}>{item.name}</option>)}
        </select></label>
      </div>
      <button type="button" className="secondary" onClick={addField}>Añadir campo</button>
      <small>Para documentos, fotos y referencias, la captura requiere autorización del Gestor Documental y aún no admite carga directa.</small>
    </fieldset>
    <div className="form-schema-submit">
      <label><span>Vigente desde (fecha y hora local)</span><input type="datetime-local" value={effectiveLocal}
        onChange={event => setEffectiveLocal(event.target.value)} /></label>
      <button type="button" onClick={() => { void publish() }} disabled={busy || loading || !catalogVerified}>
        {busy ? 'Publicando versión…' : 'Publicar nueva versión auditada'}
      </button>
    </div>
    <div className="form-schema-versions">
      <h3>Versiones institucionales</h3>
      {(history?.versions ?? []).length === 0 ? <p>No hay versiones publicadas.</p> :
        <ol>{history!.versions.map(item => <li key={item.version}>
          <strong>Versión {item.version}</strong>
          <span>Vigencia: {formatDate(item.effectiveFromUtc)}</span>
          <small>Huella SHA-256: <code>{item.payloadSha256.slice(0, 12)}…</code></small>
        </li>)}</ol>}
    </div>
  </section>
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('es-CL', { dateStyle: 'medium', timeStyle: 'short' })
}
function toMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : 'No se pudo consultar o publicar el formulario institucional.'
}
