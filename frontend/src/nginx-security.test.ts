import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const conf = readFileSync(new URL('../nginx.conf', import.meta.url), 'utf8')

describe('Seguridad · nginx de la aplicación (04-10-2026)', () => {
  it('permite las cargas de hasta 10 MB que acepta la API y no expone la versión', () => {
    expect(conf).toMatch(/client_max_body_size 12m;/)
    expect(conf).toMatch(/server_tokens off;/)
  })
  it('restringe funciones del navegador no usadas', () => {
    expect(conf).toContain('Permissions-Policy "camera=(), microphone=(), geolocation=(), payment=(), usb=()"')
  })
  it('solo cachea assets con hash y conserva seguridad y revalidación del HTML', () => {
    const block = (location: string) => {
      const start = conf.indexOf(location)
      expect(start).toBeGreaterThan(-1)
      return conf.slice(start, conf.indexOf('\n    }', start))
    }
    const assets = block('location /assets/ {')
    expect(assets).toContain('try_files $uri =404;')
    expect(assets).toContain('Cache-Control "public, max-age=31536000, immutable";')
    const html = block('location = /index.html {')
    expect(html).toContain('Cache-Control "no-cache" always;')
    for (const location of [assets, html]) {
      for (const header of ['X-Content-Type-Options', 'X-Frame-Options', 'Referrer-Policy', 'Permissions-Policy']) {
        expect(location).toContain(`add_header ${header}`)
      }
    }
    for (const path of ['/auth/callback', '/auth/logout']) {
      expect(block(`location = ${path} {`)).toContain('Cache-Control "no-store" always;')
    }
  })
})
