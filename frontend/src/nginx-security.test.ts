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
})
