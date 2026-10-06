import { useEffect, useState } from 'react'
import type { PmgmApiClient, TreasuryTerritory, TreasuryTerritoryOption } from './api/pmgmApi'
import { organizationDisplayName } from './displayFormat'
import './treasuryTerritory.css'
const labels: Record<TreasuryTerritory, string> = { santiago: 'Santiago', other_oriente: 'Regiones de Chile (otros Orientes)', peru: 'Perú (USD)' }
export default function TreasuryTerritoryPage({ api }: { api: PmgmApiClient }) {
  const [items, setItems] = useState<TreasuryTerritoryOption[]>([])
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    void api.getTreasuryTerritories().then(result => { if (active) setItems(result.items.filter(x => x.type === 'workshop')) }).catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No se pudieron cargar las Fichas.') })
    return () => { active = false }
  }, [api])
  return <section className="panel treasury-territory-panel"><h2>Orientes desde la Ficha del Taller</h2><p>Consulta de solo lectura. La zona de cuotas proviene del Oriente, ciudad y país de la Ficha. Las correcciones requieren el permiso de edición de Ficha.</p>{error && <p role="alert">{error}</p>}<div className="treasury-territory-list">{items.map(item => <article className="treasury-territory-card" key={item.id}><h3>{organizationDisplayName(item.name, item.number)}</h3><dl><div><dt>Oriente de la Ficha</dt><dd>{item.city || 'Sin registrar'} · {item.country || 'Sin registrar'}</dd></div><div><dt>Zona tarifaria</dt><dd>{item.treasuryTerritory ? labels[item.treasuryTerritory] : 'Ficha incompleta o inconsistente'}</dd></div></dl><a className="secondary-action" href={`${import.meta.env.BASE_URL}?taller=${encodeURIComponent(item.id)}#ficha-del-taller`}>Corregir en la Ficha del Taller</a></article>)}</div></section>
}
