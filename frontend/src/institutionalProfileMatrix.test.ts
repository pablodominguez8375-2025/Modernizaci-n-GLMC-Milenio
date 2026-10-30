import { describe, expect, it } from 'vitest'
import { demoProfiles } from './demoProfiles'
import { institutionalProfileMatrix } from './institutionalProfileMatrix'

describe('matriz de perfiles institucionales', () => {
  it('tiene una fila por cada perfil base del sistema, sin repetir', () => {
    const keys = institutionalProfileMatrix.map(profile => profile.key)
    expect(new Set(keys).size).toBe(keys.length)
    expect([...keys].sort()).toEqual(Object.keys(demoProfiles).sort())
  })
  it('incluye los perfiles que antes faltaban', () => {
    const labels = institutionalProfileMatrix.map(profile => profile.label)
    for (const name of ['Gran Primer Vigilante', 'Jefatura de Docencia', 'Bibliotecario', 'Comunicaciones', 'Gran Archivero', 'Régimen Interior']) expect(labels).toContain(name)
  })
})
