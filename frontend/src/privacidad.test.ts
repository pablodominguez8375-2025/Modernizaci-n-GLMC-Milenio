import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const app = readFileSync('src/App.tsx', 'utf8')
const page = readFileSync('src/PrivacyPage.tsx', 'utf8')

describe('Privacidad y mis datos (Ley 21.719)', () => {
  it('es una opción de Mi espacio abierta a todos los perfiles', () => {
    expect(app).toContain("label=\"Privacidad y mis datos\" allowed")
    expect(app).toContain("view === 'privacy' && <PrivacyPage")
  })
  it('explica los cinco derechos y no usa formularios abiertos', () => {
    for (const title of ['Saber qué datos tenemos', 'Corregir un dato', 'Pedir que se elimine', 'Oponerte o limitar su uso', 'Llevar tus datos contigo']) expect(page).toContain(title)
    expect(page).not.toContain('<form')
  })
  it('indica el correo del departamento jurídico para ejercer los derechos', () => {
    expect(page).toContain('juridico@granlogiamixta.cl')
  })
})
