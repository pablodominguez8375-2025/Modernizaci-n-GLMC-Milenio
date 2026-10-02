import { useEffect, useState, type CSSProperties } from 'react'
import { organizationNumberOf } from './displayFormat'
import InstitutionalIcon from './InstitutionalIcon'
import type { MembershipApiClient, MemberSelfProfile } from './api/membershipApi'
import type { SessionProfile } from './api/pmgmApi'
import type { DocumentApiClient } from './api/documentApi'
import MemberWorkPapersPanel from './MemberWorkPapersPanel'
import MemberTreasuryCredits from './MemberTreasuryCredits'
import './memberPortalInstruction.css'
import './memberLibraryShortcut.css'

const formatTreasuryMoney=(amount:number,currency:'CLP'|'USD'='CLP')=>new Intl.NumberFormat('es-CL',{style:'currency',currency,maximumFractionDigits:currency==='USD'?2:0}).format(amount)

interface MemberPortalPageProps {
  profile: SessionProfile | null
  useMocks: boolean
  membershipApi: MembershipApiClient
  documentApi: DocumentApiClient
  onOpenCalendar?: () => void
  onOpenNotifications?: () => void
  onOpenLibrary?: () => void
  onOpenLodge?: () => void
}

type EditablePersonalData = {
  email: string
  phone: string
  city: string
  address: string
}

type MemberAttendanceActivity = {
  fromDate: string
  throughDate: string
  total: number
  present: number
  absent: number
  excused: number
  percentage: number | null
  history: Array<{
    meetingId: string
    meetingDate: string
    meetingType: string
    grade: string
    title: string | null
    attendanceStatus: string
  }>
}

type MemberInstructionActivity = {
  total: number
  history: Array<{
    instructionSessionId: string
    instructionDate: string
    grade: string
    topic: string
    attendanceStatus: string
    responsibleOffice: string
  }>
}

type MemberUpcomingMeeting = {
  id: string
  meetingDate: string
  meetingType: string
  grade: string
  title: string | null
  status: string
}

type MemberSelfOperationalProfile = MemberSelfProfile & {
  activity?: {
    attendance: MemberAttendanceActivity
    instruction: MemberInstructionActivity
    upcomingMeetings: MemberUpcomingMeeting[]
  }
}

export const editableMemberFields = ['email', 'phone', 'address'] as const

export const memberPortalDemoData = {
  fullName: 'Hermano Demostrativo',
  displayName: 'H∴ Demostrativo',
  memberId: 'DEMO-0001',
  birthDate: '14 de marzo de 1978',
  profession: 'Profesional · dato ficticio',
  personal: {
    email: 'hermano.demo@ejemplo.cl',
    phone: '+56 9 0000 0000',
    city: 'Santiago · demo',
    address: 'Dirección ficticia para demostración',
  },
  institutional: {
    lodge: 'Taller Demostrativo Nº 23',
    orient: 'Santiago',
    degree: 'Maestro (3°)',
    status: 'Activo',
    initiation: '12 de octubre de 2013',
    wageIncrease: '18 de junio de 2015',
    exaltation: '21 de mayo de 2017',
  },
  attendance: { percentage: 87, attended: 26, absent: 3, excused: 1, total: 30 },
  instruction: [
    { date: '22 ago 2026', degree: '3°', topic: 'Ética y filosofía', attendance: 'Presente', responsible: 'Inmediato Ex Venerable Maestro' },
    { date: '08 ago 2026', degree: '3°', topic: 'Trabajo en Taller', attendance: 'Presente', responsible: 'Inmediato Ex Venerable Maestro' },
    { date: '18 jul 2026', degree: '3°', topic: 'Historia de la Orden', attendance: 'Justificada', responsible: 'Inmediato Ex Venerable Maestro' },
    { date: '04 jul 2026', degree: '3°', topic: 'Ritual y simbolismo', attendance: 'Presente', responsible: 'Inmediato Ex Venerable Maestro' },
  ],
  treasury: { status: 'Al día', detail: 'Sin cuotas pendientes' },
  hospitalaria: { status: 'Activo', detail: 'Situación hospitalaria vigente' },
  meetings: [
    { date: '12 sep', time: '19:00', title: 'Tenida Ordinaria', lodge: 'Taller Demostrativo Nº 23', status: 'Confirmada' },
    { date: '26 sep', time: '19:00', title: 'Tenida de Instrucción', lodge: 'Taller Demostrativo Nº 23', status: 'Por confirmar' },
    { date: '10 oct', time: '19:00', title: 'Tenida Blanca', lodge: 'Taller Demostrativo Nº 8', status: 'Invitación' },
  ],
  notifications: [
    { title: 'Nueva circular disponible', detail: 'Circular demostrativa · actividades del mes', age: 'hace 2 días' },
    { title: 'Cambio de horario de tenida', detail: 'La próxima tenida comienza a las 19:00 hrs.', age: 'hace 4 días' },
    { title: 'Recordatorio de cuota', detail: 'La situación de tesorería se encuentra al día.', age: 'hace 6 días' },
    { title: 'Material de instrucción disponible', detail: 'Nuevo recurso autorizado para el grado.', age: 'hace 1 semana' },
  ],
} as const

export default function MemberPortalPage({ profile, useMocks, membershipApi, documentApi, onOpenCalendar, onOpenNotifications, onOpenLibrary, onOpenLodge }: MemberPortalPageProps) {
  const [editing, setEditing] = useState(false)
  const [personal, setPersonal] = useState<EditablePersonalData>({ ...memberPortalDemoData.personal })
  const [selfProfile, setSelfProfile] = useState<MemberSelfProfile | null>(null)
  const [loadingSelf, setLoadingSelf] = useState(!useMocks)
  const [selfError, setSelfError] = useState<string | null>(null)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [showTreasuryDetail, setShowTreasuryDetail] = useState(false)

  useEffect(() => {
    let active = true
    setEditing(false)
    setSaveMessage(null)
    setSelfError(null)

    if (useMocks) {
      setLoadingSelf(false)
      membershipApi.getSelfProfile().then(result => {
        if (!active) return
        setSelfProfile(result)
        setPersonal({ email: result.contact.email ?? '', phone: result.contact.phone ?? '', city: memberPortalDemoData.personal.city, address: result.contact.address ?? '' })
      }).catch(() => undefined)
      return () => { active = false }
    }

    setLoadingSelf(true)
    membershipApi.getSelfProfile()
      .then(result => {
        if (!active) return
        setSelfProfile(result)
        setPersonal({ email: result.contact.email ?? '', phone: result.contact.phone ?? '', city: '', address: result.contact.address ?? '' })
      })
      .catch((reason: unknown) => {
        if (active) setSelfError(reason instanceof Error ? reason.message : 'No fue posible cargar tu ficha institucional.')
      })
      .finally(() => { if (active) setLoadingSelf(false) })
    return () => { active = false }
  }, [membershipApi, useMocks])

  const savePersonalData = async () => {
    setSaving(true)
    setSaveMessage(null)
    setSelfError(null)
    try {
      const result = await membershipApi.updateSelfContact({ email: personal.email || null, phone: personal.phone || null, address: personal.address || null })
      setSelfProfile(current => current ? { ...current, contact: { email: personal.email || null, phone: personal.phone || null, address: personal.address || null } } : current)
      setSaveMessage(result.status === 'updated' ? 'Tus datos personales fueron actualizados y la modificación quedó auditada.' : 'No había cambios pendientes en tus datos personales.')
      setEditing(false)
    } catch (reason: unknown) {
      setSelfError(reason instanceof Error ? reason.message : 'No fue posible guardar tus datos personales.')
    } finally {
      setSaving(false)
    }
  }

  const operationalProfile = selfProfile as MemberSelfOperationalProfile | null
  const activity = useMocks ? undefined : operationalProfile?.activity
  const fullName = useMocks
    ? memberPortalDemoData.fullName
    : selfProfile
      ? `${selfProfile.member.firstNames} ${selfProfile.member.lastNames}`.trim()
      : (profile?.displayName ?? 'Hermano')
  const memberId = useMocks ? memberPortalDemoData.memberId : (selfProfile?.member.institutionalNumber ?? 'Sin número institucional')
  const lodge = useMocks ? memberPortalDemoData.institutional.lodge : (selfProfile?.current.membership?.organization ?? 'Sin Taller vigente')
  const lodgeSeal = organizationNumberOf(lodge) ?? (lodge.trim().charAt(0).toUpperCase() || '∴')
  const orient = useMocks ? memberPortalDemoData.institutional.orient : 'Según expediente institucional'
  const effectiveDegree = useMocks ? 3 : (selfProfile?.current.effectiveDegree ?? Number.parseInt(selfProfile?.current.degree?.degree ?? '', 10))
  const degree = useMocks ? memberPortalDemoData.institutional.degree : formatDegree(selfProfile?.current.effectiveDegree, selfProfile?.current.degree?.degree)
  const libraryAccess = libraryAccessForDegree(effectiveDegree)
  const status = useMocks ? memberPortalDemoData.institutional.status : formatInstitutionalStatus(selfProfile?.current.institutionalStatus?.eventType)
  const initiation = useMocks ? memberPortalDemoData.institutional.initiation : formatDateOnly(selfProfile?.milestones.initiation)
  const wageIncrease = useMocks ? memberPortalDemoData.institutional.wageIncrease : formatDateOnly(selfProfile?.milestones.wageIncrease)
  const exaltation = useMocks ? memberPortalDemoData.institutional.exaltation : formatDateOnly(selfProfile?.milestones.exaltation)
  const treasury = useMocks ? memberPortalDemoData.treasury : formatRegularity(selfProfile?.regularity.financial?.status, 'tesorería')
  const hospitalaria = useMocks ? memberPortalDemoData.hospitalaria : formatRegularity(selfProfile?.regularity.hospitalaria?.status, 'hospitalaria')
  const treasuryAccount = selfProfile?.treasuryAccount
  const canEdit = useMocks || Boolean(selfProfile)

  return <div className="member-portal">
    <section className="member-portal-heading">
      <div>
        <p className="member-breadcrumb">Portal del Hermano <span>›</span> Mi ficha</p>
        <h1>Mi ficha</h1>
        <p>Tu información personal, masónica y de participación en un solo lugar.</p>
      </div>
      <button className="member-primary-action" type="button" disabled={!canEdit || loadingSelf || saving} onClick={() => setEditing(value => !value)}>{editing ? 'Cerrar edición' : 'Editar mis datos'}</button>
    </section>

    {loadingSelf && <div className="member-live-notice">Cargando tu expediente institucional…</div>}
    {selfError && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{selfError}</span></div>}
    {saveMessage && <div className="member-live-notice">{saveMessage}</div>}

    <section className="member-profile-grid">
      <article className="member-card member-identity-card">
        <div className="member-avatar-wrap"><img className="member-avatar-photo" src={`${import.meta.env.BASE_URL}demo-member-avatar.svg`} alt={useMocks ? 'Avatar demostrativo' : 'Avatar institucional'} /></div>
        <div className="member-identity-content">
          <p className="member-card-kicker">Ficha personal</p>
          <h2>{fullName}</h2>
          <p className="member-display-name">{useMocks ? memberPortalDemoData.displayName : 'Ficha personal autenticada'}</p>
          <div className="member-data-grid">
            <MemberDatum label="Identificador" value={memberId} />
            <MemberDatum label="Nacimiento" value={useMocks ? memberPortalDemoData.birthDate : 'No expuesto en autoservicio'} />
            <MemberDatum label="Correo" value={personal.email || 'Sin registrar'} />
            <MemberDatum label="Teléfono" value={personal.phone || 'Sin registrar'} />
            <MemberDatum label="Profesión" value={useMocks ? memberPortalDemoData.profession : 'No expuesto en autoservicio'} />
            <MemberDatum label={useMocks ? 'Ciudad' : 'Domicilio'} value={useMocks ? personal.city : (personal.address || 'Sin registrar')} />
          </div>
        </div>
      </article>

      <article className="member-card member-institutional-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Datos masónicos</p><h2>Información institucional</h2></div><span className="member-lock-badge">Sólo lectura</span></div>
        <div className="member-lodge-line"><span className="member-lodge-seal" aria-hidden="true">{lodgeSeal}</span><div><small>Taller</small><strong>{lodge}</strong><span>{orient}</span></div></div>
        <div className="member-institutional-summary">
          <MemberDatum label="Grado" value={degree} />
          <MemberDatum label="Estado" value={status} success={status === 'Activo'} />
          <MemberDatum label="Iniciación" value={initiation} />
          <MemberDatum label="Aumento de salario" value={wageIncrease} />
          <MemberDatum label="Exaltación" value={exaltation} />
        </div>
        {onOpenLibrary && <button className="member-degree-library-link" type="button" onClick={onOpenLibrary} aria-label={`Abrir Biblioteca Virtual. ${libraryAccess.title}. ${libraryAccess.detail}`}>
          <span className="member-degree-library-icon" aria-hidden="true"><InstitutionalIcon name="library" size={24} /></span>
          <span className="member-degree-library-copy"><small>Biblioteca Virtual · acceso por grado</small><strong>{libraryAccess.title}</strong><span>{libraryAccess.detail}</span></span>
          <span className="member-degree-library-action">Abrir</span>
        </button>}
      </article>

      <aside className="member-quote-card"><span className="member-quote-mark">“</span><p>Que nuestras acciones sean testimonio de los principios que profesamos.</p><span className="member-quote-rule" /><strong>Proyecto Centenario</strong><small>Libertad · Igualdad · Fraternidad</small></aside>
    </section>

    {editing && <section className="member-card member-edit-card">
      <div className="member-card-title-row"><div><p className="member-card-kicker">Autogestión</p><h2>Datos personales editables</h2><p>Los datos institucionales permanecen protegidos.</p></div><span className="member-demo-chip">{useMocks ? 'Demostración local' : 'Edición auditada'}</span></div>
      <div className="member-edit-grid">
        <MemberInput label="Correo" value={personal.email} onChange={value => setPersonal(current => ({ ...current, email: value }))} />
        <MemberInput label="Teléfono" value={personal.phone} onChange={value => setPersonal(current => ({ ...current, phone: value }))} />
        {useMocks && <MemberInput label="Ciudad" value={personal.city} onChange={value => setPersonal(current => ({ ...current, city: value }))} />}
        <MemberInput label="Domicilio" value={personal.address} onChange={value => setPersonal(current => ({ ...current, address: value }))} />
      </div>
      <div className="member-edit-actions"><button type="button" disabled={saving} onClick={() => void savePersonalData()}>{saving ? 'Guardando…' : 'Guardar cambios'}</button><small>{useMocks ? 'Los datos de QA son ficticios y permanecen sólo en la sesión demostrativa.' : 'Sólo se modifican tus datos de contacto; grado, Taller, estado y fechas masónicas no pueden editarse aquí.'}</small></div>
    </section>}

    <section className="member-insight-grid">
      <article className="member-card member-attendance-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Participación</p><h2>Asistencia a tenidas</h2><p>Últimos 12 meses</p></div></div>
        {useMocks
          ? <div className="member-attendance-layout"><div className="member-ring" style={{ '--member-progress': `${memberPortalDemoData.attendance.percentage}%` } as CSSProperties}><div><strong>{memberPortalDemoData.attendance.percentage}%</strong><small>{memberPortalDemoData.attendance.attended} de {memberPortalDemoData.attendance.total}</small></div></div><ul><li><span className="dot success" />Asistidas <strong>{memberPortalDemoData.attendance.attended}</strong></li><li><span className="dot danger" />Inasistencias <strong>{memberPortalDemoData.attendance.absent}</strong></li><li><span className="dot info" />Justificadas <strong>{memberPortalDemoData.attendance.excused}</strong></li></ul></div>
          : activity?.attendance && activity.attendance.total > 0
            ? <div className="member-attendance-layout"><div className="member-ring" style={{ '--member-progress': `${activity.attendance.percentage ?? 0}%` } as CSSProperties}><div><strong>{activity.attendance.percentage ?? 0}%</strong><small>{activity.attendance.present} de {activity.attendance.total}</small></div></div><ul><li><span className="dot success" />Asistidas <strong>{activity.attendance.present}</strong></li><li><span className="dot danger" />Inasistencias <strong>{activity.attendance.absent}</strong></li><li><span className="dot info" />Justificadas <strong>{activity.attendance.excused}</strong></li></ul></div>
            : <PortalPendingData text="Aún no existen registros personales de asistencia dentro de los últimos 12 meses." />}
      </article>

      <article className="member-card member-instruction-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Docencia</p><h2>Historial de instrucciones</h2><p>Sesiones y asistencia registradas por los encargados de instrucción del Taller.</p></div><span className="member-lock-badge">Sólo consulta</span></div>
        {useMocks
          ? <><div className="member-instruction-summary"><strong>{memberPortalDemoData.instruction.length} sesiones registradas</strong><span>La asistencia se controla en Docencia / Gestión Logial</span></div><details className="member-instruction-details"><summary>Ver historial de sesiones</summary><div className="member-instruction-history" role="table" aria-label="Historial personal de instrucciones"><div className="member-instruction-row member-instruction-header" role="row"><span>Fecha</span><span>Grado</span><span>Tema</span><span>Asistencia</span><span>Encargado</span></div>{memberPortalDemoData.instruction.map(item => <div className="member-instruction-row" role="row" key={`${item.date}-${item.topic}`}><span data-label="Fecha">{item.date}</span><span data-label="Grado">{item.degree}</span><strong data-label="Tema">{item.topic}</strong><span data-label="Asistencia" className={item.attendance === 'Presente' ? 'member-instruction-status present' : item.attendance === 'Justificada' ? 'member-instruction-status excused' : 'member-instruction-status absent'}>{item.attendance}</span><span data-label="Encargado">{item.responsible}</span></div>)}</div></details></>
          : activity?.instruction && activity.instruction.history.length > 0
            ? <><div className="member-instruction-summary"><strong>{activity.instruction.total} sesiones registradas</strong><span>Historial real proveniente de Docencia / Gestión Logial</span></div><details className="member-instruction-details"><summary>Ver historial de sesiones</summary><div className="member-instruction-history" role="table" aria-label="Historial personal de instrucciones"><div className="member-instruction-row member-instruction-header" role="row"><span>Fecha</span><span>Grado</span><span>Tema</span><span>Asistencia</span><span>Encargado</span></div>{activity.instruction.history.map(item => <div className="member-instruction-row" role="row" key={item.instructionSessionId}><span data-label="Fecha">{formatShortDate(item.instructionDate)}</span><span data-label="Grado">{formatLodgeGrade(item.grade)}</span><strong data-label="Tema">{item.topic}</strong><span data-label="Asistencia" className={`member-instruction-status ${attendanceStatusClass(item.attendanceStatus)}`}>{formatAttendanceStatus(item.attendanceStatus)}</span><span data-label="Encargado">{formatResponsibleOffice(item.responsibleOffice)}</span></div>)}</div></details></>
            : <PortalPendingData text="Aún no existen sesiones de instrucción registradas para tu ficha." />}
      </article>

      <div className="member-status-stack">
        <article className="member-card member-status-card"><span className="member-status-icon">$</span><div><small>Estado de tesorería</small><strong className={(treasuryAccount?.balance ?? 0) === 0 ? 'member-success-text' : undefined}>{treasuryAccount ? ((treasuryAccount.currencies?.length??0)>1?'Saldos separados en varias monedas':formatTreasuryMoney(treasuryAccount.balance??0,treasuryAccount.currency??'CLP')+' pendiente total') : treasury.status}</strong><p>{treasuryAccount ? ((treasuryAccount.currencies?.length??0)>1?'Consulta el detalle por moneda, sin conversión.':`${formatTreasuryMoney(treasuryAccount.overdueBalance??0,treasuryAccount.currency??'CLP')} en morosidad anterior · ${formatTreasuryMoney(treasuryAccount.futurePaidAmount??0,treasuryAccount.currency??'CLP')} pagado por adelantado`) : treasury.detail}</p></div><button type="button" onClick={() => setShowTreasuryDetail(value => !value)}>{showTreasuryDetail ? 'Ocultar cartola' : 'Ver cartola'}</button></article>
        <article className="member-card member-status-card"><span className="member-status-icon">♥</span><div><small>Estado hospitalaria</small><strong className={hospitalaria.status === 'Al día' || hospitalaria.status === 'Activo' ? 'member-success-text' : undefined}>{hospitalaria.status}</strong><p>{hospitalaria.detail}</p></div><button type="button">Ver detalle</button></article>
      </div>
      <nav className="member-quick-links" aria-label="Accesos de Mi ficha">
        {onOpenCalendar && <button type="button" onClick={onOpenCalendar}>Ver mi agenda</button>}
        {onOpenNotifications && <button type="button" onClick={onOpenNotifications}>Ver mis avisos</button>}
        {onOpenLodge && <button type="button" onClick={onOpenLodge}>Tenidas de mi Taller</button>}
      </nav>

      {showTreasuryDetail && treasuryAccount && <article className="member-card member-treasury-account">
        <MemberTreasuryCredits credits={treasuryAccount.unappliedCredits} />
        <div className="member-card-title-row"><div><p className="member-card-kicker">Mi Tesorería</p><h2>Cartola personal</h2><p>La fecha de pago se conserva separada del período de la obligación.</p></div><span className="member-lock-badge">Sólo consulta</span></div>
        {(treasuryAccount.currencies?.length?treasuryAccount.currencies:[{currency:treasuryAccount.currency??'CLP',totalCharged:treasuryAccount.totalCharged??0,totalPaid:treasuryAccount.totalPaid??0,balance:treasuryAccount.balance??0,overdueBalance:treasuryAccount.overdueBalance??0,currentPeriodBalance:treasuryAccount.currentPeriodBalance??0,futurePeriodBalance:treasuryAccount.futurePeriodBalance??0,futurePaidAmount:treasuryAccount.futurePaidAmount??0}]).map(book=><div className="member-institutional-summary" key={book.currency}><h3>Libro {book.currency}</h3><MemberDatum label="Cargado histórico" value={formatTreasuryMoney(book.totalCharged,book.currency)} /><MemberDatum label="Pagado histórico" value={formatTreasuryMoney(book.totalPaid,book.currency)} /><MemberDatum label="Morosidad anterior" value={formatTreasuryMoney(book.overdueBalance,book.currency)} /><MemberDatum label="Cuotas futuras pagadas" value={formatTreasuryMoney(book.futurePaidAmount,book.currency)} /><MemberDatum label="Saldo" value={formatTreasuryMoney(book.balance,book.currency)} success={book.balance === 0} /></div>)}
        <div className="table-scroll"><table className="treasury-table"><thead><tr><th>Período cuota</th><th>Estado</th><th>Taller</th><th>Cargo</th><th>Pagado</th><th>Saldo</th><th>Comprobante / fecha real</th></tr></thead><tbody>{treasuryAccount.items.map(item => <tr key={item.chargeId}><td>{String(item.periodMonth).padStart(2, '0')}/{item.periodYear}</td><td>{memberTreasuryPeriodLabel(item.periodStatus)}</td><td>{item.organization}</td><td>{formatTreasuryMoney(item.chargedAmount,item.currency??'CLP')}</td><td>{formatTreasuryMoney(item.paidAmount,item.currency??'CLP')}</td><td>{formatTreasuryMoney(item.balance,item.currency??'CLP')}</td><td>{item.payments.length ? item.payments.map(payment => <small key={payment.id}>{payment.receiptNumber} · {formatShortDate(payment.paymentDate)} · {formatTreasuryMoney(payment.amount,payment.currency??item.currency??'CLP')}</small>) : <small>Sin pagos</small>}</td></tr>)}</tbody></table></div>
      </article>}

    </section>

    <MemberWorkPapersPanel api={documentApi} organizationId={useMocks ? 'demo-org-23' : selfProfile?.current.membership?.organizationId ?? ''} enabled={useMocks || selfProfile?.current.membership?.status === 'active'} />
  </div>
}

function MemberDatum({ label, value, success = false }: { label: string; value: string; success?: boolean }) {
  return <div className="member-datum"><small>{label}</small><strong className={success ? 'member-success-text' : undefined}>{value}</strong></div>
}

function MemberInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="member-input"><span>{label}</span><input value={value} onChange={event => onChange(event.target.value)} /></label>
}

function PortalPendingData({ text }: { text: string }) {
  return <div className="empty-state"><strong>Información institucional</strong><span>{text}</span></div>
}

function libraryAccessForDegree(degree?: number) {
  if (degree === 1) return { title: 'Acceso de 1° grado', detail: 'General · 1°' }
  if (degree === 2) return { title: 'Acceso hasta 2° grado', detail: 'General · 1° · 2°' }
  if (degree !== undefined && degree >= 3) return { title: 'Acceso hasta 3° grado', detail: 'General · 1° · 2° · 3°' }
  return { title: 'Acceso según grado vigente', detail: 'La Biblioteca mostrará sólo contenidos autorizados para tu grado e inferiores.' }
}

function formatDateOnly(value?: string | null) {
  if (!value) return 'Sin registro'
  const parts = value.split('-').map(Number)
  if (parts.length !== 3 || parts.some(Number.isNaN)) return value
  return new Intl.DateTimeFormat('es-CL', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(parts[0], parts[1] - 1, parts[2], 12)))
}

function formatShortDate(value?: string | null) {
  if (!value) return 'Sin fecha'
  const parts = value.split('-').map(Number)
  if (parts.length !== 3 || parts.some(Number.isNaN)) return value
  return new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(parts[0], parts[1] - 1, parts[2], 12))).replace('.', '')
}

function formatDegree(effectiveDegree?: number, storedDegree?: string) {
  const degree = effectiveDegree || Number.parseInt(storedDegree ?? '', 10)
  if (degree === 1) return 'Aprendiz (1°)'
  if (degree === 2) return 'Compañero (2°)'
  if (degree === 3) return 'Maestro (3°)'
  return storedDegree || 'Sin registro'
}

function formatLodgeGrade(value: string) {
  const labels: Record<string, string> = { apprentice: '1°', fellowcraft: '2°', master: '3°', all: 'Todos' }
  return labels[value] ?? value
}

function formatInstitutionalStatus(value?: string | null) {
  const labels: Record<string, string> = { active: 'Activo', inactive: 'Inactivo', voluntary_withdrawal: 'Retiro voluntario', forced_withdrawal: 'Retiro forzoso', reinstated: 'Reintegrado', deceased: 'Fallecido', workshop_transfer: 'Cambio de Taller' }
  return value ? (labels[value] ?? value) : 'Sin registro'
}

function formatRegularity(value: string | undefined, kind: 'tesorería' | 'hospitalaria') {
  if (value === 'up_to_date') return { status: 'Al día', detail: kind === 'tesorería' ? 'Regularidad financiera vigente' : 'Reposiciones y obligaciones vigentes' }
  if (value === 'delinquent' || value === 'overdue') return { status: 'Pendiente', detail: kind === 'tesorería' ? 'Existen obligaciones por regularizar' : 'Existen reposiciones u obligaciones por regularizar' }
  if (value === 'exempt') return { status: 'Exento', detail: 'Condición institucional registrada' }
  return { status: 'Sin información', detail: 'No existe una regularidad vigente registrada' }
}

function memberTreasuryPeriodLabel(value: string) {
  const labels: Record<string, string> = { overdue: 'Morosa', due: 'Vigente', partial: 'Abono parcial', paid: 'Pagada', future_due: 'Futura pendiente', advance_partial: 'Adelanto parcial', advance_paid: 'Adelantada pagada' }
  return labels[value] ?? 'Sin estado'
}

function formatAttendanceStatus(value: string) {
  const labels: Record<string, string> = { present: 'Presente', absent: 'Ausente', excused: 'Justificada' }
  return labels[value] ?? value
}

function attendanceStatusClass(value: string) {
  if (value === 'present') return 'present'
  if (value === 'excused') return 'excused'
  return 'absent'
}

function formatResponsibleOffice(value: string) {
  const labels: Record<string, string> = {
    second_warden: 'Segundo Vigilante',
    first_warden: 'Primer Vigilante',
    immediate_past_master: 'Inmediato Ex Venerable Maestro',
  }
  return labels[value] ?? value
}

