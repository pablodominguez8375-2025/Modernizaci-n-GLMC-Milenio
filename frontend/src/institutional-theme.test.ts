import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('./institutional-theme.css', import.meta.url), 'utf8')

describe('PMGM-UI-001 institutional responsive contract', () => {
  it('keeps the approved institutional palette tokens', () => {
    expect(css).toContain('--brand-navy: #102c54')
    expect(css).toContain('--brand-blue: #173b6e')
    expect(css).toContain('--brand-gold: #b48a37')
    expect(css).toContain('--brand-gold-strong: #c79d45')
    expect(css).toContain('--brand-canvas: #f3f6f9')
  })

  it('prevents global horizontal scrolling and constrains media', () => {
    expect(css).toMatch(/body\s*\{[^}]*overflow-x:\s*hidden/s)
    expect(css).toMatch(/img,\s*\nsvg,\s*\nvideo,\s*\ncanvas\s*\{[^}]*max-width:\s*100%/s)
  })

  it.each([1180, 980, 720, 480])('defines responsive behavior at %ipx', breakpoint => {
    expect(css).toContain(`@media (max-width: ${breakpoint}px)`)
  })

  it('keeps tables internally scrollable on narrow screens', () => {
    expect(css).toMatch(/\.content table\s*\{[^}]*display:\s*block[^}]*overflow-x:\s*auto/s)
  })

  it('keeps touch targets at least 44px high on mobile', () => {
    expect(css).toMatch(/\.content button,[\s\S]*?min-height:\s*44px/)
  })

  it('supports reduced motion preferences', () => {
    expect(css).toContain('@media (prefers-reduced-motion: reduce)')
  })
})
