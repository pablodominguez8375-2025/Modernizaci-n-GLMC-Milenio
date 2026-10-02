/// <reference types="node" />

import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('./institutional-theme.css', import.meta.url), 'utf8')
const fidelityCss = readFileSync(new URL('./ppt-fidelity.css', import.meta.url), 'utf8')
const memberPortalCss = readFileSync(new URL('./member-portal.css', import.meta.url), 'utf8')
const memberLibraryShortcutCss = readFileSync(new URL('./memberLibraryShortcut.css', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')

describe('PMGM-UI-001 institutional responsive contract', () => {
  it('keeps the approved institutional palette tokens', () => {
    expect(css).toContain('--brand-navy: #06148E')
    expect(css).toContain('--brand-blue: #004AD4')
    expect(css).toContain('--brand-blue-light: #B4D0F4')
    expect(css).toContain('--brand-gold: #F3C609')
    expect(css).toContain('--brand-gold-strong: #FBAE17')
    expect(css).toContain('--font-ui: Cambria, Georgia, "Times New Roman", serif')
    expect(css).toContain('--font-institutional-name: "Arial Narrow", Arial, sans-serif')
  })

  it('uses gold glyphs on navy status and callout tiles', () => {
    expect(fidelityCss).toMatch(/\.member-status-icon,\s*\.member-callout-icon\s*\{\s*color:\s*var\(--member-ppt-gold\)/)
  })

  it('shows the Library Virtual shortcut glyph in gold on its navy tile', () => {
    const iconRule = memberLibraryShortcutCss.match(/\.member-degree-library-icon\s*\{([^}]+)\}/)?.[1] ?? ''
    expect(iconRule).toContain('background: #06148E')
    expect(iconRule).toContain('color: var(--brand-gold)')
  })

  it('keeps navigation accents readable against the corporate gold', () => {
    expect(css).toMatch(/\.sidebar \.nav-item\.active\s*\{[^}]*color:\s*var\(--brand-navy\)/s)
  })

  it('keeps the active member portal menu gold on desktop and mobile', () => {
    const desktopActiveRule = memberPortalCss.match(/\.sidebar \.nav-item\.active\s*\{([^}]+)\}/)?.[1] ?? ''
    const mobileActiveRule = memberPortalCss.match(/@media \(max-width: 980px\)[\s\S]*?\.sidebar \.nav-item\.active\s*\{([^}]+)\}/)?.[1] ?? ''

    for (const rule of [desktopActiveRule, mobileActiveRule]) {
      expect(rule).toContain('color: var(--brand-navy)')
      expect(rule).toMatch(/background:\s*linear-gradient/)
      expect(rule).toContain('var(--brand-gold)')
      expect(rule).toContain('var(--brand-gold-strong)')
    }
  })

  it('does not let the hover state override the gold active menu item (sticky hover after tapping on mobile)', () => {
    for (const source of [css, memberPortalCss]) {
      const hoverSelectors = [...source.matchAll(/([^{}]*\.sidebar \.nav-item:hover[^{]*)\{/g)].map(match => match[1].trim())
      expect(hoverSelectors.length).toBeGreaterThan(0)
      for (const selector of hoverSelectors) expect(selector).toContain(':not(.active)')
    }
  })

  it('loads the institutional layer last so module styles inherit the current standard', () => {
    expect(main.lastIndexOf("import './institutional-theme.css'")).toBeGreaterThan(main.lastIndexOf("import './mobile-nav-compact.css'"))
  })

  it('prevents global horizontal scrolling and constrains media', () => {
    expect(css).toMatch(/body\s*\{[^}]*overflow-x:\s*hidden/s)
    expect(css).toMatch(/img,\s*\nsvg,\s*\nvideo,\s*\ncanvas\s*\{[^}]*max-width:\s*100%/s)
  })

  it.each([1180, 1100, 980, 720, 480])('defines responsive behavior at %ipx', breakpoint => {
    expect(css).toContain(`@media (max-width: ${breakpoint}px)`)
  })

  it('keeps tables internally scrollable on narrow screens', () => {
    expect(css).toMatch(/\.content table\s*\{[^}]*display:\s*block[^}]*overflow-x:\s*auto/s)
  })

  it('keeps touch targets at least 44px high on mobile', () => {
    expect(css).toMatch(/\.content button,[\s\S]*?min-height:\s*44px/)
  })

  it('preserves the full mobile brand without truncation at narrower breakpoints', () => {
    const mobileCss = css.slice(css.indexOf('@media (max-width: 720px)'))
    const brandRules = [...mobileCss.matchAll(/\.brand strong\s*\{([^}]+)\}/g)].map(match => match[1])
    expect(brandRules.length).toBeGreaterThan(0)
    for (const rule of brandRules) {
      expect(rule).toContain('white-space: normal')
      expect(rule).toContain('line-height: 1.1')
      expect(rule).toContain('max-width: none')
      expect(rule).not.toMatch(/ellipsis|overflow:\s*hidden|white-space:\s*nowrap/)
    }
  })

  it('keeps the mobile Demo SHA readable and visible below 480px', () => {
    const mobileCss = css.slice(css.indexOf('@media (max-width: 720px)'))
    const labelRules = [...mobileCss.matchAll(/\.demo-profile-switcher \.demo-public-label\s*\{([^}]+)\}/g)].map(match => match[1])
    expect(labelRules.length).toBeGreaterThan(0)
    for (const rule of labelRules) {
      expect(rule).toContain('display: inline-flex')
      expect(rule).toMatch(/font-size: 0\.(75|875)rem/) // PMGM-UX-004: rem, mínimo 12 px
      expect(rule).not.toContain('display: none')
    }
    const switcher = readFileSync(new URL('./DemoProfileSwitcher.tsx', import.meta.url), 'utf8')
    expect(switcher).toContain('showcaseSha.slice(0, 7)')
    expect(switcher).toContain('className="demo-public-sha"')
    const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
    expect(app).toContain('UI QA v0.68')
  })

  it('supports reduced motion preferences', () => {
    expect(css).toContain('@media (prefers-reduced-motion: reduce)')
  })
})
