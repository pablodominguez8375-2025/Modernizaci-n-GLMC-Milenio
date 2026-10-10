import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const page = readFileSync(new URL('./SystemConfigurationPage.tsx', import.meta.url), 'utf8')
const ops = readFileSync(new URL('./SystemOperationsPanel.tsx', import.meta.url), 'utf8')

describe('PMGM-UX administración ordenada (04-10-2026)', () => {
  it('un solo menú de secciones: la consola no muestra su propio menú cuando está integrada', () => {
    expect(page).toMatch(/tabs=\{\[\{id:'backup',label:'Respaldos'(?:,icon:'\w+' as const)?\},\{id:'users',label:'Usuarios'(?:,icon:'\w+' as const)?\},\{id:'profiles',label:'Perfiles y accesos'/)
    expect(ops).toContain('{!controlledArea&&<nav className="system-tabs"')
  })

  it('los parámetros se ven por categoría y se cambian desde un panel, sin formularios abiertos', () => {
    expect(page).toContain('<WorkspaceTabs label="Categorías de parámetros"')
    expect(page).toContain('<ActionDrawer label="Cambiar valor"')
    expect(page).not.toContain("useState('Todos')")
  })

  it('restauración, correo e identidad se abren en panel', () => {
    for (const label of ['Restaurar un respaldo', 'Editar configuración de correo', 'Editar logos y colores', 'Diseñar perfiles de acceso']) expect(ops).toContain(`<ActionDrawer label="${label}"`)
  })
  it('la asignación de perfiles se abre en panel', () => {
    expect(readFileSync(new URL('./UserAccessAssignments.tsx', import.meta.url), 'utf8')).toContain('<ActionDrawer label="Asignar perfil a un usuario"')
  })
})
