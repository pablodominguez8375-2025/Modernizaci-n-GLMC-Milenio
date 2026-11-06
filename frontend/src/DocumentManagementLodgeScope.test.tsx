// @vitest-environment jsdom
import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { afterEach, describe, expect, it } from 'vitest'
import DocumentManagementPage from './DocumentManagementPage'
import { DocumentApiClient } from './api/documentApi'
import { PmgmApiClient } from './api/pmgmApi'

;(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true

/** Opción A (PO 05-11-2026): la Secretaría del Taller guarda documentos «Solo Taller» o «Solo gestión»
    y no publica en la Biblioteca Virtual; esa publicación la aprueba el Bibliotecario. */
let root: Root | null = null
let host: HTMLElement | null = null
afterEach(() => { act(() => root?.unmount()); host?.remove(); root = null; host = null })

async function render(subject: string) {
  const api = new PmgmApiClient({ useMocks: true }); api.demoAccessSubject = subject
  const documentApi = new DocumentApiClient({ useMocks: true })
  host = document.createElement('div'); document.body.appendChild(host); root = createRoot(host)
  await act(async () => { root!.render(<DocumentManagementPage api={api} documentApi={documentApi} />) })
  for (let i = 0; i < 5; i++) await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
  return host
}

describe('Gestor Documental · alcance de Taller (opción A)', () => {
  it('la Secretaría del Taller no ve «Publicar» y se le explica que aprueba el Bibliotecario', async () => {
    const view = await render('demo:lodgeSecretary')
    expect(view.textContent).toContain('Acta de Tenida ordinaria · ejemplo')
    expect(view.textContent).toContain('v1 · acta-tenida-ejemplo.pdf')
    expect([...view.querySelectorAll('button')].some(button => button.textContent === 'Publicar')).toBe(false)
    expect(view.textContent).toContain('La publicación en la Biblioteca la aprueba el Bibliotecario')
  })

  it('un perfil de alcance Orden conserva «Publicar» sobre una versión disponible', async () => {
    const view = await render('demo:secretariat')
    const select = [...view.querySelectorAll<HTMLSelectElement>('select')].find(item => [...item.options].some(option => option.value === 'dddddddd-2222-2222-2222-222222222222'))!
    await act(async () => { select.value = 'dddddddd-2222-2222-2222-222222222222'; select.dispatchEvent(new Event('change', { bubbles: true })) })
    for (let i = 0; i < 5; i++) await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
    expect([...view.querySelectorAll('button')].some(button => button.textContent === 'Publicar')).toBe(true)
  })

  it('el formulario de la Secretaría no ofrece «Biblioteca · autenticados» y parte en «Sólo Taller»', async () => {
    const view = await render('demo:lodgeSecretary')
    const open = [...view.querySelectorAll('button')].find(button => button.textContent?.includes('Registrar documento'))!
    await act(async () => { open.click() })
    const policy = [...document.querySelectorAll<HTMLSelectElement>('select')].find(item => [...item.options].some(option => option.value === 'management_only'))!
    expect([...policy.options].map(option => option.value)).toEqual(['organization_authenticated', 'management_only'])
    expect(policy.value).toBe('organization_authenticated')
  })
})
