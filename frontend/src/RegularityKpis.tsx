export default function RegularityKpis({ total, good, attention, missing, loading, kind }: { total: number; good: number; attention: number; missing: number; loading: boolean; kind: 'treasury' | 'hospitalaria' }) {
  const value = (n: number) => loading ? '—' : n
  const attentionLabel = kind === 'treasury' ? 'Morosidad / revisión' : 'Reposiciones / revisión'
  return <div className="regularity-kpis">
    <Kpi label="Talleres visibles" value={value(total)} tone="navy" />
    <Kpi label="Al día / exentos" value={value(good)} tone="good" />
    <Kpi label={attentionLabel} value={value(attention)} tone="attention" />
    <Kpi label="Sin estado" value={value(missing)} tone="missing" />
  </div>
}

function Kpi({ label, value, tone }: { label: string; value: string | number; tone: string }) {
  return <article className={`regularity-kpi ${tone}`}><span>{label}</span><strong>{value}</strong></article>
}
