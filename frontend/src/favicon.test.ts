import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8')
const favicon = readFileSync(new URL('../public/brand/favicon-glmch.svg', import.meta.url), 'utf8')

describe('Ícono de pestaña/app (decisión del PO 03-10-2026)', () => {
  it('index.html declara el favicon SVG, el PNG de 32 px y el ícono de Apple', () => {
    expect(html).toContain('<link rel="icon" type="image/svg+xml" href="/brand/favicon-glmch.svg" />')
    expect(html).toContain('href="/brand/favicon-32.png"')
    expect(html).toContain('<link rel="apple-touch-icon" sizes="180x180" href="/brand/apple-touch-icon.png" />')
    expect(existsSync(new URL('../public/brand/favicon-32.png', import.meta.url))).toBe(true)
    expect(existsSync(new URL('../public/brand/apple-touch-icon.png', import.meta.url))).toBe(true)
  })

  it('usa el color oficial del logo reducido sobre placa blanca', () => {
    expect(favicon).toContain('fill:#021493')
    expect(favicon).toMatch(/<rect [^>]*fill="#ffffff"/)
  })
})
