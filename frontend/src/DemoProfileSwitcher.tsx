import type { DemoProfileKey } from './demoProfiles'
import './demoProfileSwitcher.css'

interface DemoProfileSwitcherProps {
  value: DemoProfileKey
  onChange: (value: DemoProfileKey) => void
}

export default function DemoProfileSwitcher({ value, onChange }: DemoProfileSwitcherProps) {
  return <label className="demo-profile-switcher">
    <span>Perfil QA</span>
    <select value={value} onChange={event => onChange(event.target.value as DemoProfileKey)} aria-label="Seleccionar perfil de demostración">
      <option value="brother">Hermano</option>
      <option value="lodge">Autoridad de Taller</option>
      <option value="grandLodge">Autoridad de Gran Logia</option>
    </select>
  </label>
}
