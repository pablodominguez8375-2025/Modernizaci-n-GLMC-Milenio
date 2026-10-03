import { useEffect, useState, type FormEvent } from 'react'
import { type AdmissionPersonOption, type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import { type MembershipApiClient } from './api/membershipApi'
import { affiliationModeForDate, chileCivilDate } from './admissionDates'
import './admissions.css'
import ExternalIncorporationDrawer from './ExternalIncorporationDrawer'

type AdmissionType = 'affiliation' | 'incorporation'
export default function AdmissionsPage({ api }: { api: PmgmApiClient; membershipApi: MembershipApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [type, setType] = useState<AdmissionType>('affiliation')
  const [query, setQuery] = useState('')
  const [people, setPeople] = useState<AdmissionPersonOption[]>([])
  const [personId, setPersonId] = useState('')
  const [searching, setSearching] = useState(false)
  const [searched, setSearched] = useState(false)
  const [originObedience, setOriginObedience] = useState('')
  const [originLodgeName, setOriginLodgeName] = useState('')
  const [originLodgeNumber, setOriginLodgeNumber] = useState('')
  const [degree, setDegree] = useState('master')
  const [hasPact, setHasPact] = useState('true')
  const [withdrawalDate, setWithdrawalDate] = useState('')
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let cancelled = false
    api.getOrganizationOptions().then(r => {
      if (cancelled) return
      const items = r.items.filter(x => x.type.toLowerCase() !== 'order')
      setOrganizations(items); setOrganizationId(items[0]?.id ?? '')
    }).catch(e => { if (!cancelled) setError(toMessage(e)) })
    return () => { cancelled = true }
  }, [api])
  useEffect(() => {
    let cancelled = false
    setPeople([]); setPersonId(''); setSearched(false); setSearching(false)
    if (!organizationId || query.trim().length < 3) return
    setSearching(true)
    const timer = window.setTimeout(() => {
      api.searchAdmissionPeople({ organizationId, admissionType: type, query: query.trim() })
        .then(r => { if (!cancelled) { setPeople(r.items); setSearched(true) } })
        .catch(e => { if (!cancelled) setError(toMessage(e)) })
        .finally(() => { if (!cancelled) setSearching(false) })
    }, 180)
    return () => { cancelled = true; window.clearTimeout(timer) }
  }, [api, organizationId, query, type])
  const resetSearch = () => { setPeople([]); setPersonId(''); setMessage(null); setError(null) }
  const selected = people.find(x => x.personId === personId) ?? null
  const derivedMode = affiliationModeForDate(withdrawalDate)
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setError(null); setMessage(null)
    if (!organizationId || !selected || searching) { setError('Busque y seleccione la identidad correcta.'); return }
    if (type === 'affiliation' && (!derivedMode || !selected.memberId)) { setError('Indique una fecha válida de otorgamiento de la Carta de Retiro Voluntario.'); return }
    setWorking(true)
    try {
      const result = await api.createAdmissionCase({
        organizationId, admissionType: type,
        personId: selected.personId, memberId: type === 'affiliation' ? selected.memberId : null,
        affiliationMode: type === 'affiliation' ? derivedMode : null,
        withdrawalLetterGrantedDate: type === 'affiliation' ? withdrawalDate : null,
        originObedience: type === 'incorporation' ? originObedience.trim() || null : null,
        originLodgeName: type === 'incorporation' ? originLodgeName.trim() || null : null,
        originLodgeNumber: type === 'incorporation' ? originLodgeNumber.trim() || null : null,
        degree: type === 'incorporation' ? degree : null,
        hasPeaceAndFriendshipPact: type === 'incorporation' ? (hasPact === 'unknown' ? null : hasPact === 'true') : null,
      })
      setMessage('Expediente ' + result.id + ' creado y enviado a revisión.')
    } catch (e) { setError(toMessage(e)) } finally { setWorking(false) }
  }
  return <div className="admissions-page">
    <section className="page-heading"><div><p className="eyebrow">Secretaría · admisiones especiales</p><h1>Afiliación e Incorporación</h1><p>Camino separado de Insinuados e iniciación. Reutilice la identidad existente.</p></div><span className="count-badge">Expediente trazable</span></section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}
    {type === 'incorporation' && <ExternalIncorporationDrawer key={organizationId} api={api} organizationId={organizationId} onCreated={result => { setMessage('Incorporación registrada para revisión. Expediente ' + result.id); setError(null); setQuery(''); setPeople([]); setPersonId('') }} />}
    <form className="panel admissions-form" onSubmit={submit}>
      <div className="admissions-switch" role="group" aria-label="Tipo de admisión">
        <button type="button" disabled={working} aria-pressed={type === 'affiliation'} className={type === 'affiliation' ? 'active' : ''} onClick={() => { resetSearch(); setType('affiliation') }}>Afiliación</button>
        <button type="button" disabled={working} aria-pressed={type === 'incorporation'} className={type === 'incorporation' ? 'active' : ''} onClick={() => { resetSearch(); setType('incorporation') }}>Incorporación</button>
      </div>
      <div className="form-grid">
        <label>Taller de destino<select required disabled={working} value={organizationId} onChange={e => { resetSearch(); setOrganizationId(e.target.value) }}><option value="">Seleccione…</option>{organizations.map(x => <option key={x.id} value={x.id}>{x.name}{x.number ? ' Nº ' + x.number : ''}</option>)}</select></label>
        {type === 'affiliation' && <label>Fecha Carta de Retiro Voluntario<input type="date" required max={chileCivilDate()} value={withdrawalDate} onChange={e => setWithdrawalDate(e.target.value)} /></label>}
        <label>Buscar identidad<input type="search" maxLength={80} disabled={working} value={query} onChange={e => { resetSearch(); setQuery(e.target.value) }} placeholder="Nombre o número institucional exacto" aria-describedby="admission-search-help" /></label>
        <label>Persona seleccionada<select required disabled={working || searching} value={personId} onChange={e => setPersonId(e.target.value)}><option value="">Seleccione…</option>{people.map(x => <option key={x.personId} value={x.personId}>{x.displayName}{x.institutionalNumber ? ' · ' + x.institutionalNumber : ''}</option>)}</select></label>
      </div>
      <p id="admission-search-help">{type === 'affiliation' ? 'Ingrese al menos tres caracteres. Incluye la historia del Taller; para otro Taller use el número institucional exacto.' : 'Busque una persona de expedientes autorizados. Para una identidad nueva use el botón Nueva persona de otra Obediencia.'}</p>
      {searching && <p role="status">Buscando…</p>}
      {searched && !searching && people.length === 0 && <p role="status">No hay coincidencias autorizadas. No cree una identidad duplicada.</p>}
      {selected && <div className="admission-identity"><strong>{selected.displayName}</strong><span>{selected.institutionalNumber ?? 'Persona externa, sin membresía GLMCh'}</span></div>}
      {type === 'affiliation' ? <fieldset><legend>Modalidad calculada por la Carta de Retiro</legend><div className="admission-derived-mode">{derivedMode ? derivedMode === 'simple' ? 'Afiliación simple' : 'Afiliación con activación' : 'Indique una fecha válida de la Carta de Retiro Voluntario.'}</div></fieldset> : <fieldset><legend>Antecedentes de incorporación</legend><div className="form-grid">
        <label>Obediencia de origen<input required value={originObedience} onChange={e => setOriginObedience(e.target.value)} /></label>
        <label>Taller de origen<input value={originLodgeName} onChange={e => setOriginLodgeName(e.target.value)} /></label>
        <label>Número de Taller<input value={originLodgeNumber} onChange={e => setOriginLodgeNumber(e.target.value)} /></label>
        <label>Grado<select value={degree} onChange={e => setDegree(e.target.value)}><option value="master">Maestro</option><option value="fellowcraft">Compañero</option><option value="apprentice">Aprendiz</option></select></label>
        <label>Pacto de Paz y Amistad<select value={hasPact} onChange={e => setHasPact(e.target.value)}><option value="true">Sí</option><option value="false">No</option><option value="unknown">No consta</option></select></label>
      </div></fieldset>}
      <div className="form-actions"><button className="primary-button" type="submit" disabled={working || searching || !selected}>{working ? 'Registrando…' : 'Crear expediente de admisión'}</button><small>La iniciación normal continúa en Secretaría → Insinuados → Circuito de iniciación.</small></div>
    </form>
  </div>
}
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'La API no respondió.' }
