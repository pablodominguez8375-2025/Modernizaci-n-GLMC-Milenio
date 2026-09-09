import { useMemo, useState } from 'react'
import { type BootstrapApiClient, type BootstrapApplyResponse, type BootstrapPlanResponse, type InstitutionalBootstrapRequest } from './api/bootstrapApi'

const initialPackage: InstitutionalBootstrapRequest = {
  packageKey: 'glmch-pilot-v1',
  packageVersion: 1,
  institution: { name: 'Gran Logia Mixta de Chile' },
  workshops: [{ name: 'Respetable Logia Libertad Nº 23', number: '23' }],
}

export default function BootstrapPage({ bootstrapApi }: { bootstrapApi: BootstrapApiClient }) {
  const [packageKey, setPackageKey] = useState(initialPackage.packageKey)
  const [packageVersion, setPackageVersion] = useState(initialPackage.packageVersion)
  const [institutionName, setInstitutionName] = useState(initialPackage.institution.name)
  const [workshopName, setWorkshopName] = useState(initialPackage.workshops[0].name)
  const [workshopNumber, setWorkshopNumber] = useState(initialPackage.workshops[0].number)
  const [plan, setPlan] = useState<BootstrapPlanResponse | null>(null)
  const [applied, setApplied] = useState<BootstrapApplyResponse | null>(null)
  const [confirmed, setConfirmed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const request = useMemo<InstitutionalBootstrapRequest>(() => ({
    packageKey: packageKey.trim(), packageVersion,
    institution: { name: institutionName.trim() },
    workshops: [{ name: workshopName.trim(), number: workshopNumber.trim() }],
  }), [packageKey, packageVersion, institutionName, workshopName, workshopNumber])

  function invalidatePlan() { setPlan(null); setApplied(null); setConfirmed(false); setError(null) }

  async function validate() {
    setBusy(true); setError(null); setApplied(null); setConfirmed(false)
    try { setPlan(await bootstrapApi.plan(request)) }
    catch (reason: unknown) { setPlan(null); setError(message(reason)) }
    finally { setBusy(false) }
  }

  async function apply() {
    if (!plan?.valid || !confirmed) return
    setBusy(true); setError(null)
    try { setApplied(await bootstrapApi.apply(request)); setPlan(await bootstrapApi.plan(request)) }
    catch (reason: unknown) { setError(message(reason)) }
    finally { setBusy(false) }
  }

  return <>
    <section className="page-heading">
      <div><p className="eyebrow">Superadmin · v0.33</p><h1>Configuración inicial</h1><p>Bootstrap controlado del piloto institucional. No carga personas ni credenciales y no requiere SQL manual.</p></div>
      <span className="count-badge">{plan?.alreadyApplied || applied?.alreadyApplied ? 'Configurado' : 'Pendiente'}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}

    <section className="panel">
      <h2>Paquete institucional</h2>
      <p>El paquete queda identificado por clave, versión y SHA-256. Una misma versión no puede reutilizarse con contenido diferente.</p>
      <div className="form-grid">
        <label><span>Clave del paquete</span><input value={packageKey} onChange={event => { setPackageKey(event.target.value); invalidatePlan() }} /></label>
        <label><span>Versión</span><input type="number" min={1} value={packageVersion} onChange={event => { setPackageVersion(Number(event.target.value)); invalidatePlan() }} /></label>
        <label><span>Institución matriz</span><input value={institutionName} onChange={event => { setInstitutionName(event.target.value); invalidatePlan() }} /></label>
        <label><span>Taller inicial</span><input value={workshopName} onChange={event => { setWorkshopName(event.target.value); invalidatePlan() }} /></label>
        <label><span>Número de Taller</span><input value={workshopNumber} onChange={event => { setWorkshopNumber(event.target.value); invalidatePlan() }} /></label>
      </div>
      <div className="button-row"><button type="button" onClick={() => void validate()} disabled={busy}>{busy ? 'Validando…' : 'Validar (dry-run)'}</button></div>
    </section>

    {plan && <section className="panel">
      <div className="section-heading"><div><p className="eyebrow">Resultado de validación</p><h2>{plan.valid ? 'Paquete válido' : 'Paquete observado'}</h2></div><span className={plan.valid ? 'status-pill complete' : 'status-pill'}>{plan.alreadyApplied ? 'Ya aplicado' : plan.valid ? 'Listo para aplicar' : 'Corregir'}</span></div>
      <dl className="candidate-meta">
        <div><dt>SHA-256</dt><dd><code>{plan.payloadSha256.slice(0, 20)}…</code></dd></div>
        <div><dt>Institución nueva</dt><dd>{plan.changes.institutionsToCreate}</dd></div>
        <div><dt>Talleres nuevos</dt><dd>{plan.changes.workshopsToCreate}</dd></div>
        <div><dt>Perfiles nuevos</dt><dd>{plan.changes.securityProfilesToCreate}</dd></div>
        <div><dt>Cargos nuevos</dt><dd>{plan.changes.officeDefinitionsToCreate}</dd></div>
      </dl>
      {plan.errors.length > 0 && <ul>{plan.errors.map(item => <li key={item}>{item}</li>)}</ul>}
      {plan.valid && !plan.alreadyApplied && <>
        <label className="search-field"><span>Confirmación</span><span><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Confirmo que este paquete corresponde a la estructura institucional inicial del piloto.</span></label>
        <button type="button" disabled={!confirmed || busy} onClick={() => void apply()}>{busy ? 'Aplicando…' : 'Aplicar configuración'}</button>
      </>}
    </section>}

    {applied?.applied && <section className="panel">
      <p className="eyebrow">Aplicación transaccional completada</p><h2>{applied.alreadyApplied ? 'La configuración ya existía' : 'Configuración inicial creada'}</h2>
      {applied.institution && <p><strong>{applied.institution.name}</strong> · ID {applied.institution.id}</p>}
      {applied.workshops.map(workshop => <p key={workshop.id}><strong>{workshop.name}</strong> · Nº {workshop.number} · ID {workshop.id}</p>)}
    </section>}

    {(applied?.catalog.securityProfiles.length ?? plan?.catalog.securityProfiles.length ?? 0) > 0 && <Catalog applied={applied} plan={plan} />}
  </>
}

function Catalog({ applied, plan }: { applied: BootstrapApplyResponse | null; plan: BootstrapPlanResponse | null }) {
  const catalog = applied?.catalog ?? plan?.catalog
  if (!catalog) return null
  return <section className="panel"><p className="eyebrow">Catálogo base</p><h2>Perfiles de seguridad y cargos</h2><div className="dashboard-grid"><article><h3>Perfiles de seguridad</h3><ul>{catalog.securityProfiles.map(item => <li key={item.code}><strong>{item.name}</strong> <small>· {item.scope}</small></li>)}</ul></article><article><h3>Cargos de Taller</h3><ul>{catalog.officeDefinitions.map(item => <li key={item.code}><strong>{item.name}</strong> <small>· {item.category === 'administrative' ? 'administrativo' : 'ritualístico'}</small></li>)}</ul></article></div></section>
}

function message(reason: unknown) { return reason instanceof Error ? reason.message : 'Error inesperado de configuración inicial.' }
