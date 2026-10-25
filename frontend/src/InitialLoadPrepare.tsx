import { useEffect, useState } from 'react'
import type { BootstrapApiClient, InitialLoadContract, InitialLoadStatus } from './api/bootstrapApi'
import { describeInitialLoadStatus, initialLoadStatusErrorMessage } from './initialLoadStatus'
import { describeInitialLoadFiles } from './initialLoadFiles'

/** #694 — «Preparar la carga»: plantillas CSV para Talleres, Hermanos y cargos. No recibe datos todavía. */
export default function InitialLoadPrepare({ bootstrapApi }: { bootstrapApi: BootstrapApiClient }) {
  const [contract, setContract] = useState<InitialLoadContract | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [downloading, setDownloading] = useState(false)
  const [status, setStatus] = useState<InitialLoadStatus | null>(null)
  const [statusError, setStatusError] = useState<string | null>(null)

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

  const statusView = describeInitialLoadStatus(status)
  const files = contract ? describeInitialLoadFiles(contract.files) : []
  return <section className="panel initial-load-prepare" aria-labelledby="initial-load-title">
    <p className="eyebrow">Carga inicial · Paso 1 de 3</p>
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
    <div className="initial-load-notice" role="note">
      <strong>Todavía no se pueden subir datos.</strong>
      <span>La recepción de la nómina real se habilitará cuando Jurídico apruebe el tratamiento de datos personales (Ley 21.719). Los pasos 2 (revisar) y 3 (cerrar la carga) aparecerán aquí cuando estén listos. Mientras tanto, no envíe archivos con datos reales por correo ni chat.</span>
    </div>
  </section>
}
