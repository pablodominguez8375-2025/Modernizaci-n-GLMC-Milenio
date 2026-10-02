import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const portal = readFileSync(new URL('./MemberPortalPage.tsx', import.meta.url), 'utf8')
const papers = readFileSync(new URL('./MemberWorkPapersPanel.tsx', import.meta.url), 'utf8')
const css = readFileSync(new URL('./common-views.css', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')

describe('PMGM-UX vistas comunes · Mi ficha sin duplicados y acciones bajo demanda', () => {
  it('Mi ficha no repite agenda ni avisos (ya están en Inicio, Agenda y Avisos) y ofrece accesos directos', () => {
    expect(portal).not.toContain('member-calendar-card')
    expect(portal).not.toContain('member-lower-grid')
    expect(portal).toContain('className="member-quick-links"')
  })

  it('el historial de instrucciones queda plegado y la subida de planchas se abre en panel', () => {
    expect(portal.match(/member-instruction-details/g)?.length).toBe(2)
    expect(papers).toContain('<ActionDrawer label="Subir una plancha"')
  })

  it('la hoja se carga al final y usa unidades relativas', () => {
    expect(main.indexOf("./common-views.css")).toBeGreaterThan(main.indexOf("./home-consistency.css"))
    expect(css).not.toMatch(/font-size:\s*\d+px/)
  })
})
