import './access-screen.css'

export type AccessTone = 'info' | 'error'

export interface AccessScreenProps {
  /** Mensaje de estado bajo el título (por ejemplo «Preparando acceso…»). */
  message: string
  /** Si es 'error' el mensaje se anuncia como alerta. */
  tone?: AccessTone
  /** Acción del botón «Ingresar». Sin ella el botón queda deshabilitado (cargando o sin ingreso disponible). */
  onLogin?: () => void
  /** Texto del botón cuando está disponible. */
  loginLabel?: string
}

/** Correo de soporte para quienes no logran ingresar. */
export const accessSupportEmail = 'soporte@granlogiamixta.cl'

const logoSrc = `${import.meta.env.BASE_URL}brand/logo-glmch-reducido-v2-azul.svg`

/**
 * Pantalla de ingreso a la plataforma (antes de iniciar sesión).
 * Identidad corporativa de la Gran Logia Mixta de Chile; una sola acción visible; textos simples.
 */
export default function AccessScreen({ message, tone = 'info', onLogin, loginLabel = 'Ingresar con mi cuenta' }: AccessScreenProps) {
  const loading = !onLogin && tone === 'info'
  return <main className="access-shell" aria-labelledby="access-title">
    <section className="access-brand" aria-label="Gran Logia Mixta de Chile">
      <span className="access-logo"><img src={logoSrc} alt="Gran Logia Mixta de Chile" /></span>
      <div className="access-brand-text">
        <p className="access-kicker">Gran Logia Mixta de Chile</p>
        <h2>Proyecto Centenario</h2>
        <p className="access-tagline">Plataforma de gestión logial</p>
      </div>
      <ul className="access-points" aria-label="Qué encontrarás dentro">
        <li>Tu ficha, tus pagos y tus asistencias</li>
        <li>La agenda y los avisos de tu Taller</li>
        <li>Las herramientas de tu cargo, si tienes uno</li>
      </ul>
    </section>

    <section className="access-card">
      <p className="access-eyebrow">Acceso a la plataforma</p>
      <h1 id="access-title">Ingresar</h1>
      <p className={`access-message ${tone}`} role={tone === 'error' ? 'alert' : 'status'}>{message}</p>
      <button className="access-button" type="button" onClick={onLogin} disabled={!onLogin} aria-busy={loading}>
        {loading ? 'Preparando acceso…' : loginLabel}
      </button>
      <p className="access-help">¿No puedes ingresar? Escribe a <a href={`mailto:${accessSupportEmail}?subject=Ayuda%20para%20ingresar%20a%20la%20plataforma`}>{accessSupportEmail}</a></p>
      <p className="access-legal">Uso restringido a personas autorizadas de la Orden. Tu actividad en la plataforma queda registrada.</p>
    </section>
  </main>
}
