import { describe, expect, it } from 'vitest'
import { describeBuild } from './buildInfo'

describe('Acerca de esta versión', () => {
  it('distingue la demo del sistema conectado y acorta el corte', () => {
    const demo = describeBuild({ demo: true, versionLabel: 'UI QA v0.190', sha: 'a184936d78e6e9ee0fa85f7be060b13a5280710e' })
    expect(demo[1].value).toContain('no es el QA físico')
    expect(demo[2].value).toBe('a184936')
    const real = describeBuild({ demo: false, versionLabel: 'API v1.0.0', environment: 'QA glmdch' })
    expect(real[1].value).toBe('Sistema conectado · QA glmdch')
    expect(real[2].value).toBe('No indicado en esta compilación')
  })
})
