import type { ReactNode } from 'react'
import { WorkspaceTabs } from './actionKit'
import type { InstitutionalIconName } from './InstitutionalIcon'

export interface TreasuryRoleSection {
  id: string
  label: string
  description: string
  icon: InstitutionalIconName
  badge?: number
}

export default function TreasuryRoleNavigation({ title, sections, active, onChange, actions }: {
  title: string
  sections: TreasuryRoleSection[]
  active: string
  onChange: (id: string) => void
  actions?: ReactNode
}) {
  return <>
    <section className="treasury-role-navigation" aria-label={`Presentación de ${title}`}>
      <div className="treasury-role-navigation-heading">
        <div><p className="eyebrow">Menú operativo</p><h2>{title}</h2></div>
        {actions}
      </div>
    </section>
    <WorkspaceTabs
      label={`Secciones de ${title}`}
      active={active}
      onChange={onChange}
      hub={{ title, hints: Object.fromEntries(sections.map(section => [section.id, section.description])) }}
      tabs={sections.map(({ id, label, icon, badge }) => ({ id, label, icon, badge }))}
    />
  </>
}
