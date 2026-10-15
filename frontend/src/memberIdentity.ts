import type { MembershipApiClient } from './api/membershipApi'
import { officeLabel } from './officeLabels'

/** «Tu situación» del Inicio: Taller, grado y cargo vigente del hermano (solo lo que le corresponde ver de sí mismo). */
export interface MemberIdentity { lodge: string | null; degree: string | null; office: string | null }

export function degreeLabel(effectiveDegree?: number | null, storedDegree?: string | null): string | null {
  const value = effectiveDegree && effectiveDegree > 0 ? effectiveDegree : Number.parseInt(storedDegree ?? '', 10)
  if (value === 1) return 'Aprendiz (1°)'
  if (value === 2) return 'Compañero (2°)'
  if (value >= 3) return 'Maestro (3°)'
  return null
}

export async function loadMemberIdentity(api: MembershipApiClient, today = new Date().toISOString().slice(0, 10)): Promise<MemberIdentity | null> {
  const [profile, offices] = await Promise.allSettled([api.getSelfProfile(), api.getOwnOffices()])
  if (profile.status !== 'fulfilled') return null
  const membership = profile.value.current.membership
  const current = offices.status === 'fulfilled' ? offices.value.items.find(item => !item.hasta || item.hasta >= today) : undefined
  return {
    lodge: membership ? organizationLabel(membership.organization, membership.organizationNumber) : null,
    degree: degreeLabel(profile.value.current.effectiveDegree, profile.value.current.degree?.degree),
    office: current ? officeLabel(current.cargo) : null,
  }
}

function organizationLabel(name: string, number: string | null) {
  return number && !name.includes(number) ? `${name} N° ${number}` : name
}
