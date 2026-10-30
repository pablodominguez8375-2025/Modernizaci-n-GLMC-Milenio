import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ActionDrawer, ConfirmAction, HelpNote } from './actionKit'
import { DocumentApiHttpError, type DocumentApiClient } from './api/documentApi'
import { canDelegateUpload, collaboratorActionLabels, collaboratorCategories, type AreaCollaborator, type AreaCollaborators, type CollaboratorAction, type CollaboratorArea, type AddAreaCollaborator } from './api/areaCollaborators'

const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago' }).format(new Date())
const areaTitle: Record<CollaboratorArea, string> = { library: 'Biblioteca Virtual', communications: 'Comunicaciones', documentmanager: 'Gestión Documental' }
const toMessage = (e: unknown) => {
  if (e instanceof DocumentApiHttpError) {
    if (e.status === 409) return 'La lista cambió o el usuario ya tiene acceso vigente a esa categoría en ese período. Se recargó; revise y repita.'
    if (e.status === 400) return 'Revise el usuario, las acciones y las fechas.'
    if (e.status === 404) return 'Ese acceso ya no existe en esta área.'
  }
  return e instanceof Error ? e.message : 'No se pudo completar la acción.'
}
const fmt = (d: string) => d.split('-').reverse().join('-')

/** Colaboradores de un área: acceso limitado por categoría, acciones y vigencia. Solo lo ve el responsable del área. */
export default function AreaCollaboratorsSection({ documentApi, area, title = 'Colaboradores' }: { documentApi: DocumentApiClient; area: CollaboratorArea; title?: string }) {
  const [data, setData] = useState<AreaCollaborators | null>(null)
  const [denied, setDenied] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const categories = collaboratorCategories[area]
  const [category, setCategory] = useState(categories[0].value)
  const [actions, setActions] = useState<CollaboratorAction[]>(['view'])
  const [subject, setSubject] = useState('')
  const [people, setPeople] = useState<Array<{ subject: string; name: string }>>([])
  const [manual, setManual] = useState(false)
  const [from, setFrom] = useState(today())
  const [until, setUntil] = useState('')

  const load = useCallback(async () => {
    try { setData(await documentApi.getAreaCollaborators(area)); setDenied(false) }
    catch (e) { if (e instanceof DocumentApiHttpError && e.status === 403) setDenied(true); else setError(toMessage(e)) }
  }, [documentApi, area])
  useEffect(() => { void load() }, [load])
  useEffect(() => {
    let live = true
    documentApi.getAreaCollaboratorCandidates(area).then(r => { if (live && r.available) setPeople(r.items) }).catch(() => { /* sin lista: se ingresa el identificador a mano */ })
    return () => { live = false }
  }, [documentApi, area])
  if (denied) return null

  const upload = canDelegateUpload(area, category)
  const toggle = (a: CollaboratorAction) => setActions(cur => cur.includes(a) ? cur.filter(x => x !== a) : [...cur, a])
  const pickCategory = (value: string) => { setCategory(value); if (!canDelegateUpload(area, value)) setActions(cur => cur.filter(a => a !== 'write')) }

  async function add(event: FormEvent) {
    event.preventDefault(); if (!data) return
    setBusy(true); setError(''); setNotice('')
    try {
      setData(await documentApi.addAreaCollaborator(area, { subject: subject.trim(), category, actions: ['view', ...actions.filter(a => a !== 'view')], effectiveFrom: from, effectiveTo: until || null, expectedVersion: data.version }))
      setNotice('Acceso otorgado y registrado en auditoría. Tiene efecto de inmediato.'); setSubject(''); setActions(['view'])
    } catch (e) { setError(toMessage(e)); await load() } finally { setBusy(false) }
  }
  async function update(item: AreaCollaborator, request: Omit<AddAreaCollaborator, 'expectedVersion'>) {
    if (!data) return
    setBusy(true); setError(''); setNotice('')
    try {
      setData(await documentApi.updateAreaCollaborator(area, item.assignmentId, { ...request, expectedVersion: data.version }))
      setNotice('Colaborador modificado y registrado en auditoría.')
    } catch (e) { setError(toMessage(e)); await load() } finally { setBusy(false) }
  }
  async function revoke(item: AreaCollaborator) {
    if (!data) return
    setBusy(true); setError(''); setNotice('')
    try { setData(await documentApi.revokeAreaCollaborator(area, item.assignmentId, data.version)); setNotice('Acceso revocado; el historial queda en auditoría.') }
    catch (e) { setError(toMessage(e)); await load() } finally { setBusy(false) }
  }
  const label = (v: string) => categories.find(c => c.value === v)?.label ?? v

  return <section className="panel area-collaborators" aria-label={`Colaboradores de ${areaTitle[area]}`}>
    <header><div><h2>{title}</h2><p>Personas con acceso limitado a una categoría de {areaTitle[area]}. No pueden dar acceso a otros ni salir de su categoría.</p></div>
      <ActionDrawer label="Agregar colaborador" title="Agregar colaborador" description="Elija la persona, la categoría, lo que puede hacer y hasta cuándo." keepOpen>
        <form className="library-admin-form" onSubmit={event => void add(event)}>
          {people.length > 0 && !manual
            ? <label><span>Persona</span><select required value={subject} disabled={busy} onChange={e => setSubject(e.target.value)}><option value="">Seleccione a la persona</option>{people.map(p => <option key={p.subject} value={p.subject}>{p.name}</option>)}</select><button type="button" className="action-link" onClick={() => { setManual(true); setSubject('') }}>No aparece en la lista: escribir su identificador</button></label>
            : <label><span>Identificador del usuario</span><input required maxLength={320} value={subject} disabled={busy} onChange={e => setSubject(e.target.value)} /><small>Lo entrega el administrador de la plataforma al crear la cuenta; no sirve el correo.</small>{people.length > 0 && <button type="button" className="action-link" onClick={() => { setManual(false); setSubject('') }}>Elegir de la lista de personas</button>}</label>}
          <label><span>Categoría</span><select value={category} disabled={busy} onChange={e => pickCategory(e.target.value)}>{categories.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}</select></label>
          <fieldset className="area-collaborators-actions"><legend>Qué puede hacer</legend>
            <label><input type="checkbox" checked disabled /> {collaboratorActionLabels.view} <small>(siempre)</small></label>
            {(['create', 'edit', 'delete'] as CollaboratorAction[]).map(a => <label key={a}><input type="checkbox" checked={actions.includes(a)} disabled={busy} onChange={() => toggle(a)} /> {collaboratorActionLabels[a]}</label>)}
            {upload ? <label><input type="checkbox" checked={actions.includes('write')} disabled={busy} onChange={() => toggle('write')} /> {collaboratorActionLabels.write}</label> : <small>Subir y publicar contenido solo se delega para la Revista y el Boletín.</small>}
          </fieldset>
          <label><span>Desde</span><input type="date" required value={from} disabled={busy} onChange={e => setFrom(e.target.value)} /></label>
          <label><span>Hasta (opcional)</span><input type="date" min={from} value={until} disabled={busy} onChange={e => setUntil(e.target.value)} /></label>
          <div className="library-admin-actions"><button type="submit" className="action-trigger primary" disabled={busy || !data}>{busy ? 'Guardando…' : 'Otorgar acceso'}</button></div>
        </form>
      </ActionDrawer></header>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {data && data.items.length === 0 && <p className="library-admin-empty">No hay colaboradores. Solo quien administra esta área y el Máster pueden agregarlos.</p>}
    <div className="library-admin-list">{data?.items.map(item => <article key={item.assignmentId} className="library-admin-card">
      <div><strong>{label(item.category)}</strong><span>{people.find(p => p.subject === item.subject)?.name ?? item.subject}</span><small>{item.actions.map(a => collaboratorActionLabels[a]).join(' · ')} · desde {fmt(item.effectiveFrom)}{item.effectiveTo ? ` hasta ${fmt(item.effectiveTo)}` : ' sin fecha de término'}{item.effectiveTo && item.effectiveTo < today() ? ' · vencido' : ''}</small></div>
      <div className="library-admin-row-actions">
        {item.isActive && <EditCollaboratorAction area={area} item={item} disabled={busy} onSave={request => void update(item, request)} />}
        <ConfirmAction label="Revocar" tone="secondary" message="Pierde el acceso de inmediato. Queda registrado en auditoría." disabled={busy} onConfirm={() => void revoke(item)} /></div>
    </article>)}</div>
    <HelpNote>El acceso se verifica siempre en el servidor. Vencida la fecha, el colaborador deja de ver la categoría sin que usted haga nada.</HelpNote>
  </section>
}


/** Editar exclusivamente una asignación del área desde la cual fue listada. */
function EditCollaboratorAction({ area, item, disabled, onSave }: {
  area: CollaboratorArea; item: AreaCollaborator; disabled: boolean
  onSave: (request: Omit<AddAreaCollaborator, 'expectedVersion'>) => void
}) {
  const [subject, setSubject] = useState(item.subject)
  const [category, setCategory] = useState(item.category)
  const [actions, setActions] = useState<CollaboratorAction[]>(item.actions)
  const [effectiveFrom, setEffectiveFrom] = useState(item.effectiveFrom)
  const [effectiveTo, setEffectiveTo] = useState(item.effectiveTo ?? '')
  const options = collaboratorCategories[area]
  const toggle = (action: CollaboratorAction) =>
    setActions(current => current.includes(action) ? current.filter(x => x !== action) : [...current, action])
  const chooseCategory = (value: string) => {
    setCategory(value)
    if (!canDelegateUpload(area, value)) setActions(current => current.filter(x => x !== 'write'))
  }
  return <ActionDrawer label="Modificar" title="Modificar colaborador"
    description="Cambie la persona, categoría, acciones o vigencia sin crear un acceso en otra área." keepOpen>
    <form className="library-admin-form" onSubmit={event => {
      event.preventDefault()
      onSave({ subject: subject.trim(), category, actions: ['view', ...actions.filter(x => x !== 'view')],
        effectiveFrom, effectiveTo: effectiveTo || null })
    }}>
      <label><span>Identificador del usuario</span><input required maxLength={320} value={subject} disabled={disabled} onChange={event => setSubject(event.target.value)} /></label>
      <label><span>Categoría</span><select value={category} disabled={disabled} onChange={event => chooseCategory(event.target.value)}>
        {options.map(option => <option value={option.value} key={option.value}>{option.label}</option>)}
      </select></label>
      <fieldset className="area-collaborators-actions"><legend>Qué puede hacer</legend>
        <label><input type="checkbox" checked disabled /> {collaboratorActionLabels.view} <small>(siempre)</small></label>
        {(['create', 'edit', 'delete'] as CollaboratorAction[]).map(a => <label key={a}>
          <input type="checkbox" checked={actions.includes(a)} disabled={disabled} onChange={() => toggle(a)} /> {collaboratorActionLabels[a]}</label>)}
        {canDelegateUpload(area, category) && <label><input type="checkbox" checked={actions.includes('write')} disabled={disabled} onChange={() => toggle('write')} /> {collaboratorActionLabels.write}</label>}
      </fieldset>
      <label><span>Desde</span><input type="date" required value={effectiveFrom} disabled={disabled} onChange={event => setEffectiveFrom(event.target.value)} /></label>
      <label><span>Hasta (opcional)</span><input type="date" min={effectiveFrom} value={effectiveTo} disabled={disabled} onChange={event => setEffectiveTo(event.target.value)} /></label>
      <button type="submit" className="action-trigger primary" disabled={disabled}>Guardar cambios</button>
    </form>
  </ActionDrawer>
}
