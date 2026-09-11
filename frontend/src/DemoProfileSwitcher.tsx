import type { DemoProfileKey } from './demoProfiles'
import './demoProfileSwitcher.css'

interface DemoProfileSwitcherProps {
  value: DemoProfileKey
  onChange: (value: DemoProfileKey) => void
}

export default function DemoProfileSwitcher({ value, onChange }: DemoProfileSwitcherProps) {
  const showcaseSha = (import.meta.env.VITE_SHOWCASE_SHA ?? '').trim()
  const shortSha = showcaseSha ? showcaseSha.slice(0, 7) : ''

  return <label
    className="demo-profile-switcher"
    data-showcase-sha={showcaseSha || undefined}
    title="Demo pública de Proyecto Centenario. Usa exclusivamente datos ficticios y no está conectada a la VM institucional."
  >
    <span className="demo-public-label">
      <span className="demo-public-long">Demo pública · datos ficticios</span>
      <span className="demo-public-short">Demo</span>
      {shortSha && <span className="demo-public-sha"> · {shortSha}</span>}
    </span>
    <span className="demo-profile-label">Perfil QA</span>
    <select value={value} onChange={event => onChange(event.target.value as DemoProfileKey)} aria-label="Seleccionar perfil de demostración">
      <option value="brother">Hermano</option>
      <option value="lodge">Autoridad de Taller</option>
      <option value="grandLodge">Autoridad de Gran Logia</option>
    </select>
  </label>
}
