import { describe, expect, it } from 'vitest'
import { DynamicAccessClient, type CatalogUpdateReason } from './api/dynamicAccess'

describe('G10 · Abrir Usuarios no invalida la sesión al leer el catálogo', () => {
  it('distingue lectura inicial frente a cambios de permisos', async () => {
    const events: CatalogUpdateReason[] = []
    const client = new DynamicAccessClient(async () => { throw new Error('No debe usar red en demo') }, true)
    client.subscribe(reason => { events.push(reason) })

    await client.load()
    expect(events).toEqual(['load'])
    expect(events.map(reason => reason === 'mutation')).toEqual([false])

    await client.create({
      code: 'prueba-segura',
      name: 'Perfil QA temporal',
      scope: 'order',
      menuCodes: ['system'],
    }, 0)
    expect(events).toEqual(['load', 'mutation'])
    expect(events.map(reason => reason === 'mutation')).toEqual([false, true])
  })
  it('la carga de catálogo API emite lectura y su modificación emite mutación', async () => {
    const events: CatalogUpdateReason[] = []
    const paths: string[] = []
    const api = new DynamicAccessClient(async (path: string) => {
      paths.push(path)
      return {
        version: paths.length,
        actions: ['view', 'create', 'write', 'edit', 'delete', 'print'],
        menus: [], profiles: [], assignments: [],
      } as never
    }, false)
    api.subscribe(reason => { events.push(reason) })
    await api.load()
    expect(paths).toEqual(['/api/system/access/catalog'])
    expect(events).toEqual(['load'])
    await api.create({code:'qa-test',name:'Prueba QA',scope:'order',menuCodes:[]}, 1)
    expect(events).toEqual(['load','mutation'])
  })
})
