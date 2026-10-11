import { WorkspaceTabs } from './actionKit'
import type { InstitutionalIconName } from './InstitutionalIcon'

export interface TreasuryRoleSection {
  id: string
  label: string
  description: string
  icon: InstitutionalIconName
  badge?: number
}

export default function TreasuryRoleNavigation({ title, sections, active, onChange }: {
  title: string
  sections: TreasuryRoleSection[]
  active: string
  onChange: (id: string) => void
}) {
  return <WorkspaceTabs
    label={`Secciones de ${title}`}
    active={active}
    onChange={onChange}
    hub={{ title, hints: Object.fromEntries(sections.map(section => [section.id, section.description])) }}
    tabs={sections.map(({ id, label, icon, badge }) => ({ id, label, icon, badge }))}
  />
}
