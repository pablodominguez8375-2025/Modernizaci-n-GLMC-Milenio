import { useEffect, useState } from 'react'
import type { BootstrapApiClient, InitialLoadContract, InitialLoadStatus } from './api/bootstrapApi'
import { describeInitialLoadStatus, initialLoadStatusErrorMessage } from './initialLoadStatus'
import { describeInitialLoadFiles } from './initialLoadFiles'
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
    try { setPreview(await bootstrapApi.previewInitialLoad(texts as Record<string, string>)) }
    catch (reason) { setPreviewError(reason instanceof Error ? reason.message : 'No fue posible revisar los archivos.') }
    finally { setReviewing(false) }
  }

  function clearFiles() { setTexts({}); setNames({}); setPreview(null); setPreviewError(null); setFileError(null) }

  const statusView = describeInitialLoadStatus(status)
  const files = contract ? describeInitialLoadFiles(contract.files) : []
  return <section className="panel initial-load-prepare" aria-labelledby="initial-load-title">
    <p className="eyebrow">Carga inicial · Paso 1 de 3 · Plantillas</p>
    <h2 id="initial-load-title">Preparar la carga de Talleres, Hermanos y cargos</h2>
    <p>Para poner en marcha los Talleres reales se cargan tres archivos: los Talleres, los hermanos de cada Taller y los cargos vigentes. Descargue las plantillas, complételas fuera del sistema y guárdelas como CSV. Las plantillas vienen vacías: solo traen los encabezados.</p>
    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    <div className={`initial-load-status ${statusError ? 'warn' : statusView.tone}`} role="status">
      <strong>{statusError ? 'Estado de la carga' : statusView.label}</strong>
      <span>{statusError ?? statusView.detail}</span>
    </div>
    <ol className="initial-load-files">
      {files.map(file => <li key={file.name}>
        <strong>{file.name}</strong>
        <span>{file.purpose}</span>
        <small>Columnas: {file.columns.join(', ')}</small>
      </li>)}
    </ol>
    <div className="button-row"><button type="button" className="action-trigger primary" disabled={downloading || !contract} onClick={() => void download()}>{downloading ? 'Descargando…' : 'Descargar plantillas CSV'}</button></div>
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
        {preview.findings.map((f, i) => <tr key={`${f.sheet}-${f.row}-${f.field}-${i}`}><td>{f.sheet}</td><td>{f.row}</td><td>{describeField(f.field)}</td><td>{describeFinding(f.code)}</td></tr>)}
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
