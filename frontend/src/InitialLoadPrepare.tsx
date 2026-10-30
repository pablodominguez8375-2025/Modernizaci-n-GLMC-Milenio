import { useEffect, useState } from 'react'
import type { BootstrapApiClient, InitialLoadContract, InitialLoadStatus } from './api/bootstrapApi'
import { describeInitialLoadStatus, initialLoadStatusErrorMessage } from './initialLoadStatus'
import { describeInitialLoadFiles } from './initialLoadFiles'
import { describeFieldEs, describeFindingEs, gradeOptions, initialLoadColumns, initialLoadFilePurpose, normalizeAll, officeOptions, spanishTemplate, statusOptions } from './initialLoadSpanish'
import { INITIAL_LOAD_FILE_NAMES, checkChosenFile, checkTexts, describeField, describeFinding, describePreview } from './initialLoadPreview'
import type { InitialLoadFileName, InitialLoadPreviewResult } from './initialLoadPreview'

/** #694/#750 — «Preparar la carga»: plantillas CSV y revisión (solo lectura) de los tres archivos. No guarda ni importa datos. */
export default function InitialLoadPrepare({ bootstrapApi }: { bootstrapApi: BootstrapApiClient }) {
  const [contract, setContract] = useState<InitialLoadContract | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [downloading, setDownloading] = useState(false)
  const [status, setStatus] = useState<InitialLoadStatus | null>(null)
  const [statusError, setStatusError] = useState<string | null>(null)
  // Los textos viven solo en memoria mientras se revisan; nunca van a almacenamiento del navegador.
  const [texts, setTexts] = useState<Partial<Record<InitialLoadFileName, string>>>({})
  const [names, setNames] = useState<Partial<Record<InitialLoadFileName, string>>>({})
  const [fileError, setFileError] = useState<string | null>(null)
  const [reviewing, setReviewing] = useState(false)
  const [preview, setPreview] = useState<InitialLoadPreviewResult | null>(null)
  const [previewError, setPreviewError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    bootstrapApi.initialLoadContract()
      .then(value => { if (active) setContract(value) })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar el contrato de la carga inicial.') })
    bootstrapApi.initialLoadStatus()
      .then(value => { if (active) setStatus(value) })
      .catch(reason => { if (active) setStatusError(initialLoadStatusErrorMessage(reason instanceof Error ? reason.message : undefined)) })
    return () => { active = false }
  }, [bootstrapApi])

  async function download() {
    setDownloading(true); setError(null)
    try {
      const { blob, fileName } = await bootstrapApi.downloadInitialLoadTemplate()
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName
      document.body.append(anchor); anchor.click(); anchor.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible descargar las plantillas.') }
    finally { setDownloading(false) }
  }

  async function choose(name: InitialLoadFileName, file: File | undefined) {
    setPreview(null); setPreviewError(null); setFileError(null)
    if (!file) { setTexts(t => { const n = { ...t }; delete n[name]; return n }); setNames(t => { const n = { ...t }; delete n[name]; return n }); return }
    const problem = checkChosenFile(name, file)
    if (problem) { setFileError(problem); return }
    const text = await file.text()
    setTexts(t => ({ ...t, [name]: text })); setNames(t => ({ ...t, [name]: file.name }))
  }

  async function review() {
    const problem = checkTexts(texts)
    if (problem) { setFileError(problem); return }
    setReviewing(true); setPreview(null); setPreviewError(null); setFileError(null)
    try { setPreview(await bootstrapApi.previewInitialLoad(normalizeAll(texts))) }
    catch (reason) { setPreviewError(reason instanceof Error ? reason.message : 'No fue posible revisar los archivos.') }
    finally { setReviewing(false) }
  }

  function downloadSpanish(name: InitialLoadFileName) {
    const url = URL.createObjectURL(new Blob([spanishTemplate(name)], { type: 'text/csv;charset=utf-8' }))
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = name.replace('.csv', '-plantilla.csv')
    document.body.append(anchor); anchor.click(); anchor.remove()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  function clearFiles() { setTexts({}); setNames({}); setPreview(null); setPreviewError(null); setFileError(null) }

  const statusView = describeInitialLoadStatus(status)
  const files = contract ? describeInitialLoadFiles(contract.files) : []
  return <section className="panel initial-load-prepare" aria-labelledby="initial-load-title">
    <p className="eyebrow">Carga inicial · Paso 1 de 3 · Plantillas</p>
    <h2 id="initial-load-title">Preparar la carga de Talleres, Hermanos y cargos</h2>
    <p>Para poner en marcha los Talleres reales se cargan tres archivos: los Talleres, los hermanos de cada Taller y los cargos vigentes. Siga la guía de abajo: descargue las plantillas en español, complételas fuera del sistema y guárdelas como CSV.</p>
    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    <div className={`initial-load-status ${statusError ? 'warn' : statusView.tone}`} role="status">
      <strong>{statusError ? 'Estado de la carga' : statusView.label}</strong>
      <span>{statusError ?? statusView.detail}</span>
    </div>
    <div className="initial-load-guide">
      <h3>Guía: qué cargar y cómo llenarlo</h3>
      <ol className="initial-load-howto">
        <li>Descargue las tres plantillas (vienen en español y vacías, solo con los títulos de las columnas).</li>
        <li>Ábralas con Excel, llénelas <strong>una fila por registro</strong> y guárdelas como «CSV». No cambie los títulos de las columnas ni el orden.</li>
        <li>Llene primero Talleres, luego Hermanos y al final Cargos vigentes: los dos últimos usan los códigos que usted puso antes.</li>
        <li>Use solo datos reales de la institución; en la demostración, solo datos ficticios.</li>
      </ol>
      {INITIAL_LOAD_FILE_NAMES.map(name => <details key={name} className="initial-load-file" open={name === 'Talleres.csv'}>
        <summary><strong>{name.replace('.csv', '')}</strong><span>{initialLoadFilePurpose[name]}</span></summary>
        <div className="table-wrap"><table><thead><tr><th>Columna</th><th>Qué escribir</th><th>Ejemplo</th></tr></thead><tbody>
          {initialLoadColumns[name].map(c => <tr key={c.key}><td><strong>{c.label}</strong>{c.required ? '' : <small> (opcional)</small>}</td><td>{c.help}</td><td>{c.example}</td></tr>)}
        </tbody></table></div>
        <button type="button" className="action-trigger secondary" onClick={() => downloadSpanish(name)}>Descargar plantilla de {name.replace('.csv', '')}</button>
      </details>)}
      <details className="initial-load-file"><summary><strong>Listas de valores permitidos</strong><span>Grados, estados y cargos</span></summary>
        <p><strong>Grado:</strong> {gradeOptions.map(o => o.label).join(' · ')}</p>
        <p><strong>Estado institucional:</strong> {statusOptions.map(o => o.label).join(' · ')}</p>
        <p><strong>Cargo:</strong> {officeOptions.map(o => o.label).join(' · ')}</p>
      </details>
      <p className="initial-load-hint">Excel en español guarda los CSV separados por «;»: el sistema lo entiende. Las fechas se escriben día-mes-año (15-03-2026).</p>
    </div>
    <details className="initial-load-official"><summary>Plantillas oficiales del servidor (títulos técnicos en inglés)</summary>
      <ol className="initial-load-files">
        {files.map(file => <li key={file.name}><strong>{file.name}</strong><span>{file.purpose}</span><small>Columnas: {file.columns.join(', ')}</small></li>)}
      </ol>
      <div className="button-row"><button type="button" className="action-trigger secondary" disabled={downloading || !contract} onClick={() => void download()}>{downloading ? 'Descargando…' : 'Descargar plantillas oficiales (ZIP)'}</button></div>
    </details>
    <h3 className="initial-load-step">Paso 2 de 3 · Revisar los archivos</h3>
    <p>Elija los tres archivos ya completados. El sistema solo los <strong>revisa</strong> (encabezados, columnas y largos): <strong>no guarda ni carga ningún dato</strong>. Los resultados muestran la fila y la columna con el problema, nunca el contenido.</p>
    <div className="initial-load-pickers">
      {INITIAL_LOAD_FILE_NAMES.map(name => <label key={name} className="initial-load-picker">
        <span><strong>{name}</strong><small>{names[name] ? 'Archivo listo para revisar' : 'Sin elegir'}</small></span>
        <input type="file" accept=".csv,text/csv" onChange={event => void choose(name, event.target.files?.[0])} />
      </label>)}
    </div>
    {fileError && <div className="error-banner" role="alert"><strong>Revise el archivo.</strong><span>{fileError}</span></div>}
    {previewError && <div className="error-banner" role="alert"><strong>No fue posible revisar los archivos.</strong><span>{previewError}</span></div>}
    <div className="button-row">
      <button type="button" className="action-trigger primary" disabled={reviewing || INITIAL_LOAD_FILE_NAMES.some(n => texts[n] === undefined)} onClick={() => void review()}>{reviewing ? 'Revisando…' : 'Revisar archivos'}</button>
      <button type="button" className="action-trigger secondary" disabled={reviewing || INITIAL_LOAD_FILE_NAMES.every(n => texts[n] === undefined)} onClick={clearFiles}>Quitar los archivos</button>
    </div>
    {preview && (() => { const view = describePreview(preview); return <div className={`initial-load-result ${view.tone}`} role="status">
      <strong>{view.title}</strong><span>{view.detail}</span>
      <ul className="initial-load-counts">{view.counts.map(c => <li key={c.name}><span>{c.name}</span><strong>{c.rows} fila(s)</strong></li>)}</ul>
      {preview.findings.length > 0 && <div className="table-wrap"><table><thead><tr><th>Archivo</th><th>Fila</th><th>Columna</th><th>Qué pasa</th></tr></thead><tbody>
        {preview.findings.map((f, i) => <tr key={`${f.sheet}-${f.row}-${f.field}-${i}`}><td>{f.sheet}</td><td>{f.row}</td><td>{describeFieldEs(f.field) ?? describeField(f.field)}</td><td>{describeFindingEs(f.code) ?? describeFinding(f.code)}</td></tr>)}
      </tbody></table></div>}
    </div>})()}
    <h3 className="initial-load-step">Paso 3 de 3 · Aplicar y cerrar la carga</h3>
    <div className="button-row">
      <button type="button" className="action-trigger secondary" disabled title="Todavía no disponible">Aplicar la carga</button>
      <button type="button" className="action-trigger secondary" disabled title="Todavía no disponible">Cerrar la carga</button>
    </div>
    <div className="initial-load-notice" role="note">
      <strong>Aplicar y cerrar todavía no están disponibles.</strong>
      <span>La importación real (con auditoría y sin duplicados) aún no está lista, por eso estos botones están inactivos y ningún dato se guarda. La revisión jurídica del tratamiento de datos personales (Ley 21.719) la hace la institución fuera del sistema; el sistema no la comprueba. En la demostración use solo datos ficticios.</span>
    </div>
  </section>
}
