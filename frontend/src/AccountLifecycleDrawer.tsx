import { useState } from 'react'
import { ActionDrawer } from './actionKit'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AccountInventoryItem, AccountLifecycleAction, AccountLifecyclePolicy } from './api/userAccounts'
import { LIFECYCLE_LABELS, REASON_MAX, REASON_MIN, lifecycleConfirmationValid, lifecycleDescription, lifecycleDoneMessage, lifecycleErrorMessage, lifecycleReasonValid } from './accounts/accountLifecycle'

function LifecycleForm({ api, item, action, policy, onDone }: { api: PmgmApiClient; item: AccountInventoryItem; action: AccountLifecycleAction; policy: AccountLifecyclePolicy; onDone: (message: string) => void }) {
  const [reason, setReason] = useState('')
  const [typed, setTyped] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const name = item.name || item.username
  const isDelete = action === 'delete-permanently'
  const ready = lifecycleReasonValid(reason, policy) && lifecycleConfirmationValid(action, item.username, typed)
  const submit = async () => {
    setBusy(true); setError('')
    try {
      await api.runUserAccountLifecycle(item.subject, action, { reason: reason.trim(), confirmation: isDelete ? typed : undefined })
      onDone(lifecycleDoneMessage(action, name))
    } catch (e) { setError(lifecycleErrorMessage(e)) } finally { setBusy(false) }
  }
  return <form onSubmit={e => { e.preventDefault(); void submit() }}>
    <p>{lifecycleDescription(action, name)}</p>
    {item.protected && <p className="account-lifecycle-warning"><strong>Cuenta preexistente · Protegida.</strong> La eliminación es una excepción autorizada solo para esta cuenta y solo para el Máster.</p>}
    <p>Queda registrado quién lo hizo, cuándo y por qué.</p>
    {error && <p role="alert">{error}</p>}
    <label><span>Motivo ({policy.reasonMinLength ?? REASON_MIN} a {policy.reasonMaxLength ?? REASON_MAX} caracteres)</span>
      <input required minLength={policy.reasonMinLength ?? REASON_MIN} maxLength={policy.reasonMaxLength ?? REASON_MAX} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} placeholder="Ej.: La persona ya no pertenece a la Orden" /></label>
    {isDelete && <label><span>Para confirmar, escriba el usuario <strong>{item.username}</strong> tal como aparece</span>
      <input required autoComplete="off" autoCapitalize="none" spellCheck={false} value={typed} disabled={busy} onChange={e => setTyped(e.target.value)} /></label>}
    <button className={isDelete ? 'account-lifecycle-danger' : undefined} disabled={busy || !ready}>{busy ? 'Procesando…' : LIFECYCLE_LABELS[action]}</button>
  </form>
}

export default function AccountLifecycleDrawer({ api, item, action, policy, onDone }: { api: PmgmApiClient; item: AccountInventoryItem; action: AccountLifecycleAction; policy: AccountLifecyclePolicy; onDone: (message: string) => void }) {
  const [open, setOpen] = useState(false)
  const name = item.name || item.username
  const confirmMessage = action === 'delete-permanently' ? `Se eliminará para siempre la cuenta de ${name}. No se puede deshacer.` : undefined
  return <ActionDrawer label={LIFECYCLE_LABELS[action]} title={`${LIFECYCLE_LABELS[action]} la cuenta de ${name}`} confirmMessage={confirmMessage} tone="secondary" disabled={!policy.operationsAvailable} open={open} onOpenChange={setOpen} keepOpen>
    {open && <LifecycleForm api={api} item={item} action={action} policy={policy} onDone={message => { setOpen(false); onDone(message) }} />}
  </ActionDrawer>
}
