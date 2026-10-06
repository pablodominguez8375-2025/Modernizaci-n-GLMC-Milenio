import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'

/** PMGM-UX-002 · Barra inferior en móvil y tablet (≤980 px, PMGM-UX-004). En pantallas mayores se oculta por CSS. */
export type MobileTabId = 'home' | 'second' | 'calendar' | 'notifications' | 'menu'

export interface MobileTabBarProps {
  active: MobileTabId | null
  menuOpen: boolean
  operational: boolean
  pendingCount: number
  unreadCount: number
  onHome: () => void
  onSecond: () => void
  onCalendar: () => void
  onNotifications: () => void
  onToggleMenu: () => void
}

export default function MobileTabBar(props: MobileTabBarProps) {
  const second = props.operational
    ? { label: 'Pendientes', icon: 'tasks' as const, badge: props.pendingCount }
    : { label: 'Mi ficha', icon: 'member' as const, badge: 0 }
  const navigate = (action: () => void) => {
    if (props.menuOpen) props.onToggleMenu()
    action()
  }
  return <nav className="mobile-tabbar" aria-label="Navegación rápida">
    <Tab id="home" label="Inicio" icon="home" active={!props.menuOpen && props.active === 'home'} onClick={() => navigate(props.onHome)} />
    <Tab id="second" label={second.label} icon={second.icon} badge={second.badge} active={!props.menuOpen && props.active === 'second'} onClick={() => navigate(props.onSecond)} />
    <Tab id="calendar" label="Agenda" icon="calendar" active={!props.menuOpen && props.active === 'calendar'} onClick={() => navigate(props.onCalendar)} />
    <Tab id="notifications" label="Avisos" icon="bell" badge={props.unreadCount} active={!props.menuOpen && props.active === 'notifications'} onClick={() => navigate(props.onNotifications)} />
    <button className={props.menuOpen ? 'mobile-tab active' : 'mobile-tab'} type="button" data-tab="menu" aria-expanded={props.menuOpen} aria-controls="navegacion-principal" onClick={props.onToggleMenu}>
      <InstitutionalIcon name="menu" size={22} />
      <span>{props.menuOpen ? 'Cerrar' : 'Menú'}</span>
    </button>
  </nav>
}

function Tab({ id, label, icon, badge = 0, active, onClick }: { id: MobileTabId; label: string; icon: InstitutionalIconName; badge?: number; active: boolean; onClick: () => void }) {
  return <button className={active ? 'mobile-tab active' : 'mobile-tab'} type="button" data-tab={id} aria-current={active ? 'page' : undefined} onClick={onClick}>
    <InstitutionalIcon name={icon} size={22} />
    <span>{label}</span>
    {badge > 0 && <em aria-label={`${badge} pendientes`}>{badge > 99 ? '99+' : badge}</em>}
  </button>
}
