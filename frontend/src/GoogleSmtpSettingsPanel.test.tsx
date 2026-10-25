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
    const before = await api.getGoogleSmtpCredentialStatus()
    expect(before.configured).toBe(false)
    expect(before.transportReady).toBe(false)
    expect(before.reason).toBe('missing_password')
    await api.saveGoogleSmtpPassword('abcdefghijklmnop')
    const after = await api.getGoogleSmtpCredentialStatus()
    expect(after.configured).toBe(true)
    expect(after.transportReady).toBe(true)
    expect(after.reason).toBe('ready_to_test')
    const test = await api.testGoogleSmtp('test@example.org')
    expect(test.sent).toBe(false)
    expect(test.message).toContain('no se envió')
  })
})
