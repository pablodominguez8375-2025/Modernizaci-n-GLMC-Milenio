/** Avance de la ficha de insinuado: qué secciones tienen sus datos clave y cuáles faltan. */
export type IntakeSectionId = 'personal' | 'work' | 'lodge' | 'photo'

export interface IntakeProgressInput {
  form: {
    firstNames?: string | null
    paternalSurname?: string | null
    rutOrInstitutionalId?: string | null
    birthDate?: string | null
    phone?: string | null
    email?: string | null
    occupation?: string | null
    insinuationDate?: string | null
  }
  presentersText: string
  sponsorRegistered: boolean
  hasPhoto: boolean
}

export interface IntakeSectionStatus {
  id: IntakeSectionId
  label: string
  missing: string[]
  complete: boolean
}

const filled = (value: string | null | undefined) => (value ?? '').trim().length > 0

export function intakeProgress(input: IntakeProgressInput): { sections: IntakeSectionStatus[]; done: number; total: number; firstPending: IntakeSectionId | null } {
  const { form } = input
  const missing = (checks: [boolean, string][]) => checks.filter(([ok]) => !ok).map(([, label]) => label)
  const sections: IntakeSectionStatus[] = ([
    ['personal', 'Datos personales y de contacto', missing([
      [filled(form.firstNames), 'nombres'], [filled(form.paternalSurname), 'apellido paterno'],
      [filled(form.rutOrInstitutionalId), 'RUT o ID'], [filled(form.birthDate), 'fecha de nacimiento'],
      [filled(form.phone) || filled(form.email), 'teléfono o correo'],
    ])],
    ['work', 'Antecedentes laborales', missing([[filled(form.occupation), 'actividad, profesión u oficio']])],
    ['lodge', 'Presentación logial y antecedentes', missing([
      [filled(form.insinuationDate), 'fecha de insinuación'], [filled(input.presentersText), 'presentantes'],
      [input.sponsorRegistered, 'patrocinio formal'],
    ])],
    ['photo', 'Fotografía tipo pasaporte', missing([[input.hasPhoto, 'fotografía']])],
  ] as [IntakeSectionId, string, string[]][]).map(([id, label, gaps]) => ({ id, label, missing: gaps, complete: gaps.length === 0 }))
  const done = sections.filter(section => section.complete).length
  return { sections, done, total: sections.length, firstPending: sections.find(section => !section.complete)?.id ?? null }
}
