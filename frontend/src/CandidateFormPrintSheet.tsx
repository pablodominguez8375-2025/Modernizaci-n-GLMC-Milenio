import { createPortal } from 'react-dom'
import type { CandidateIntakeProfile } from './api/candidateIntakeApi'

interface CandidateFormPrintSheetProps {
  profile: CandidateIntakeProfile
  photoSrc: string | null
  originText: string
  sponsorText: string | null
}

const DASH = '—'

function dateOnly(value: string | null | undefined) {
  if (!value) return DASH
  const [year, month, day] = value.slice(0, 10).split('-')
  return year && month && day ? `${day}-${month}-${year}` : value
}

function printedAt() {
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'long', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date())
}

function Row({ label, value }: { label: string; value: string | null | undefined }) {
  return <div className="candidate-print-row"><dt>{label}</dt><dd>{value && value.trim() ? value : DASH}</dd></div>
}

/**
 * Hoja de la ficha del insinuado para imprimir. Solo se ve al imprimir (candidate-print.css):
 * en pantalla no existe visualmente y al imprimir reemplaza al resto de la página.
 * No incluye las observaciones internas ni el resumen de entrevista (son reservados).
 */
export function CandidateFormPrintContent({ profile, photoSrc, originText, sponsorText }: CandidateFormPrintSheetProps) {
  const fullName = [profile.firstNames, profile.paternalSurname, profile.maternalSurname].filter(Boolean).join(' ')
  const hasNumber = !profile.workshopNumber || profile.workshopName.includes(profile.workshopNumber)
  const workshop = hasNumber ? profile.workshopName : `${profile.workshopName} N° ${profile.workshopNumber}`
  return (
    <section className="candidate-print-sheet" aria-hidden="true">
      <header className="candidate-print-head">
        <div>
          <p>Gran Logia Mixta de Chile</p>
          <h1>Ficha de Insinuado</h1>
          <p>{workshop}</p>
        </div>
        <div className="candidate-print-photo">
          {photoSrc ? <img src={photoSrc} alt="" /> : <span>Fotografía tipo pasaporte</span>}
        </div>
      </header>

      <h2>Datos personales</h2>
      <dl className="candidate-print-grid">
        <Row label="Nombre completo" value={fullName} />
        <Row label="RUT o identificación" value={profile.rutOrInstitutionalId} />
        <Row label="Fecha de nacimiento" value={profile.birthDate ? dateOnly(profile.birthDate) : null} />
        <Row label="Nacionalidad" value={profile.nationality} />
        <Row label="Estado civil" value={profile.civilStatus} />
      </dl>

      <h2>Contacto</h2>
      <dl className="candidate-print-grid">
        <Row label="Teléfono" value={profile.phone} />
        <Row label="Correo" value={profile.email} />
        <Row label="Domicilio" value={profile.address} />
        <Row label="Ciudad" value={profile.city} />
      </dl>

      <h2>Antecedentes laborales</h2>
      <dl className="candidate-print-grid">
        <Row label="Ocupación" value={profile.occupation} />
        <Row label="Empleador" value={profile.employerName} />
        <Row label="Cargo" value={profile.workPosition} />
        <Row label="Dirección laboral" value={profile.workAddress} />
        <Row label="Teléfono laboral" value={profile.workPhone} />
      </dl>

      <h2>Insinuación</h2>
      <dl className="candidate-print-grid">
        <Row label="Taller" value={workshop} />
        <Row label="Oriente" value={profile.orient} />
        <Row label="Procedencia" value={originText} />
        <Row label="Patrocinante" value={sponsorText} />
        <Row label="Presentadores" value={profile.presenters.length ? profile.presenters.join(', ') : null} />
        <Row label="Fecha de insinuación" value={dateOnly(profile.insinuationDate)} />
        <Row label="Presentación en 1.er grado" value={profile.firstDegreePresentationDate ? dateOnly(profile.firstDegreePresentationDate) : null} />
        <Row label="Secretario(a) responsable" value={profile.responsibleSecretaryName} />
      </dl>

      <div className="candidate-print-sign">
        <div><span /> Firma Secretaría del Taller</div>
      </div>
      <footer className="candidate-print-foot">
        Documento privado con antecedentes personales (Ley 21.719). Debe permanecer bajo custodia institucional. Impreso el {printedAt()}
      </footer>
    </section>
  )
}

export default function CandidateFormPrintSheet(props: CandidateFormPrintSheetProps) {
  return createPortal(<CandidateFormPrintContent {...props} />, document.body)
}
