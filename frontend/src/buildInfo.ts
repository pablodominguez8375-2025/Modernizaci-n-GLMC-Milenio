/** «Acerca de esta versión»: ayuda a quien hace pruebas a saber qué corte está viendo (demo o sistema real/QA). */
export interface BuildInfoInput { demo: boolean; versionLabel: string; sha?: string; environment?: string }
export interface BuildInfoRow { label: string; value: string }

export function describeBuild({ demo, versionLabel, sha, environment }: BuildInfoInput): BuildInfoRow[] {
  const cut = (sha ?? '').trim().slice(0, 7)
  const env = (environment ?? '').trim()
  return [
    { label: 'Versión', value: versionLabel },
    { label: 'Ambiente', value: demo ? 'Demostración pública con datos ficticios (no es el QA físico)' : env ? `Sistema conectado · ${env}` : 'Sistema conectado (el ambiente no está indicado en esta compilación)' },
    { label: 'Corte', value: cut || 'No indicado en esta compilación' },
  ]
}
