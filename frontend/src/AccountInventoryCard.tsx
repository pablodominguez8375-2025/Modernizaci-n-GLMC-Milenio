import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AccountInventoryItem } from './api/userAccounts'
import { PROTECTED_LABEL, inventoryErrorMessage, inventoryKindLabel, inventoryOriginLabel, inventoryStateLabel, summarizeInventory } from './accounts/accountInventory'

/** Solo se monta para el Máster único. Muestra las cuentas preexistentes y protegidas (por ejemplo qa.admin) que no aparecen en «Usuarios configurados». Solo lectura: sin activar, desactivar ni eliminar. */
export default function AccountInventoryCard({ api, refreshKey }: { api: PmgmApiClient; refreshKey?: number }) {
  const [items, setItems] = useState<AccountInventoryItem[] | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    let live = true
    setFailed(false)
    void api.getUserAccountInventory().then(result => { if (live) setItems(result.items) }).catch(() => { if (live) { setItems(null); setFailed(true) } })
    return () => { live = false }
  }, [api, refreshKey])

  if (failed) return <p role="status" className="account-inventory-note">{inventoryErrorMessage()}</p>
  if (!items) return <p role="status" className="account-inventory-note">Consultando el inventario completo de cuentas…</p>
  const summary = summarizeInventory(items)
  return <section className="account-inventory" aria-labelledby="inventario-cuentas-titulo">
    <h3 id="inventario-cuentas-titulo">Cuentas preexistentes y protegidas</h3>
    <p className="account-inventory-summary">En total hay <strong>{summary.total}</strong> cuentas de acceso: {summary.managedCount} creadas desde este sistema y {summary.protectedCount} preexistentes y protegidas.</p>
    <p className="account-inventory-note">Las cuentas protegidas se muestran solo para que usted las vea. Desde el sistema no se pueden desactivar, eliminar ni cambiar.</p>
    {summary.protectedItems.length === 0 && <p>No hay cuentas preexistentes.</p>}
    <ul className="account-inventory-list">
      {summary.protectedItems.map(item => <li className="system-user-row account-inventory-row" key={item.subject}>
        <strong>{item.name || item.username}</strong>
        <span className="account-protected-badge">{PROTECTED_LABEL}</span>
        <small>Usuario: {item.username}</small>
        <small>{inventoryKindLabel(item)} · {inventoryStateLabel(item)}</small>
        <small>{inventoryOriginLabel(item)}</small>
        <small>Identificador de usuario: {item.subject}</small>
      </li>)}
    </ul>
  </section>
}
