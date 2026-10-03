import { useState, type FormEvent } from 'react'
import { ActionDrawer } from './actionKit'
import type { AdmissionCaseResponse, PmgmApiClient, ExternalIncorporationRequest } from './api/pmgmApi'
import { validateExternalIncorporation } from './api/externalIncorporation'

export default function ExternalIncorporationDrawer({ api, organizationId, onCreated }: { api: PmgmApiClient; organizationId: string; onCreated: (result: AdmissionCaseResponse) => void }) {
  const [open, setOpen] = useState(false)
  const [step, setStep] = useState(1)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [data, setData] = useState<Omit<ExternalIncorporationRequest, 'organizationId'>>({ firstNames: '', lastNames: '', rutOrInstitutionalId: '', originObedience: '', degree: 'apprentice', originLodgeName: '', originLodgeNumber: '' })
  const field = (key: keyof typeof data, value: string) => { setData(current => ({ ...current, [key]: value })); setStep(1); setError(null) }
  async function submit(event: FormEvent) {
    event.preventDefault(); setError(null)
    try {
      const payload = { ...data, organizationId }
      validateExternalIncorporation(payload)
      setWorking(true)
      const result = await api.createExternalIncorporation(payload)
      onCreated(result); setOpen(false); setStep(1)
      setData({ firstNames: '', lastNames: '', rutOrInstitutionalId: '', originObedience: '', degree: 'apprentice', originLodgeName: '', originLodgeNumber: '' })
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible registrar la incorporación.') }
    finally { setWorking(false) }
  }
  return <ActionDrawer label="+ Nueva persona de otra Obediencia" title="Nueva incorporación" description="La identidad y el expediente se registran juntos. Los antecedentes deben acreditarse antes de autorizar." open={open} onOpenChange={value => { if (!working) setOpen(value) }} keepOpen disabled={!organizationId || working}>
    {error && <p role="alert">{error}</p>}
    <p>Paso {step} de 3 · El borrador se conserva al cerrar el panel.</p>
    <form onSubmit={submit}>
      <fieldset disabled={working} hidden={step !== 1}><legend>Identidad</legend><div className="form-grid">
        <label>Nombres<input required maxLength={160} value={data.firstNames} onChange={e => field('firstNames', e.target.value)} /></label>
        <label>Apellidos<input required maxLength={160} value={data.lastNames} onChange={e => field('lastNames', e.target.value)} /></label>
        <label>RUT o identificación<input required maxLength={20} value={data.rutOrInstitutionalId} onChange={e => field('rutOrInstitutionalId', e.target.value)} /></label>
      </div><p>Hasta 16 caracteres al quitar puntos, guiones y espacios. Si ya existe, solicite revisión al área autorizada.</p></fieldset>
      <fieldset disabled={working} hidden={step !== 2}><legend>Procedencia y grado declarado</legend><div className="form-grid">
        <label>Obediencia de origen<input required maxLength={240} value={data.originObedience} onChange={e => setData({ ...data, originObedience: e.target.value })} /></label>
        <label>Taller de origen<input maxLength={240} value={data.originLodgeName ?? ''} onChange={e => setData({ ...data, originLodgeName: e.target.value })} /></label>
        <label>Número del Taller<input maxLength={80} value={data.originLodgeNumber ?? ''} onChange={e => setData({ ...data, originLodgeNumber: e.target.value })} /></label>
        <label>Grado<select value={data.degree} onChange={e => setData({ ...data, degree: e.target.value as typeof data.degree })}><option value="apprentice">Aprendiz</option><option value="fellowcraft">Compañero</option><option value="master">Maestro</option></select></label>
      </div></fieldset>
      {step === 3 && <section><h3>Revise antes de registrar</h3><p><strong>{data.firstNames} {data.lastNames}</strong></p><p>Identificación: {data.rutOrInstitutionalId}</p><p>{data.originObedience} · {data.originLodgeName}</p><p>Grado declarado: {data.degree === 'master' ? 'Maestro' : data.degree === 'fellowcraft' ? 'Compañero' : 'Aprendiz'}.</p><p>La identificación se guardará en la base maestra. Pacto de Paz y Amistad queda «No consta». Deben revisarse documentos legalizados y Carta de Retiro Voluntario con firma manuscrita. Este registro no autoriza la incorporación.</p></section>}
      <div className="form-actions">
        {step > 1 && <button type="button" disabled={working} onClick={() => setStep(step - 1)}>Volver</button>}
        {step < 3 ? <button type="button" disabled={working} onClick={() => {
          if (step === 1 && (!data.firstNames.trim() || !data.lastNames.trim() || !data.rutOrInstitutionalId.trim())) { setError('Complete la identidad antes de continuar.'); return }
          if (step === 2) { try { validateExternalIncorporation({ ...data, organizationId }) } catch (reason) { setError(String(reason)); return } }
          setError(null); setStep(step + 1)
        }}>Continuar</button> : <button type="submit" className="primary-button" disabled={working}>{working ? 'Registrando…' : 'Confirmar y registrar incorporación'}</button>}
      </div>
    </form>
  </ActionDrawer>
}
