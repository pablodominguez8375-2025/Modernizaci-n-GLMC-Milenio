import { useEffect, type DependencyList, type RefObject } from 'react'

/* PMGM-UX menús simples (aprobado por el PO 03-10-2026; ajustado por el PO 07-10-2026).
   En PC todos los grupos del menú lateral, incluido «Mi espacio», se pueden plegar y abrir con un clic, y se recuerda lo que cada persona dejó cerrado.
   El grupo de la vista actual queda siempre abierto. Si el menú no cabe en la pantalla y la persona aún no eligió nada, se parte con los últimos grupos plegados.
   En celular y tablet no aplica (allí el menú es una hoja de tarjetas). */
const STORAGE_KEY = 'pmgm.navCollapsedGroups'
const DESKTOP_QUERY = '(min-width: 981px)'

type NavGroup = { name: string; header: HTMLElement; items: HTMLElement[] }

export function navGroupsOf(nav: HTMLElement): NavGroup[] {
  const groups: NavGroup[] = []
  let current: NavGroup | null = null
  for (const element of Array.from(nav.children) as HTMLElement[]) {
    if (element.classList.contains('nav-section')) {
      current = { name: element.textContent?.trim() ?? '', header: element, items: [] }
      groups.push(current)
    } else if (current && element.classList.contains('nav-item')) {
      current.items.push(element)
    }
  }
  return groups
}

/** Alto real del contenido del menú (el menú lateral se estira con la página, así que scrollHeight no sirve). */
function contentHeight(nav: HTMLElement) {
  const top = nav.getBoundingClientRect().top
  const bottoms = (Array.from(nav.children) as HTMLElement[]).filter(child => child.offsetParent !== null).map(child => child.getBoundingClientRect().bottom)
  return bottoms.length ? Math.max(...bottoms) - top + 24 : 0
}

function readCollapsed(): string[] | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY)
    return value ? JSON.parse(value) as string[] : null
  } catch {
    return null
  }
}

function saveCollapsed(names: string[]) {
  try { window.localStorage.setItem(STORAGE_KEY, JSON.stringify(names)) } catch { /* navegador sin almacenamiento */ }
}

export function useCollapsibleNavGroups(ref: RefObject<HTMLElement | null>, deps: DependencyList) {
  useEffect(() => {
    const nav = ref.current
    if (!nav) return
    const apply = () => {
      const groups = navGroupsOf(nav)
      groups.forEach(group => {
        group.items.forEach(item => { item.hidden = false })
        group.header.removeAttribute('role')
        group.header.removeAttribute('tabindex')
        group.header.removeAttribute('aria-expanded')
      })
      nav.classList.remove('has-collapsible-groups')
      if (!window.matchMedia(DESKTOP_QUERY).matches || nav.closest('.is-sidebar-collapsed')) return
      const top = nav.getBoundingClientRect().top + window.scrollY
      const available = window.innerHeight - top
      nav.classList.add('has-collapsible-groups')
      const stored = readCollapsed()
      const setGroup = (group: NavGroup, collapsed: boolean) => {
        group.header.setAttribute('role', 'button')
        group.header.tabIndex = 0
        group.header.setAttribute('aria-expanded', String(!collapsed))
        group.items.forEach(item => { item.hidden = collapsed })
      }
      const canCollapse = (group: NavGroup) => !group.items.some(item => item.classList.contains('active'))
      if (stored) {
        groups.forEach(group => setGroup(group, canCollapse(group) && stored.includes(group.name)))
      } else {
        /* Sin preferencia guardada: se pliegan los grupos desde el final, solo los necesarios para que el menú quepa. */
        groups.forEach(group => setGroup(group, false))
        for (const group of [...groups].reverse()) {
          if (contentHeight(nav) <= available) break
          if (canCollapse(group)) setGroup(group, true)
        }
      }
    }
    const toggle = (header: HTMLElement) => {
      const groups = navGroupsOf(nav)
      const group = groups.find(entry => entry.header === header)
      if (!group) return
      const expand = header.getAttribute('aria-expanded') !== 'true'
      header.setAttribute('aria-expanded', String(expand))
      group.items.forEach(item => { item.hidden = !expand })
      saveCollapsed(groups.filter(entry => entry.header.getAttribute('aria-expanded') === 'false').map(entry => entry.name))
    }
    const onClick = (event: MouseEvent) => {
      const header = (event.target as HTMLElement).closest<HTMLElement>('.nav-section[role="button"]')
      if (header) toggle(header)
    }
    const onKey = (event: KeyboardEvent) => {
      const header = (event.target as HTMLElement).closest<HTMLElement>('.nav-section[role="button"]')
      if (header && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); toggle(header) }
    }
    apply()
    /* Si el menú se vuelve a dibujar (por ejemplo al cambiar de perfil, cuando llegan los permisos), se aplica de nuevo:
       así todos los grupos quedan plegables y el plegado automático mide el menú completo. */
    let frame = 0
    const observer = new MutationObserver(() => {
      window.cancelAnimationFrame(frame)
      frame = window.requestAnimationFrame(apply)
    })
    observer.observe(nav, { childList: true })
    nav.addEventListener('click', onClick)
    nav.addEventListener('keydown', onKey)
    window.addEventListener('resize', apply)
    window.addEventListener('pmgm:text-size', apply)
    return () => {
      observer.disconnect()
      window.cancelAnimationFrame(frame)
      nav.removeEventListener('click', onClick)
      nav.removeEventListener('keydown', onKey)
      window.removeEventListener('resize', apply)
      window.removeEventListener('pmgm:text-size', apply)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
}
