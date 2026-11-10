// @vitest-environment jsdom
import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { readFileSync } from 'node:fs'
import { afterEach, describe, expect, it } from 'vitest'
import WorkshopRegistryPanel from './WorkshopRegistryPanel'
import { PmgmApiClient } from './api/pmgmApi'
import { availableWorkshopActions, workshopRegistryCsv, workshopRequestValid, type WorkshopRegistryItem } from './api/workshopRegistry'

;(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true

/** Sistema › Talleres (PO 09-11-2026, opciones A y A): ver y descargar Máster y superusuarios;
    eliminar solo el Máster y solo sin datos; con datos, «Dar de baja». */
const base: WorkshopRegistryItem = { id: 'a', shortCode: 'AAAAAAAA', number: '230', name: 'Taller X', type: 'workshop', orienteCode: null, city: null, createdAtUtc: '2026-11-08T00:00:00Z', origin: 'initial_load', status: 'active', hasData: false, protected: false }
const policy = { reasonMinLength: 10, reasonMaxLength: 500 }

describe('reglas del registro de Talleres', () => {
  it('ofrece eliminar solo sin datos, dar de baja con datos, reactivar si está de baja y nada si es protegido o no es el Máster', () => {
    expect(availableWorkshopActions(base, true)).toEqual(['delete'])
    expect(availableWorkshopActions({ ...base, hasData: true }, true)).toEqual(['retire'])
    expect(availableWorkshopActions({ ...base, status: 'retired', hasData: true }, true)).toEqual(['reactivate'])
    expect(availableWorkshopActions({ ...base, protected: true }, true)).toEqual([])
    expect(availableWorkshopActions(base, false)).toEqual([])
  })
  it('exige motivo de 10 a 500 caracteres y escribir el número del Taller para eliminar o dar de baja', () => {
    expect(workshopRequestValid('delete', base, { reason: 'Creado por error', confirmation: '230' }, policy)).toBe(true)
    expect(workshopRequestValid('delete', base, { reason: 'corto', confirmation: '230' }, policy)).toBe(false)
    expect(workshopRequestValid('delete', base, { reason: 'Creado por error', confirmation: '23' }, policy)).toBe(false)
    expect(workshopRequestValid('reactivate', base, { reason: 'Vuelve a trabajar' }, policy)).toBe(true)
  })
  it('el CSV usa «;», BOM, no expone conteos y neutraliza fórmulas', () => {
    const csv = workshopRegistryCsv([{ ...base, name: '=HYPERLINK("x")' }])
    expect(csv.startsWith('﻿Código interno;Número;Nombre')).toBe(true)
    expect(csv).toContain(`"'=HYPERLINK(""x"")"`)
    expect(csv).toContain('Tiene datos asociados')
    expect(csv).not.toMatch(/hermanos/i)
  })
  it('el cliente principal conserva los miembros que usa el registro de Talleres', () => {
    const source = readFileSync('src/api/pmgmApi.ts', 'utf8')
    for (const member of ['readonly useMocks', 'demoAccessSubject', 'readonly baseUrl', 'readonly getAccessToken', 'readonly onUnauthorized', 'private async request<T>', 'private postJson<T>']) expect(source).toContain(member)
  })
  it('la sección «Talleres» solo aparece para quien gestiona perfiles (Máster y superusuarios)', () => {
    const source = readFileSync('src/SystemConfigurationPage.tsx', 'utf8')
    expect(source).toContain("{id:'workshops',label:'Talleres',icon:'lodge' as const}")
    expect(source).toContain("t.id!=='workshops'")
  })
})

let root: Root | null = null
let host: HTMLElement | null = null
afterEach(() => { act(() => root?.unmount()); document.body.innerHTML = ''; root = null; host = null })
const flush = async () => { for (let i = 0; i < 4; i++) await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) }) }
async function render(subject: string) {
  const api = new PmgmApiClient({ useMocks: true }); api.demoAccessSubject = subject
  host = document.createElement('div'); document.body.appendChild(host); root = createRoot(host)
  await act(async () => { root!.render(<WorkshopRegistryPanel api={api} />) })
  await flush()
  return { api, view: host }
}
const buttons = (label: string) => [...document.querySelectorAll('button')].filter(button => button.textContent === label)
function type(input: HTMLInputElement, value: string) {
  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, value)
  input.dispatchEvent(new Event('input', { bubbles: true }))
}

describe('Sistema › Talleres en la demo', () => {
  it('el superusuario ve y descarga, sin acciones de eliminar ni dar de baja', async () => {
    const { view } = await render('demo:supportUser')
    expect(view.textContent).toContain('Talleres registrados')
    expect(view.textContent).toContain('Nº 230 · Taller Demostrativo Nº 230')
    expect(buttons('Descargar CSV')).toHaveLength(1)
    expect(buttons('Eliminar')).toHaveLength(0)
    expect(buttons('Dar de baja')).toHaveLength(0)
    expect(view.textContent).toContain('lo hace solo el Máster')
  })

  it('el Máster elimina el Taller sin datos tras motivo y número; los que tienen datos solo se dan de baja', async () => {
    const { view } = await render('demo:systemAdmin')
    expect(buttons('Eliminar')).toHaveLength(1)
    expect(buttons('Dar de baja').length).toBeGreaterThan(1)
    expect(view.textContent).toContain('Gran Logia Mixta de Chile')
    await act(async () => { buttons('Eliminar')[0].click() })
    const [reason, confirm] = [...document.querySelectorAll<HTMLInputElement>('form input')]
    const submit = () => [...document.querySelectorAll<HTMLButtonElement>('form button')].find(button => button.textContent === 'Eliminar')!
    await act(async () => { type(reason, 'Creado por error en la Carga inicial'); type(confirm, '23') })
    expect(submit().disabled).toBe(true)
    await act(async () => { type(confirm, '230') })
    expect(submit().disabled).toBe(false)
    await act(async () => { submit().click() })
    expect(document.body.textContent).toContain('Revise antes de confirmar')
    await act(async () => { buttons('Confirmar')[0].click() }); await flush()
    expect(view.textContent).toContain('Se eliminó el Taller Nº 230 · Taller Demostrativo Nº 230.')
    expect(buttons('Eliminar')).toHaveLength(0)
  })
})
