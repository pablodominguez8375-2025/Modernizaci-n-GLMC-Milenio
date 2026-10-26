import type { SupportLodgeDataHealthItem, SupportLodgeProbeItem } from './api/pmgmApi'

export type HealthLight = 'ok' | 'attention'
export interface ResourceHealth {
  resource: string
  label: string
  light: HealthLight
  /** Qué falta, en palabras simples; vacío si todo está en orden. */
  problems: string[]
}

const labels: Record<string, string> = { tenidas: 'Tenidas', instrucciones: 'Instrucciones', miembros: 'Hermanos del Taller' }
const order = ['tenidas', 'instrucciones', 'miembros']

/** Agrupa las relaciones revisadas por recurso del Taller y traduce cada falla a un texto claro. */
export function summarizeLodgeHealth(items: SupportLodgeDataHealthItem[]): ResourceHealth[] {
  const groups = new Map<string, SupportLodgeDataHealthItem[]>()
  for (const item of items) groups.set(item.resource, [...(groups.get(item.resource) ?? []), item])
  return [...groups.entries()]
    .sort(([a], [b]) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99))
    .map(([resource, rows]) => {
      const problems: string[] = []
      for (const row of rows) {
        const table = row.relation.replace(/^core\./, '')
        if (!row.present) problems.push(`Falta la tabla ${table}`)
        else {
          if (!row.schemaUsable) problems.push('La API no puede usar el esquema de datos')
          if (!row.selectGranted) problems.push(`La API no tiene permiso de lectura en ${table}`)
          if (!row.requiredColumnsPresent) problems.push(`Faltan columnas en ${table}`)
        }
      }
      return { resource, label: labels[resource] ?? resource, light: problems.length === 0 ? 'ok' : 'attention', problems: [...new Set(problems)] }
    })
}

/** Mensaje claro según el estado HTTP; nunca muestra detalles internos. */
export function lodgeHealthErrorMessage(status: number | undefined): string {
  if (status === 403) return 'Su cuenta no tiene permiso para ver este diagnóstico.'
  if (status === 503) return 'El diagnóstico no está disponible ahora. Intente nuevamente en unos minutos.'
  return 'No fue posible verificar el estado de los datos de Talleres.'
}

export interface ProbeLine { resource: string; label: string; ok: boolean; text: string }

/** Traduce las tres lecturas de prueba a líneas simples; una lectura que falla se explica sin detalles internos. */
export function summarizeLodgeProbe(items: SupportLodgeProbeItem[]): ProbeLine[] {
  return [...items]
    .sort((a, b) => (order.indexOf(a.resource) + 1 || 99) - (order.indexOf(b.resource) + 1 || 99))
    .map(item => ({
      resource: item.resource,
      label: labels[item.resource] ?? item.resource,
      ok: item.result === 'ok',
      text: item.result === 'ok' ? 'La lectura respondió bien' : 'La lectura falló: revise el registro del servidor con el ID de correlación',
    }))
}

export function lodgeProbeErrorMessage(status: number | undefined): string {
  if (status === 404) return 'Ese Taller no existe o no es un Taller de la Orden.'
  return lodgeHealthErrorMessage(status)
}
