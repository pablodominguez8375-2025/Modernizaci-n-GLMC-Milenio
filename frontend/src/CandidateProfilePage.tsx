export const candidateCoreFields = [
  'passportPhoto',
  'names',
  'paternalSurname',
  'maternalSurname',
  'rut',
  'birthDate',
  'presentingLodge',
  'orient',
  'presenters',
  'insinuationDate',
  'processStatus',
] as const

export const candidateDemoData = {
  names: 'Tomás Ignacio',
  paternalSurname: 'Valdés',
  maternalSurname: 'Riquelme',
  rut: 'DEMO-16.543.219-X',
  birthDate: '14 de agosto de 1990',
  age: '36 años',
  nationality: 'Chilena · demo',
  civilStatus: 'Soltero · demo',
  occupation: 'Profesional · dato ficticio',
  phone: '+56 9 0000 4321',
  email: 'insinuado.demo@ejemplo.cl',
  address: 'Dirección ficticia 2345, Depto. 702',
  city: 'Santiago · demo',
  presentingLodge: 'Taller Demostrativo Nº 23',
  orient: 'Santiago',
  presenters: ['H∴ Presentante Uno · demo', 'H∴ Presentante Dos · demo'],
  insinuationDate: '12 de agosto de 2026',
  targetDegree: 'Iniciación (Aprendiz)',
  processStatus: 'En evaluación',
  processStage: 'En comisión de estudio',
  documents: [
    ['Documento de identidad', 'Completo'],
    ['Certificado de antecedentes', 'Completo'],
    ['Carta de motivación', 'Completo'],
    ['Entrevista inicial', 'Completo'],
    ['Informe de la comisión', 'En revisión'],
    ['Patrocinio de la Logia', 'Completo'],
  ],
  history: [
    ['12 ago 2026', 'Recepción de antecedentes', 'Completado'],
    ['18 ago 2026', 'Asignación a comisión', 'Completado'],
    ['25 ago 2026', 'Entrevista preliminar', 'Completado'],
    ['05 sep 2026', 'Revisión documental', 'En curso'],
    ['Por definir', 'Presentación a Logia', 'Pendiente'],
  ],
} as const

interface CandidateProfilePageProps {
  useMocks: boolean
  onBack?: () => void
}

export default function CandidateProfilePage({ useMocks, onBack }: CandidateProfilePageProps) {
  const data = candidateDemoData
  return <div className="candidate-profile-page">
    <section className="candidate-profile-heading">
      <div>
        <p className="candidate-profile-breadcrumb">Secretaría Logial <span>›</span> Insinuados <span>›</span> Ficha de insinuado</p>
        <h1>Ficha de Insinuado</h1>
        <p>Antecedentes personales, documentales y de patrocinio para proceso de iniciación.</p>
      </div>
      <div className="candidate-heading-actions">
        {onBack && <button className="candidate-secondary-button" type="button" onClick={onBack}>Volver a insinuados</button>}
        <button className="candidate-secondary-button" type="button">Generar expediente</button>
        <button className="candidate-primary-button" type="button" disabled={!useMocks}>Registrar avance</button>
      </div>
    </section>

    {!useMocks && <div className="candidate-protected-notice">Vista de producto disponible. Los datos personales reales sólo se cargarán desde el expediente interno con autorización de Secretaría y permisos de ámbito.</div>}

    <section className="candidate-profile-grid">
      <article className="candidate-product-card candidate-person-card">
        <div className="candidate-section-title"><span>▣</span><h2>Datos personales del insinuado</h2><em>Formulario configurable</em></div>
        <div className="candidate-person-layout">
          <div className="candidate-passport-photo"><img src={`${import.meta.env.BASE_URL}demo-candidate-passport.svg`} alt="Foto tipo pasaporte de demostración" /><small>Foto tipo pasaporte</small></div>
          <div>
            <h3>{useMocks ? `${data.names} ${data.paternalSurname} ${data.maternalSurname}` : 'Insinuado autorizado'}</h3>
            <div className="candidate-fields-grid">
              <CandidateField label="Nombres" value={useMocks ? data.names : 'Protegido'} />
              <CandidateField label="Apellido paterno" value={useMocks ? data.paternalSurname : 'Protegido'} />
              <CandidateField label="Apellido materno" value={useMocks ? data.maternalSurname : 'Protegido'} />
              <CandidateField label="RUT / ID" value={useMocks ? data.rut : 'Protegido'} />
              <CandidateField label="Fecha de nacimiento" value={useMocks ? data.birthDate : 'Protegido'} />
              <CandidateField label="Nacionalidad" value={useMocks ? data.nationality : 'Protegido'} />
              <CandidateField label="Estado civil" value={useMocks ? data.civilStatus : 'Protegido'} />
              <CandidateField label="Profesión u oficio" value={useMocks ? data.occupation : 'Protegido'} />
              <CandidateField label="Teléfono" value={useMocks ? data.phone : 'Protegido'} />
              <CandidateField label="Correo" value={useMocks ? data.email : 'Protegido'} />
              <CandidateField label="Dirección" value={useMocks ? data.address : 'Protegido'} />
              <CandidateField label="Ciudad" value={useMocks ? data.city : 'Protegido'} />
            </div>
          </div>
        </div>
      </article>

      <article className="candidate-product-card candidate-lodge-card">
        <div className="candidate-section-title"><span>⌂</span><h2>Datos logiales y de presentación</h2></div>
        <div className="candidate-lodge-hero"><span className="candidate-lodge-seal">M</span><div><small>Logia que presenta al insinuado</small><strong>{useMocks ? data.presentingLodge : 'Según expediente autorizado'}</strong><span>{useMocks ? data.orient : 'Ámbito protegido'}</span></div></div>
        <div className="candidate-lodge-fields">
          <CandidateField label="Oriente" value={useMocks ? data.orient : 'Protegido'} />
          <CandidateField label="Fecha de insinuación" value={useMocks ? data.insinuationDate : 'Protegido'} />
          <CandidateField label="Grado objetivo" value={data.targetDegree} />
          <CandidateField label="Estado del proceso" value={data.processStatus} status />
        </div>
        <div className="candidate-presenters"><small>Patrocinantes / Presentantes</small>{data.presenters.map(value => <strong key={value}>{useMocks ? value : 'Presentante autorizado'}</strong>)}</div>
        <div className="candidate-process-banner"><span className="candidate-blue-dot" /><div><strong>{data.processStatus}</strong><small>{data.processStage}</small></div></div>
      </article>

      <article className="candidate-product-card candidate-doc-card">
        <div className="candidate-section-title"><span>▤</span><h2>Documentación requerida</h2></div>
        <div className="candidate-doc-list">{data.documents.map(([label, status]) => <div key={label}><span className="candidate-doc-icon">▧</span><strong>{label}</strong><em className={status === 'Completo' ? 'complete' : 'review'}>{status}</em></div>)}</div>
        <button className="candidate-wide-button" type="button">Ver todos los documentos</button>
      </article>

      <aside className="candidate-quote-panel"><span>“</span><p>La verdadera iniciación comienza cuando el ser humano decide trabajar sobre sí mismo.</p><i /><strong>Proyecto Milenio</strong><small>Libertad · Igualdad · Fraternidad</small></aside>
    </section>

    <section className="candidate-middle-grid">
      <article className="candidate-product-card candidate-observation-card">
        <div className="candidate-section-title"><span>●</span><h2>Resumen de la entrevista / observaciones</h2></div>
        <p>Registro demostrativo: la entrevista evidencia interés por el conocimiento, el servicio y el perfeccionamiento personal. Se recomienda continuar con la etapa de revisión documental.</p>
        <button className="candidate-inline-link" type="button">Ver entrevista completa →</button>
      </article>
      <aside className="candidate-product-card candidate-summary-card">
        <div className="candidate-section-title"><span>▦</span><h2>Resumen del insinuado</h2></div>
        <CandidateSummary label="Edad" value={useMocks ? data.age : 'Protegido'} />
        <CandidateSummary label="Antigüedad del trámite" value="29 días" />
        <CandidateSummary label="Documentos completos" value="83%" progress={83} />
        <CandidateSummary label="Estado general" value={data.processStatus} status />
        <CandidateSummary label="Próxima acción" value="Revisión de informe" />
      </aside>
    </section>

    <section className="candidate-product-card candidate-history-card">
      <div className="candidate-section-title"><span>↻</span><h2>Historial del proceso de iniciación</h2><em>Auditado y no destructivo</em></div>
      <div className="candidate-history-list">{data.history.map(([date, action, status], index) => <div key={`${date}-${action}`}><span className={`candidate-history-node ${status === 'Completado' ? 'done' : status === 'En curso' ? 'current' : ''}`}>{status === 'Completado' ? '✓' : index + 1}</span><time>{date}</time><strong>{action}</strong><p>{historyDescription(action)}</p><em>{status}</em></div>)}</div>
    </section>
  </div>
}

function CandidateField({ label, value, status = false }: { label: string; value: string; status?: boolean }) {
  return <div className="candidate-field"><small>{label}</small><strong className={status ? 'candidate-status-text' : undefined}>{value}</strong></div>
}

function CandidateSummary({ label, value, progress, status = false }: { label: string; value: string; progress?: number; status?: boolean }) {
  return <div className="candidate-summary-row"><small>{label}</small><strong className={status ? 'candidate-status-text' : undefined}>{value}</strong>{progress !== undefined && <div className="candidate-summary-progress"><span style={{ width: `${progress}%` }} /></div>}</div>
}

function historyDescription(action: string) {
  if (action.includes('Recepción')) return 'Se recibe la solicitud y documentación inicial del insinuado.'
  if (action.includes('Asignación')) return 'Se designa la comisión responsable del estudio y seguimiento.'
  if (action.includes('Entrevista')) return 'Se realiza la entrevista personal y se registran observaciones.'
  if (action.includes('Revisión')) return 'La comisión revisa informe, documentos y antecedentes disponibles.'
  return 'Se programará la presentación del informe cuando corresponda.'
}
