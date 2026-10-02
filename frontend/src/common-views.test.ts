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

describe('PMGM-UX vistas comunes · Avisos con una acción visible e íconos SVG', () => {
  const notifications = readFileSync(new URL('./NotificationsPage.tsx', import.meta.url), 'utf8')

  it('cada aviso muestra una sola acción principal y el resto va al menú', () => {
    expect(notifications).toContain("{onAction && unread && <RowMenu items={[{ label: 'Marcar como leído'")
    expect(notifications).toContain('label={markingAll ? \'Marcando…\' : \'Marcar todos como leídos\'}')
  })

  it('no usa glifos de texto como íconos (PMGM-UI-001 §3)', () => {
    expect(notifications).not.toMatch(/return '[◉◎▣◇□✦]'/)
    expect(portal).not.toContain('member-status-icon">$<')
    expect(portal).not.toContain('♥')
  })
})
