import { describe, expect, it } from 'vitest'
import { BootstrapApiClient } from './api/bootstrapApi'
import { describeInitialLoadStatus, initialLoadStatusErrorMessage } from './initialLoadStatus'

const base = { version: 1, canApply: false, writesPerformed: 0, code: 'x' }
describe('#694 estado de la carga inicial', () => {
  it('describe cada estado en palabras simples', () => {
    expect(describeInitialLoadStatus({ ...base, status: 'not_opened' }).label).toBe('Carga no abierta')
    expect(describeInitialLoadStatus({ ...base, status: 'open' }).tone).toBe('open')
    expect(describeInitialLoadStatus({ ...base, status: 'closed' }).detail).toContain('Gran Secretaría')
  })
  it('sin dato o no disponible no asume que esté abierta', () => {
    expect(describeInitialLoadStatus(null).tone).toBe('warn')
    expect(describeInitialLoadStatus({ ...base, status: 'unavailable' }).detail).toContain('No se asume')
  })
  it('403 explica que falta registrar la cuenta Máster, sin detalles internos', () => {
    expect(initialLoadStatusErrorMessage('Sólo el Superadmin de plataforma puede ejecutar la configuración inicial.')).toContain('cuenta Máster')
    expect(initialLoadStatusErrorMessage('La API respondió 500')).not.toMatch(/500|sql/i)
  })
  it('la demo devuelve not_opened y nunca permite aplicar', async () => {
    const s = await new BootstrapApiClient({ useMocks: true }).initialLoadStatus()
    expect(s).toMatchObject({ status: 'not_opened', canApply: false, writesPerformed: 0 })
  })
})
