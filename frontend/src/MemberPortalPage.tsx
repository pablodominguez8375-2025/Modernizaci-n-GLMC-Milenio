import { useState } from 'react'
import type { SessionProfile } from './api/pmgmApi'

interface MemberPortalPageProps {
  profile: SessionProfile | null
  useMocks: boolean
  onOpenCalendar?: () => void
  onOpenNotifications?: () => void
  onOpenLibrary?: () => void
  onOpenLodge?: () => void
}

export const editableMemberFields = ['email', 'phone', 'city', 'address'] as const

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
    { label: 'Ritual y simbolismo', progress: 100 },
    { label: 'Historia de la Orden', progress: 80 },
    { label: 'Ética y filosofía', progress: 70 },
    { label: 'Trabajo en Taller', progress: 90 },
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

export default function MemberPortalPage({ profile, useMocks, onOpenCalendar, onOpenNotifications, onOpenLibrary, onOpenLodge }: MemberPortalPageProps) {
  const [editing, setEditing] = useState(false)
  const [personal, setPersonal] = useState({ ...memberPortalDemoData.personal })
  const fullName = useMocks ? memberPortalDemoData.fullName : (profile?.displayName ?? 'Hermano')
  const personalValue = (value: string) => useMocks ? value : 'Disponible al integrar expediente personal'

  return <div className="member-portal">
    <section className="member-portal-heading">
      <div>
        <p className="member-breadcrumb">Portal del Hermano <span>›</span> Mi ficha</p>
        <h1>Mi ficha</h1>
        <p>Tu información personal, masónica y de participación en un solo lugar.</p>
      </div>
      <button className="member-primary-action" type="button" disabled={!useMocks} onClick={() => setEditing(value => !value)}>{editing ? 'Cerrar edición' : 'Editar mis datos'}</button>
    </section>

    {!useMocks && <div className="member-live-notice">La vista operacional ya está disponible. La persistencia de los datos personales se conectará al expediente institucional en la siguiente integración de backend.</div>}

    <section className="member-profile-grid">
      <article className="member-card member-identity-card">
        <div className="member-avatar-wrap"><img className="member-avatar-photo" src={`${import.meta.env.BASE_URL}demo-member-avatar.svg`} alt="Avatar demostrativo" /></div>
        <div className="member-identity-content">
          <p className="member-card-kicker">Ficha personal</p>
          <h2>{fullName}</h2>
          <p className="member-display-name">{useMocks ? memberPortalDemoData.displayName : 'Ficha personal autenticada'}</p>
          <div className="member-data-grid">
            <MemberDatum label="Identificador" value={useMocks ? memberPortalDemoData.memberId : 'Protegido'} />
            <MemberDatum label="Nacimiento" value={useMocks ? memberPortalDemoData.birthDate : 'Protegido'} />
            <MemberDatum label="Correo" value={personalValue(personal.email)} />
            <MemberDatum label="Teléfono" value={personalValue(personal.phone)} />
            <MemberDatum label="Profesión" value={useMocks ? memberPortalDemoData.profession : 'Protegido'} />
            <MemberDatum label="Ciudad" value={personalValue(personal.city)} />
          </div>
        </div>
      </article>

      <article className="member-card member-institutional-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Datos masónicos</p><h2>Información institucional</h2></div><span className="member-lock-badge">Sólo lectura</span></div>
        <div className="member-lodge-line"><span className="member-lodge-seal">M</span><div><small>Taller</small><strong>{useMocks ? memberPortalDemoData.institutional.lodge : 'Según expediente institucional'}</strong><span>{useMocks ? memberPortalDemoData.institutional.orient : 'Ámbito autenticado'}</span></div></div>
        <div className="member-institutional-summary">
          <MemberDatum label="Grado" value={useMocks ? memberPortalDemoData.institutional.degree : 'Según expediente'} />
          <MemberDatum label="Estado" value={useMocks ? memberPortalDemoData.institutional.status : 'Según expediente'} success />
          <MemberDatum label="Iniciación" value={useMocks ? memberPortalDemoData.institutional.initiation : 'Según expediente'} />
          <MemberDatum label="Aumento de salario" value={useMocks ? memberPortalDemoData.institutional.wageIncrease : 'Según expediente'} />
          <MemberDatum label="Exaltación" value={useMocks ? memberPortalDemoData.institutional.exaltation : 'Según expediente'} />
        </div>
      </article>

      <aside className="member-quote-card"><span className="member-quote-mark">“</span><p>Que nuestras acciones sean testimonio de los principios que profesamos.</p><span className="member-quote-rule" /><strong>Proyecto Milenio</strong><small>Libertad · Igualdad · Fraternidad</small></aside>
    </section>

    {editing && <section className="member-card member-edit-card">
      <div className="member-card-title-row"><div><p className="member-card-kicker">Autogestión</p><h2>Datos personales editables</h2><p>Los datos institucionales permanecen protegidos.</p></div><span className="member-demo-chip">Demostración local</span></div>
      <div className="member-edit-grid">
        <MemberInput label="Correo" value={personal.email} onChange={value => setPersonal(current => ({ ...current, email: value }))} />
        <MemberInput label="Teléfono" value={personal.phone} onChange={value => setPersonal(current => ({ ...current, phone: value }))} />
        <MemberInput label="Ciudad" value={personal.city} onChange={value => setPersonal(current => ({ ...current, city: value }))} />
        <MemberInput label="Domicilio" value={personal.address} onChange={value => setPersonal(current => ({ ...current, address: value }))} />
      </div>
      <div className="member-edit-actions"><button type="button" onClick={() => setEditing(false)}>Guardar cambios de demostración</button><small>En producción esta acción quedará auditada y persistida por el servicio institucional.</small></div>
    </section>}

    <section className="member-insight-grid">
      <article className="member-card member-attendance-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Participación</p><h2>Asistencia a tenidas</h2><p>Últimos 12 meses</p></div></div>
        <div className="member-attendance-layout"><div className="member-ring" style={{ '--member-progress': `${memberPortalDemoData.attendance.percentage}%` } as React.CSSProperties}><div><strong>{memberPortalDemoData.attendance.percentage}%</strong><small>{memberPortalDemoData.attendance.attended} de {memberPortalDemoData.attendance.total}</small></div></div><ul><li><span className="dot success" />Asistidas <strong>{memberPortalDemoData.attendance.attended}</strong></li><li><span className="dot danger" />Inasistencias <strong>{memberPortalDemoData.attendance.absent}</strong></li><li><span className="dot info" />Justificadas <strong>{memberPortalDemoData.attendance.excused}</strong></li></ul></div>
      </article>

      <article className="member-card member-instruction-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Docencia</p><h2>Progreso de instrucción</h2><p>Plan formativo del grado</p></div></div>
        <div className="member-progress-list">{memberPortalDemoData.instruction.map(item => <div key={item.label}><div><span>{item.label}</span><strong>{item.progress}%</strong></div><div className="member-progress-track"><span style={{ width: `${item.progress}%` }} /></div></div>)}</div>
      </article>

      <div className="member-status-stack">
        <article className="member-card member-status-card"><span className="member-status-icon">$</span><div><small>Estado de tesorería</small><strong className="member-success-text">{memberPortalDemoData.treasury.status}</strong><p>{memberPortalDemoData.treasury.detail}</p></div><button type="button">Ver detalle</button></article>
        <article className="member-card member-status-card"><span className="member-status-icon">♥</span><div><small>Estado hospitalaria</small><strong className="member-success-text">{memberPortalDemoData.hospitalaria.status}</strong><p>{memberPortalDemoData.hospitalaria.detail}</p></div><button type="button">Ver detalle</button></article>
      </div>

      <article className="member-card member-calendar-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Agenda</p><h2>Septiembre 2026</h2></div><button className="member-inline-button" type="button" onClick={onOpenCalendar}>Ver calendario</button></div>
        <div className="member-calendar-week"><span>Lu</span><span>Ma</span><span>Mi</span><span>Ju</span><span>Vi</span><span>Sá</span><span>Do</span></div>
        <div className="member-calendar-days">{Array.from({ length: 30 }, (_, index) => index + 1).map(day => <span key={day} className={day === 12 ? 'meeting' : day === 26 ? 'instruction' : ''}>{day}</span>)}</div>
        <div className="member-calendar-legend"><span><i className="meeting" />Tenida</span><span><i className="instruction" />Instrucción</span></div>
      </article>
    </section>

    <section className="member-lower-grid">
      <article className="member-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Agenda personal</p><h2>Próximas tenidas</h2></div><button className="member-inline-button" type="button" onClick={onOpenLodge}>Ver Taller</button></div>
        <div className="member-meeting-list">{memberPortalDemoData.meetings.map(meeting => <div key={`${meeting.date}-${meeting.title}`}><div className="member-date-block"><strong>{meeting.date}</strong><small>{meeting.time}</small></div><div><strong>{meeting.title}</strong><span>{meeting.lodge}</span></div><span className="member-state-pill">{meeting.status}</span></div>)}</div>
      </article>
      <article className="member-card">
        <div className="member-card-title-row"><div><p className="member-card-kicker">Centro de avisos</p><h2>Notificaciones recientes</h2></div><button className="member-inline-button" type="button" onClick={onOpenNotifications}>Ver todas</button></div>
        <div className="member-notification-list">{memberPortalDemoData.notifications.map(item => <div key={item.title}><span className="member-notification-dot" /><div><strong>{item.title}</strong><p>{item.detail}</p></div><small>{item.age}</small></div>)}</div>
      </article>
      <aside className="member-card member-library-callout"><span className="member-callout-icon">▥</span><p className="member-card-kicker">Según tu grado</p><h2>Biblioteca Virtual</h2><p>Accede solamente al material autorizado para tu grado institucional vigente.</p><button type="button" onClick={onOpenLibrary}>Abrir Biblioteca</button></aside>
    </section>
  </div>
}

function MemberDatum({ label, value, success = false }: { label: string; value: string; success?: boolean }) {
  return <div className="member-datum"><small>{label}</small><strong className={success ? 'member-success-text' : undefined}>{value}</strong></div>
}

function MemberInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="member-input"><span>{label}</span><input value={value} onChange={event => onChange(event.target.value)} /></label>
}
