import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'

type Right = { icon: InstitutionalIconName; title: string; text: string }

const rights: Right[] = [
  { icon: 'member', title: 'Saber qué datos tenemos', text: 'Puedes pedir una copia de los datos personales que la Gran Logia guarda sobre ti.' },
  { icon: 'check', title: 'Corregir un dato', text: 'Si tu nombre, contacto u otro dato está mal o desactualizado, puedes pedir que se corrija.' },
  { icon: 'archive', title: 'Pedir que se elimine', text: 'Puedes pedir que borremos un dato cuando ya no sea necesario, salvo lo que la Orden deba conservar por norma o por su registro histórico.' },
  { icon: 'shield', title: 'Oponerte o limitar su uso', text: 'Puedes oponerte a un uso que no sea indispensable o pedir que se suspenda mientras se revisa tu caso.' },
  { icon: 'documents', title: 'Llevar tus datos contigo', text: 'Puedes pedir tus datos en un archivo que puedas guardar o entregar a otra organización.' },
]

const dataGroups: Array<{ title: string; text: string }> = [
  { title: 'Qué datos guardamos', text: 'Datos de tu ficha (nombre, contacto, grado, Taller), tu asistencia, tus pagos y los documentos que tú o tu Taller suben.' },
  { title: 'Para qué los usamos', text: 'Solo para la vida de la Orden: llevar el Cuadro, citar a tenidas, cobrar cuotas, emitir certificados y cumplir normas.' },
  { title: 'Quién puede verlos', text: 'Cada persona ve únicamente lo que su cargo necesita. Tu Taller ve tu información de Taller; las oficialías de la Gran Logia ven lo propio de su función.' },
  { title: 'Acceso de soporte', text: 'Solo con fines de soporte técnico, un administrador autorizado de la plataforma puede ver tu cuenta tal como tú la ves. Se le da permiso solo por un tiempo predeterminado (máximo 30 minutos), en modo solo lectura: no puede cambiar tus datos ni conocer tu clave. Cada acceso queda registrado con su motivo.' },
  { title: 'Cómo los cuidamos', text: 'Acceso con tu cuenta personal, registro de quién consulta o cambia datos, documentos revisados con antivirus y copias de respaldo.' },
]

/** Hallazgo 4 de la auditoría 13-10-2026 · Ley 21.719: política de privacidad en lenguaje simple y derechos del titular. */
export default function PrivacyPage({ contactEmail, onOpenProfile }: { contactEmail: string; onOpenProfile?: () => void }) {
  return <section className="privacy-page" aria-labelledby="privacidad-titulo">
    <h2 id="privacidad-titulo">Privacidad y mis datos</h2>
    <p>Tus datos son tuyos. Aquí te explicamos, en simple, cómo los cuidamos y qué puedes pedir.</p>
    <div className="privacy-grid">
      {dataGroups.map(group => <article key={group.title} className="privacy-card">
        <h3>{group.title}</h3>
        <p>{group.text}</p>
      </article>)}
    </div>
    <h3 className="privacy-subtitle">Tus derechos</h3>
    <div className="privacy-grid">
      {rights.map(right => <article key={right.title} className="privacy-card privacy-right">
        <span className="workbench-icon" aria-hidden="true"><InstitutionalIcon name={right.icon} size={26} /></span>
        <div><h4>{right.title}</h4><p>{right.text}</p></div>
      </article>)}
    </div>
    <div className="privacy-how" role="note">
      <strong>¿Cómo pedirlo?</strong>
      <p>Escríbenos a <a href={`mailto:${contactEmail}?subject=Solicitud%20sobre%20mis%20datos%20personales`}>{contactEmail}</a> indicando qué quieres pedir. Te responderemos dentro del plazo que fija la ley. Muy pronto podrás hacerlo desde esta misma pantalla.</p>
      {onOpenProfile && <button type="button" className="secondary" onClick={onOpenProfile}>Revisar mis datos en Mi ficha</button>}
    </div>
  </section>
}
