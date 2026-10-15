import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('Mis asistencias: filtros plegados', () => {
  const src = readFileSync(new URL('./MemberOwnHistory.tsx', import.meta.url), 'utf8')
  it('los filtros viven dentro de «Filtrar» y se abren solos si hay alguno activo', () => {
    expect(src).toContain('<details className="own-filter" open={activos>0}>')
    expect(src).toContain('Filtrar')
    expect(src).toContain('Quitar filtros')
  })
  it('no usa <form> abierto', () => {
    expect(src).not.toContain('<form')
  })
})
