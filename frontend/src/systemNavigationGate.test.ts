import { describe, expect, it } from 'vitest'
import { shouldReturnHomeForDeniedView, systemNavigationGate } from './systemNavigationGate'

describe('QA G10 - Sistema no regresa a Inicio al validar permisos', () => {
  it('mantiene Sistema a la vista durante recarga fallida o aún pendiente', () => {
    expect(systemNavigationGate('system', false, false)).toBe('checking')
    expect(shouldReturnHomeForDeniedView('system', true, false, false)).toBe(false)
    expect(systemNavigationGate('bootstrap', false, false)).toBe('checking')
    expect(shouldReturnHomeForDeniedView('bootstrap', true, false, false)).toBe(false)
  })
  it('muestra denegación explícita sin presentar ni cargar consola', () => {
    expect(systemNavigationGate('system', true, false)).toBe('denied')
    expect(shouldReturnHomeForDeniedView('system', true, true, false)).toBe(false)
    expect(systemNavigationGate('bootstrap', true, false)).toBe('denied')
  })
  it('abre normalmente Sistema solo cuando autorización está confirmada', () => {
    expect(systemNavigationGate('system', true, true)).toBe('ready')
    expect(shouldReturnHomeForDeniedView('system', true, true, true)).toBe(false)
  })
  it('no redirige a otros módulos mientras revalida snapshot', () => {
    expect(shouldReturnHomeForDeniedView('members', true, false, false)).toBe(false)
    expect(shouldReturnHomeForDeniedView('members', true, true, false)).toBe(true)
    expect(shouldReturnHomeForDeniedView('members', true, true, true)).toBe(false)
    expect(shouldReturnHomeForDeniedView('dashboard', false, true, false)).toBe(false)
    expect(systemNavigationGate('dashboard', true, true)).toBe('not-system')
  })
})
