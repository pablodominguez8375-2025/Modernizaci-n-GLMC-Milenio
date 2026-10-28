import { useCallback, useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AccountInventoryItem, AccountLifecyclePolicy } from './api/userAccounts'
import { PROTECTED_LABEL, inventoryErrorMessage, inventoryKindLabel, inventoryOriginLabel, inventoryStateLabel, summarizeInventory } from './accounts/accountInventory'
import { LIFECYCLE_UNAVAILABLE, availableLifecycleActions } from './accounts/accountLifecycle'
import AccountLifecycleDrawer from './AccountLifecycleDrawer'

const POLICY_OFF: AccountLifecyclePolicy = { operationsAvailable: false, actionNames: [], reasonRequired: true, reasonMinLength: 10, reasonMaxLength: 200, deletionNeedsExactUsername: true, requiresLiveIdentityVerification: true, requiresEffectiveSessionRevocation: true }

/** Solo se monta para el Máster único. Muestra las cuentas preexistentes y protegidas (por ejemplo qa.admin) y permite activar, desactivar o eliminar las cuentas creadas desde el sistema. Las acciones las decide y ejecuta el servidor: aquí solo se pide motivo y confirmación. */
export default function AccountInventoryCard({ api, refreshKey, onChanged }: { api: PmgmApiClient; refreshKey?: number; onChanged?: () => void }) {
  const [items, setItems] = useState<AccountInventoryItem[] | null>(null)
  const [policy, setPolicy] = useState<AccountLifecyclePolicy>(POLICY_OFF)
  const [failed, setFailed] = useState(false)
  const [done, setDone] = useState('')
  const [reload, setReload] = useState(0)
  useEffect(() => {
    let live = true
    setFailed(false)
    void api.getUserAccountInventory().then(result => { if (live) setItems(result.items) }).catch(() => { if (live) { setItems(null); setFailed(true) } })
    void api.getUserAccountLifecyclePolicy().then(result => { if (live) setPolicy(result) }).catch(() => { if (live) setPolicy(POLICY_OFF) })
    return () => { live = false }
  }, [api, refreshKey, reload])
  const finished = useCallback((message: string) => { setDone(message); setReload(value => value + 1); onChanged?.() }, [onChanged])

  if (failed) return <p role="status" className="account-inventory-note">{inventoryErrorMessage()}</p>
  if (!items) return <p role="status" className="account-inventory-note">Consultando el inventario completo de cuentas…</p>
  const summary = summarizeInventory(items)
  const managed = items.filter(item => !item.protected)
  const actionsFor = (item: AccountInventoryItem) => availableLifecycleActions(item).map(action =>
    <AccountLifecycleDrawer key={action} api={api} item={item} action={action} policy={policy} onDone={finished} />)
  return <section className="account-inventory" aria-labelledby="inventario-cuentas-titulo">
    <h3 id="inventario-cuentas-titulo">Cuentas preexistentes y protegidas</h3>
    <p className="account-inventory-summary">En total hay <strong>{summary.total}</strong> cuentas de acceso: {summary.managedCount} creadas desde este sistema y {summary.protectedCount} preexistentes y protegidas.</p>
    <p className="account-inventory-note">Las cuentas protegidas no se pueden desactivar ni cambiar desde el sistema. Solo la cuenta <strong>qa.admin</strong> admite una eliminación excepcional, hecha por usted con motivo y confirmación. La cuenta <strong>admin</strong> nunca se modifica.</p>
    {!policy.operationsAvailable && <p role="status" className="account-inventory-note account-lifecycle-off">{LIFECYCLE_UNAVAILABLE}</p>}
    {done && <p role="status" className="account-lifecycle-done">{done}</p>}
    {summary.protectedItems.length === 0 && <p>No hay cuentas preexistentes.</p>}
    <ul className="account-inventory-list">
      {summary.protectedItems.map(item => <li className="system-user-row account-inventory-row" key={item.subject}>
        <strong>{item.name || item.username}</strong>
        <span className="account-protected-badge">{PROTECTED_LABEL}</span>
        <small>Usuario: {item.username}</small>
        <small>{inventoryKindLabel(item)} · {inventoryStateLabel(item)}</small>
        <small>{inventoryOriginLabel(item)}</small>
        <small>Identificador de usuario: {item.subject}</small>
        {availableLifecycleActions(item).length > 0 && <div className="action-bar">{actionsFor(item)}</div>}
      </li>)}
    </ul>
    <h3>Activar, desactivar o eliminar cuentas</h3>
    <p className="account-inventory-note">Para las cuentas de Hermanos y Superusuarios de soporte creadas desde este sistema. Cada acción pide un motivo y queda en la auditoría.</p>
    {managed.length === 0 && <p>No hay cuentas creadas desde este sistema.</p>}
    <ul className="account-inventory-list">
      {managed.map(item => <li className="system-user-row account-inventory-row" key={item.subject}>
        <strong>{item.name || item.username}</strong>
        <small>Usuario: {item.username}</small>
        <small>{inventoryKindLabel(item)} · {inventoryStateLabel(item)}</small>
        <div className="action-bar">{actionsFor(item)}</div>
      </li>)}
    </ul>
  </section>
}
