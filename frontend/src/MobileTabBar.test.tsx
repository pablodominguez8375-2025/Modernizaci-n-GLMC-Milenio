import { type ReactElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import MobileTabBar, { type MobileTabBarProps, type MobileTabId } from './MobileTabBar'

function harness(active: MobileTabId, operational = false) {
  const props: MobileTabBarProps = {
    active, menuOpen: false, operational, pendingCount: 2, unreadCount: 5,
    onHome: vi.fn(), onSecond: vi.fn(), onCalendar: vi.fn(), onNotifications: vi.fn(),
    onToggleMenu: vi.fn(() => { props.menuOpen = !props.menuOpen }),
  }
  const click = (index: number) => {
    const nav = MobileTabBar(props)
    const children = nav.props.children as ReactElement<{ onClick: () => void }>[]
    children[index].props.onClick()
  }
  return { props, click }
}

describe('navegación inferior móvil', () => {
  it('cierra Menú al volver a Inicio aunque Inicio ya esté activo', () => {
    const { props, click } = harness('home')
    click(4)
    expect(props.menuOpen).toBe(true)
    click(0)
    expect(props.onHome).toHaveBeenCalledOnce()
    expect(props.active).toBe('home')
    expect(props.menuOpen).toBe(false)
    const html = renderToStaticMarkup(<MobileTabBar {...props} />)
    expect(html).toContain('data-tab="home" aria-current="page"')
    expect(html).toContain('aria-expanded="false"')
  })

  it.each([
    ['home', 0, 'onHome', false],
    ['second', 1, 'onSecond', false],
    ['second', 1, 'onSecond', true],
    ['calendar', 2, 'onCalendar', false],
    ['notifications', 3, 'onNotifications', false],
  ] as const)('cierra el menú al pulsar la opción activa %s (índice: %s, callback: %s, operativo: %s)', (active, index, callback, operational) => {
    const { props, click } = harness(active, operational)
    click(4)
    click(index)
    expect(props.menuOpen).toBe(false)
    expect(props[callback]).toHaveBeenCalledOnce()
    vi.mocked(props.onToggleMenu).mockClear()
    click(index)
    expect(props.menuOpen).toBe(false)
    expect(props.onToggleMenu).not.toHaveBeenCalled()
    expect(props[callback]).toHaveBeenCalledTimes(2)
  })
})
