import { useEffect, useState, type FormEvent } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'

const errorText = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.'
/** Herramienta exclusiva del administrador. El secreto no forma parte del historial de Parámetros. */
export default function GoogleSmtpSettingsPanel({ api }: { api: PmgmApiClient }) {
  const [password, setPassword] = useState('')
  const [recipient, setRecipient] = useState('')
  const [configured, setConfigured] = useState(false)
  const [encryptionReady, setEncryptionReady] = useState(false)
  const [working, setWorking] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    void api.getGoogleSmtpCredentialStatus().then(data => {
      if (live) { setConfigured(data.configured); setEncryptionReady(data.encryptionReady) }
    }).catch(e => { if (live) setError(errorText(e)) })
    return () => { live = false }
  }, [api])

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!password.trim()) return
    setWorking(true); setError(null); setNotice(null)
    try {
      await api.saveGoogleSmtpPassword(password)
      setPassword('')
      setConfigured(true)
      setNotice('Clave de aplicación guardada de forma protegida. Ahora puede enviar un correo de prueba.')
    } catch (e) { setError(errorText(e)) }
    finally { setWorking(false) }
  }

  const test = async (event: FormEvent) => {
    event.preventDefault()
    setWorking(true); setError(null); setNotice(null)
    try {
      const result = await api.testGoogleSmtp(recipient.trim())
      setNotice(result.message)
    } catch (e) { setError(errorText(e)) }
    finally { setWorking(false) }
  }

  return <section className="system-mail-credentials" aria-label="Autenticación y prueba SMTP Google">
    <h3>Autenticación SMTP de Google</h3>
    <p>Configure arriba <strong>smtp.gmail.com</strong>, puerto <strong>587</strong>, seguridad <strong>STARTTLS</strong> y su cuenta completa de Google como remitente. Guarde primero esos parámetros.</p>
    <p>Utilice una <strong>contraseña de aplicación</strong> de Google si su cuenta la permite, no la contraseña habitual. El secreto nunca aparece en el historial ni se muestra de nuevo.</p>
    <p role="status">{configured ? 'Credencial guardada en el servidor' : encryptionReady ? 'Falta guardar la credencial SMTP' : api.useMocks ? 'Modo demostración, sin envío real' : 'El servidor aún necesita habilitar el cifrado SMTP'}</p>
    <form onSubmit={event => void save(event)}>
      <label><span>Clave SMTP / contraseña de aplicación</span>
        <input type="password" autoComplete="new-password" spellCheck={false}
          value={password} maxLength={256} placeholder="Ingrese la clave de aplicación"
          onChange={event => setPassword(event.target.value)} aria-label="Clave SMTP" /></label>
      <button type="submit" disabled={working || password.replace(/ /g, '').length < 12 || (!encryptionReady && !api.useMocks)}>
        {working ? 'Procesando…' : configured ? 'Reemplazar clave SMTP' : 'Guardar clave SMTP'}
      </button>
    </form>
    <form onSubmit={event => void test(event)}>
      <label><span>Destinatario del mensaje de prueba</span>
        <input type="email" autoComplete="email" required value={recipient}
          placeholder="correo@dominio.cl"
          onChange={event => setRecipient(event.target.value)} /></label>
      <button type="submit" disabled={working || !configured || !recipient.includes('@')}>
        {working ? 'Comprobando…' : 'Enviar correo de prueba real'}
      </button>
    </form>
    {notice && <p role="status">{notice}</p>}
    {error && <p role="alert">{error}</p>}
  </section>
}
