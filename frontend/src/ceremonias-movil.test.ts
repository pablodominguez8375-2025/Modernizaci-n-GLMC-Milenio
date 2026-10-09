import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('Ceremonias en celular (auditoría 09-10-2026)', () => {
  const css = readFileSync(new URL('./ceremonies.css', import.meta.url), 'utf8')
  it('las tarjetas y el resumen de derecho no ensanchan la pantalla (a 390 px se salían 145 px)', () => {
    expect(css).toContain('.ceremony-list > *, .ceremony-card, .ceremony-card > * { min-width: 0; }')
    expect(css).toContain('.ceremony-right-summary, .ceremony-right-summary > *, .ceremony-right-summary dl > div { min-width: 0; }')
    expect(css).toContain('.ceremony-right-summary > div:first-child { grid-template-columns: minmax(0, 1fr); }')
  })
})
