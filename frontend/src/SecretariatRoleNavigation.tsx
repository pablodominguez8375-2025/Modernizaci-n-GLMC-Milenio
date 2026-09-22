import './secretariatRoleNavigation.css'

export type SecretariatSection = {
  id: string
  label: string
  description: string
  pending?: number
}

export default function SecretariatRoleNavigation({ title, sections, active, onChange }: {
  title: string
  sections: SecretariatSection[]
  active: string
  onChange: (id: string) => void
}) {
  return <section className="secretariat-role-navigation" aria-label={title}>
    <div className="secretariat-role-navigation-heading">
      <div><small>Espacio operativo por cargo</small><h2>{title}</h2></div>
      <span>{sections.length} funciones</span>
    </div>
    <div className="secretariat-role-tabs">
      {sections.map(section => <button key={section.id} type="button" className={active === section.id ? 'active' : ''} aria-current={active === section.id ? 'page' : undefined} onClick={() => onChange(section.id)}>
        <strong>{section.label}{section.pending ? <em>{section.pending}</em> : null}</strong>
        <small>{section.description}</small>
      </button>)}
    </div>
  </section>
}
