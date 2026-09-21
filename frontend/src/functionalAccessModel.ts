export type FunctionalScope = 'Taller' | 'Orden'
export type FunctionalDegree = 1 | 2 | 3 | 'all'

export interface FunctionalMenuDefinition {
  menu: string
  scope: FunctionalScope
  actions: readonly string[]
  degree: FunctionalDegree
  description: string
}

export const functionalMenuDefinitions: readonly FunctionalMenuDefinition[] = [
  { menu: 'Secretaría', scope: 'Taller', degree: 'all', actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Remitir', 'Firmar', 'Descargar', 'Exportar reportes'], description: 'Tenidas, correspondencia, insinuaciones, ceremonias, afiliaciones, incorporaciones, retiros y traslados.' },
  { menu: 'Tesorería', scope: 'Taller', degree: 'all', actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Remitir', 'Descargar', 'Exportar reportes'], description: 'Cuotas, abonos, comprobantes, libro mayor y rendición mensual.' },
  { menu: 'Hospitalaria', scope: 'Taller', degree: 'all', actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Remitir', 'Descargar', 'Exportar reportes'], description: 'Bolso, ayudas, aportes, reposiciones y rendición agregada.' },
  { menu: 'Docencia', scope: 'Taller', degree: 1, actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Descargar'], description: 'Segundo Vigilante: instrucción y asistencia de Aprendices.' },
  { menu: 'Docencia', scope: 'Taller', degree: 2, actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Descargar'], description: 'Primer Vigilante: instrucción y asistencia de Compañeros.' },
  { menu: 'Docencia', scope: 'Taller', degree: 3, actions: ['Consultar', 'Crear', 'Modificar', 'Registrar', 'Cargar archivos', 'Descargar'], description: 'Inmediato Ex-Venerable Maestro: instrucción y asistencia de Maestros.' },
  { menu: 'Gran Secretaría', scope: 'Orden', degree: 'all', actions: ['Consultar', 'Revisar', 'Observar', 'Aprobar', 'Autorizar', 'Emitir', 'Firmar', 'Descargar', 'Exportar reportes'], description: 'Planchas, decretos, comunicados, espacios y revisión institucional.' },
  { menu: 'Gran Tesorería', scope: 'Orden', degree: 'all', actions: ['Consultar', 'Revisar', 'Observar', 'Aprobar', 'Exportar reportes'], description: 'Regularidad financiera y revisión de rendiciones.' },
  { menu: 'Gran Hospitalaria', scope: 'Orden', degree: 'all', actions: ['Consultar', 'Revisar', 'Observar', 'Aprobar', 'Exportar reportes'], description: 'Regularidad de obligaciones y reposiciones hospitalarias.' },
  { menu: 'Régimen Interior', scope: 'Orden', degree: 'all', actions: ['Consultar', 'Revisar', 'Observar', 'Aprobar', 'Exportar reportes'], description: 'Control institucional y Cuadro General de la Orden.' },
] as const

export const functionalMenuNames = [...new Set(functionalMenuDefinitions.map(item => item.menu))]
export const functionalActionNames = [...new Set(functionalMenuDefinitions.flatMap(item => item.actions))]

export function docenciaDegreesForProfile(profileName: string): Array<1 | 2 | 3> {
  if (profileName === 'Segundo Vigilante') return [1]
  if (profileName === 'Primer Vigilante') return [2]
  if (profileName === 'Inmediato Ex-Venerable Maestro') return [3]
  return []
}
