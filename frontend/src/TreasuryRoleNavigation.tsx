import type { ReactNode } from 'react'

export interface TreasuryRoleSection {
  id: string
  label: string
  description: string
  badge?: string
}

export default function TreasuryRoleNavigation({ title, sections, active, onChange, actions }: {
  title: string
  sections: TreasuryRoleSection[]
  active: string
  onChange: (id: string) => void
  actions?: ReactNode
}) {
  return <section className="treasury-role-navigation" aria-label={title}>
    <div className="treasury-role-navigation-heading">
      <div><p className="eyebrow">Menú operativo</p><h2>{title}</h2></div>
      {actions}
    </div>
    <div className="treasury-role-tabs" role="tablist" aria-label={title}>
      {sections.map(section => <button
        key={section.id}
        type="button"
        role="tab"
        aria-selected={active === section.id}
        className={active === section.id ? 'active' : ''}
        onClick={() => onChange(section.id)}
      >
        <span>{section.label}{section.badge && <em>{section.badge}</em>}</span>
        <small>{section.description}</small>
      </button>)}
    </div>
  </section>
}
