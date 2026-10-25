import { useEffect, useState, type FormEvent } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'

const errorText = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.'
/** Herramienta exclusiva del administrador. El secreto no forma parte del historial de Parámetros. */
export default function GoogleSmtpSettingsPanel({ api }: { api: PmgmApiClient }) {
  const [password, setPassword] = useState('')
  const [recipient, setRecipient] = useState('')
  const [configured, setConfigured] = useState(false)
  const [encryptionReady, setEncryptionReady] = useState(false)
  const [transportReady, setTransportReady] = useState(false)
  const [reason, setReason] = useState('checking')
  const [working, setWorking] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    void api.getGoogleSmtpCredentialStatus().then(data => {
      if (live) { setConfigured(data.configured); setEncryptionReady(data.encryptionReady); setTransportReady(data.transportReady); setReason(data.reason) }
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
      const state = await api.getGoogleSmtpCredentialStatus()
      setConfigured(state.configured)
      setEncryptionReady(state.encryptionReady)
      setTransportReady(state.transportReady)
      setReason(state.reason)
      setNotice(state.transportReady
        ? 'Clave protegida guardada. Listo para intentar prueba de envío; todavía no se ha comprobado la entrega.'
        : 'Clave protegida guardada. Revise los parámetros del servidor antes de probar el envío.')
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
    <p role="status">{api.useMocks ? 'Modo demostración, sin envío real' : reason === 'checking' ? 'Comprobando parámetros del servidor…' :
      reason === 'missing_encryption_key' ? 'El servidor necesita habilitar el cifrado SMTP.' :
      reason === 'missing_password' ? 'Falta guardar la clave SMTP de aplicación.' :
      reason === 'incomplete_transport' ? 'Clave almacenada. Complete smtp.gmail.com, puerto 587, cuenta remitente y STARTTLS en Parámetros.' :
      reason === 'unreadable_password' ? 'El servidor no puede descifrar la clave SMTP. Revise la custodia de la clave de cifrado.' :
      reason === 'ready_to_test' ? 'Configuración local completa. Falta realizar la prueba real de envío a Google.' :
      'No se pudo verificar el estado SMTP.'}</p>
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
      <button type="submit" disabled={working || !transportReady || !recipient.includes('@')}>
        {working ? 'Comprobando…' : 'Enviar correo de prueba real'}
      </button>
    </form>
    {notice && <p role="status">{notice}</p>}
    {error && <p role="alert">{error}</p>}
  </section>
}
