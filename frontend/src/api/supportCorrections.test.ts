import { describe, expect, it } from 'vitest'
import { UserAccountsDemo, type AccountUser } from './userAccounts'

const seed: AccountUser[] = [
  { subject: 'demo:brother', name: 'Hermano Demostrativo', email: 'h@ejemplo.cl', enabled: true, kind: 'member' },
  { subject: 'demo:supportUser', name: 'Soporte Demostrativo', email: 's@ejemplo.cl', enabled: true, kind: 'superuser' },
]
function build(who: { current: string }) {
  return new UserAccountsDemo([], () => true, () => who.current === 'demo:systemAdmin', seed, () => who.current, () => who.current === 'demo:supportUser')
}

describe('Corregir como soporte (demo)', () => {
  it('solo el Máster da el permiso, solo el Superusuario corrige y el Hermano ve la corrección', () => {
    const who = { current: 'demo:supportUser' }
    const demo = build(who)
    expect(() => demo.grantCorrection({ operatorSubject: 'demo:supportUser', reason: 'Corregir contactos de la semana', minutes: 60 })).toThrow()
    expect(demo.correctionStatus().canCorrect).toBe(false)

    who.current = 'demo:systemAdmin'
    expect(() => demo.grantCorrection({ operatorSubject: 'demo:brother', reason: 'Corregir contactos de la semana', minutes: 60 })).toThrow()
    expect(() => demo.grantCorrection({ operatorSubject: 'demo:supportUser', reason: 'corto', minutes: 60 })).toThrow()
    expect(() => demo.grantCorrection({ operatorSubject: 'demo:supportUser', reason: 'Corregir contactos de la semana', minutes: 600 })).toThrow()
    demo.grantCorrection({ operatorSubject: 'demo:supportUser', reason: 'Corregir contactos de la semana', minutes: 60 })
    expect(demo.correctionStatus().isMaster).toBe(true)
    expect(demo.correctionStatus().canCorrect).toBe(false) // el Máster no corrige
    expect(() => demo.correct({ targetSubject: 'demo:brother', field: 'phone', newValue: '+56 9 1111 2222', reason: 'El Máster no corrige' })).toThrow()

    who.current = 'demo:supportUser'
    expect(demo.correctionStatus().canCorrect).toBe(true)
    expect(() => demo.correct({ targetSubject: 'demo:brother', field: 'phone', newValue: '+56 9 1111 2222', reason: 'corto' })).toThrow()
    expect(() => demo.correct({ targetSubject: 'demo:brother', field: 'email', newValue: 'sin-arroba', reason: 'Correo mal escrito' })).toThrow()
    expect(() => demo.correct({ targetSubject: 'demo:supportUser', field: 'phone', newValue: '+56 9 1111 2222', reason: 'No a una cuenta de soporte' })).toThrow()
    expect(demo.correct({ targetSubject: 'demo:brother', field: 'phone', newValue: '+56 9 1111 2222', reason: 'Teléfono mal digitado en la ficha' }).label).toBe('Teléfono')

    who.current = 'demo:brother'
    const mine = demo.myCorrections()
    expect(mine).toHaveLength(1)
    expect(mine[0]).toMatchObject({ label: 'Teléfono', operatorName: 'Soporte Demostrativo' })
    expect(JSON.stringify(mine)).not.toContain('1111')
  })

  it('retirar el permiso corta la posibilidad de corregir', () => {
    const who = { current: 'demo:systemAdmin' }
    const demo = build(who)
    const grant = demo.grantCorrection({ operatorSubject: 'demo:supportUser', reason: 'Corregir contactos de la semana', minutes: 30 })
    demo.revokeCorrectionGrant(grant.grantId)
    who.current = 'demo:supportUser'
    expect(demo.correctionStatus().canCorrect).toBe(false)
  })
})
