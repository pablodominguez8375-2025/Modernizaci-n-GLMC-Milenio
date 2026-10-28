import { describe, expect, it } from 'vitest'
import { UserAccountsDemo, type AccountUser } from '../api/userAccounts'
import { LIFECYCLE_LABELS, availableLifecycleActions, lifecycleConfirmationValid, lifecycleErrorMessage, lifecycleReasonValid } from './accountLifecycle'

const seed: AccountUser[] = [
  { subject: 'demo:brother', name: 'Hermano Demostrativo', email: 'h@ejemplo.cl', enabled: true, kind: 'member' },
  { subject: 'demo:supportUser', name: 'Soporte Demostrativo', email: 's@ejemplo.cl', enabled: true, kind: 'superuser' },
]
const build = (master = true) => new UserAccountsDemo([], () => true, () => master, structuredClone(seed), () => 'demo:systemAdmin', () => false)
const REASON = 'Motivo de prueba válido'

describe('Ciclo de vida de cuentas (#770 parte 2)', () => {
  it('admin nunca; qa.admin solo eliminar; otras protegidas nada; el resto activar/desactivar y eliminar', () => {
    expect(availableLifecycleActions({ username: 'admin', enabled: true, protected: true })).toEqual([])
    expect(availableLifecycleActions({ username: 'qa.admin', enabled: true, protected: true })).toEqual(['delete-permanently'])
    expect(availableLifecycleActions({ username: 'bootstrap.inicial', enabled: false, protected: true })).toEqual([])
    expect(availableLifecycleActions({ username: 'h', enabled: true, protected: false })).toEqual(['deactivate', 'delete-permanently'])
    expect(availableLifecycleActions({ username: 'h', enabled: false, protected: false })).toEqual(['activate', 'delete-permanently'])
    expect(LIFECYCLE_LABELS['delete-permanently']).toBe('Eliminar definitivamente')
  })

  it('motivo de 10 a 200 caracteres y confirmación literal para eliminar', () => {
    expect(lifecycleReasonValid('corto')).toBe(false)
    expect(lifecycleReasonValid('x'.repeat(10))).toBe(true)
    expect(lifecycleReasonValid('x'.repeat(201))).toBe(false)
    expect(lifecycleConfirmationValid('delete-permanently', 'qa.admin', 'qa.admin')).toBe(true)
    expect(lifecycleConfirmationValid('delete-permanently', 'qa.admin', 'QA.admin')).toBe(false)
    expect(lifecycleConfirmationValid('delete-permanently', 'qa.admin', 'qa.admin ')).toBe(false)
    expect(lifecycleConfirmationValid('deactivate', 'qa.admin', '')).toBe(true)
  })

  it('la demo desactiva, activa y elimina cuentas creadas desde el sistema', () => {
    const demo = build()
    expect(demo.lifecyclePolicy().operationsAvailable).toBe(true)
    demo.lifecycleAction('demo:brother', 'deactivate', { reason: REASON })
    expect(demo.inventory().items.find(x => x.subject === 'demo:brother')!.enabled).toBe(false)
    demo.lifecycleAction('demo:brother', 'activate', { reason: REASON })
    expect(demo.inventory().items.find(x => x.subject === 'demo:brother')!.enabled).toBe(true)
    expect(() => demo.lifecycleAction('demo:brother', 'delete-permanently', { reason: REASON, confirmation: 'otro' })).toThrow()
    demo.lifecycleAction('demo:brother', 'delete-permanently', { reason: REASON, confirmation: 'h' })
    expect(demo.inventory().items.some(x => x.subject === 'demo:brother')).toBe(false)
  })

  it('la demo permite eliminar qa.admin con confirmación literal, pero no desactivarlo; admin nunca', () => {
    const demo = build()
    expect(() => demo.lifecycleAction('demo:legacy-qa-admin', 'deactivate', { reason: REASON })).toThrow(/protegida/)
    expect(() => demo.lifecycleAction('demo:legacy-qa-admin', 'delete-permanently', { reason: REASON, confirmation: 'qa.admin' + ' ' })).toThrow()
    demo.lifecycleAction('demo:legacy-qa-admin', 'delete-permanently', { reason: REASON, confirmation: 'qa.admin' })
    expect(demo.inventory().items.some(x => x.username === 'qa.admin')).toBe(false)
    expect(() => demo.lifecycleAction('demo:legacy-admin', 'delete-permanently', { reason: REASON, confirmation: 'admin' })).toThrow(/Máster/)
    expect(() => demo.lifecycleAction('demo:legacy-bootstrap', 'activate', { reason: REASON })).toThrow(/protegida/)
  })

  it('solo el Máster puede usarlo y exige motivo válido', () => {
    expect(() => build(false).lifecyclePolicy()).toThrow()
    expect(() => build(false).lifecycleAction('demo:brother', 'deactivate', { reason: REASON })).toThrow()
    expect(() => build().lifecycleAction('demo:brother', 'deactivate', { reason: 'corto' })).toThrow(/motivo/i)
  })

  it('traduce los errores del servidor a lenguaje simple', () => {
    expect(lifecycleErrorMessage({ status: 409 })).toContain('cambió')
    expect(lifecycleErrorMessage({ status: 503 })).toContain('aún no está habilitada')
    expect(lifecycleErrorMessage({ status: 403 })).toContain('permiso')
    expect(lifecycleErrorMessage(new Error('Mensaje propio'))).toBe('Mensaje propio')
  })
})
