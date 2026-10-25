import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const page = readFileSync(new URL('./SystemConfigurationPage.tsx', import.meta.url), 'utf8')
const ops = readFileSync(new URL('./SystemOperationsPanel.tsx', import.meta.url), 'utf8')

describe('PMGM-UX administración ordenada (04-10-2026)', () => {
  it('un solo menú de secciones: la consola no muestra su propio menú cuando está integrada', () => {
    expect(page).toMatch(/tabs=\{\[\{id:'backup',label:'Respaldos'(?:,icon:'\w+' as const)?\},\{id:'users',label:'Usuarios'(?:,icon:'\w+' as const)?\},\{id:'profiles',label:'Perfiles y accesos'/)
    expect(ops).toContain('{!controlledArea&&<nav className="system-tabs"')
  })

  it('los parámetros se eligen por categoría en tarjetas (no pestañas) y se cambian desde un panel, sin formularios abiertos', () => {
    expect(page).toContain('<WorkspaceTabs label="Categorías de parámetros"')
    expect(page).toMatch(/Categorías de parámetros"[^>]*hub=\{\{ title: 'Parámetros'/)
    expect(page).toContain('<SystemSettingsCategoryPanel')
    expect(readFileSync(new URL('./SystemSettingsCategoryPanel.tsx', import.meta.url), 'utf8')).toContain('<ActionDrawer label="Editar parámetros"')
    expect(page).not.toContain("useState('Todos')")
  })

  it('el diseñador de perfiles vive en «Perfiles y accesos», no en «Usuarios»', () => {
    const users = ops.split("{area==='users'&&")[1].split("{area==='profiles'&&")[0]
    expect(users).not.toContain('Diseñar perfiles de acceso')
    expect(ops.split("{area==='profiles'&&")[1]).toContain('<ActionDrawer label="Diseñar perfiles de acceso"')
    expect(page).toContain("['backup','users','profiles'")
  })

  it('restauración, correo e identidad se abren en panel', () => {
    for (const label of ['Restaurar un respaldo', 'Editar logos y colores', 'Diseñar perfiles de acceso']) expect(ops).toContain(`<ActionDrawer label="${label}"`)
  })
  it('el correo vive solo en su tarjeta, funcional y auditable (sin repetirse en Parámetros)', () => {
    const page = readFileSync(new URL('./SystemConfigurationPage.tsx', import.meta.url), 'utf8')
    expect(page).toContain("{id:'mail',label:'Correo',icon:'mail' as const}")
    expect(page).toContain('<WorkspacePanel id="mail" active={tab===\'mail\'}>')
    expect(page).toContain('category={MAIL}')
    expect(page).toContain('items.filter(item=>item.category!==MAIL)')
    expect(ops).not.toContain("area==='mail'")
  })
  it('la asignación de perfiles se abre en panel', () => {
    expect(readFileSync(new URL('./UserAccessAssignments.tsx', import.meta.url), 'utf8')).toContain('<ActionDrawer label="Asignar perfil a un usuario"')
  })
})
