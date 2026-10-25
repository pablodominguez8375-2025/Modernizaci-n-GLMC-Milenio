import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import GoogleSmtpSettingsPanel from './GoogleSmtpSettingsPanel'
import { PmgmApiClient } from './api/pmgmApi'

describe('SMTP Google: administración segura', () => {
  it('expone clave protegida y envío real sin presentar contraseñas guardadas', () => {
    const html = renderToStaticMarkup(
      <GoogleSmtpSettingsPanel api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Clave SMTP')
    expect(html).toContain('type="password"')
    expect(html).toContain('Enviar correo de prueba real')
    expect(html).toContain('smtp.gmail.com')
    expect(html).toContain('STARTTLS')
    expect(html).not.toContain('abcdefghijklmnop')
  })

  it('la demo jamás confirma falsamente el envío real', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    expect((await api.getGoogleSmtpCredentialStatus()).configured).toBe(false)
    await api.saveGoogleSmtpPassword('abcdefghijklmnop')
    expect((await api.getGoogleSmtpCredentialStatus()).configured).toBe(true)
    const test = await api.testGoogleSmtp('test@example.org')
    expect(test.sent).toBe(false)
    expect(test.message).toContain('no se envió')
  })
})
