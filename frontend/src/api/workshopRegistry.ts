/** Registro de Talleres para el Máster y los superusuarios de soporte (decisión del PO 09-11-2026, opciones A y A).
    Contrato de servidor para ChatGPT en el Issue «Sistema › Talleres». La UI nunca decide permisos: usa `canManage` y `hasData` del servidor.
    - Ver y descargar: Máster y superusuarios.
    - Eliminar: solo el Máster y solo si el Taller no tiene datos asociados.
    - Si tiene datos: solo el Máster puede «Dar de baja» (queda oculto y sin operación, con su historia intacta) y «Reactivar».
    - La Gran Logia y el «Taller 00 · Soporte» nunca se eliminan ni se dan de baja (`protected`). */
export type WorkshopRegistryOrigin = 'initial_load' | 'qa_seed' | 'bootstrap' | 'other' | string
export type WorkshopRegistryStatus = 'active' | 'retired'
export type WorkshopRegistryAction = 'delete' | 'retire' | 'reactivate'

export interface WorkshopRegistryItem {
  id: string
  /** Código interno corto (primeros 8 caracteres del identificador, en mayúsculas). */
  shortCode: string
  number: string | null
  name: string
  type: 'workshop' | 'grand_lodge' | string
  orienteCode: string | null
  city: string | null
  createdAtUtc: string
  origin: WorkshopRegistryOrigin
  status: WorkshopRegistryStatus
  retiredAtUtc?: string | null
  /** Solo indica si hay datos asociados (hermanos, cargos, Tesorería, Hospitalaria, actas, documentos); nunca cuántos (Ley 21.719). */
  hasData: boolean
  protected: boolean
}
export interface WorkshopRegistry { items: WorkshopRegistryItem[]; canManage: boolean; reasonMinLength: number; reasonMaxLength: number }
export interface WorkshopRegistryRequest { reason: string; confirmation?: string }
export interface WorkshopRegistryResult { id: string; action: WorkshopRegistryAction; completed: boolean }

export const WORKSHOP_ACTION_LABELS: Record<WorkshopRegistryAction, string> = { delete: 'Eliminar', retire: 'Dar de baja', reactivate: 'Reactivar' }
export const WORKSHOP_ORIGIN_LABELS: Record<string, string> = { initial_load: 'Carga inicial', qa_seed: 'Prueba (creado por el sistema QA)', bootstrap: 'Configuración inicial', other: 'Otro' }
export const workshopOriginLabel = (origin: string) => WORKSHOP_ORIGIN_LABELS[origin] ?? WORKSHOP_ORIGIN_LABELS.other
export const workshopLabel = (item: Pick<WorkshopRegistryItem, 'name' | 'number' | 'type'>) => item.type === 'grand_lodge' || !item.number ? item.name : `Nº ${item.number} · ${item.name}`
/** Texto que se escribe para confirmar: el número del Taller (o su código corto si no tiene número). */
export const workshopConfirmationText = (item: Pick<WorkshopRegistryItem, 'number' | 'shortCode'>) => item.number ?? item.shortCode

/** Acciones que la UI ofrece; el servidor vuelve a validar todo. */
export function availableWorkshopActions(item: WorkshopRegistryItem, canManage: boolean): WorkshopRegistryAction[] {
  if (!canManage || item.protected) return []
  if (item.status === 'retired') return ['reactivate']
  return item.hasData ? ['retire'] : ['delete']
}

export function workshopActionDescription(action: WorkshopRegistryAction, item: WorkshopRegistryItem): string {
  const label = workshopLabel(item)
  if (action === 'delete') return `Se eliminará el Taller ${label}. Solo es posible porque no tiene hermanos, cargos, movimientos, actas ni documentos asociados. No se puede deshacer.`
  if (action === 'retire') return `El Taller ${label} tiene datos asociados, por eso no se elimina: se da de baja. Queda oculto y sin operación, y toda su historia se conserva. Se puede reactivar.`
  return `El Taller ${label} vuelve a quedar activo y visible.`
}

export function workshopRequestValid(action: WorkshopRegistryAction, item: WorkshopRegistryItem, request: WorkshopRegistryRequest, policy: Pick<WorkshopRegistry, 'reasonMinLength' | 'reasonMaxLength'>): boolean {
  const reason = request.reason.trim()
  if (reason.length < policy.reasonMinLength || reason.length > policy.reasonMaxLength) return false
  return action === 'reactivate' || (request.confirmation ?? '').trim() === workshopConfirmationText(item)
}

const CSV_HEADER = ['Código interno', 'Número', 'Nombre', 'Tipo', 'Oriente', 'Ciudad', 'Origen', 'Estado', 'Tiene datos asociados', 'Creado (UTC)', 'Identificador']
const csvCell = (value: string) => { const safe = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value; return /[";\n\r]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe }
/** CSV de la demo con el mismo formato que debe entregar el servidor (separador «;», UTF-8 con BOM, celdas protegidas contra fórmulas). */
export function workshopRegistryCsv(items: WorkshopRegistryItem[]): string {
  const rows = items.map(item => [item.shortCode, item.number ?? '', item.name, item.type === 'grand_lodge' ? 'Gran Logia' : 'Taller', item.orienteCode ?? '', item.city ?? '', workshopOriginLabel(item.origin), item.status === 'retired' ? 'De baja' : 'Activo', item.hasData ? 'Sí' : 'No', item.createdAtUtc, item.id])
  return '﻿' + [CSV_HEADER, ...rows].map(row => row.map(csvCell).join(';')).join('\r\n') + '\r\n'
}

const shortCode = (id: string) => id.replace(/-/g, '').slice(0, 8).toUpperCase()
const demoItem = (id: string, number: string | null, name: string, extra: Partial<WorkshopRegistryItem> = {}): WorkshopRegistryItem =>
  ({ id, shortCode: shortCode(id), number, name, type: 'workshop', orienteCode: 'SCL', city: 'Santiago', createdAtUtc: '2026-10-18T13:00:00Z', origin: 'qa_seed', status: 'active', retiredAtUtc: null, hasData: true, protected: false, ...extra })

/** Demo (datos ficticios): el Máster gestiona; el superusuario solo ve y descarga. */
export class WorkshopRegistryDemo {
  private items: WorkshopRegistryItem[]
  constructor(private readonly isMaster: () => boolean, private readonly canRead: () => boolean) {
    const workshops = [1, ...Array.from({ length: 18 }, (_, index) => index + 2), 23].map(number => demoItem(
      number === 1 ? '11111111-1111-1111-1111-111111111111' : number === 23 ? '23232323-2323-2323-2323-232323232323' : `${(0x3a6b0000 + number * 0x1f3d1).toString(16)}-0000-4000-8000-${String(number).padStart(12, '0')}`,
      String(number), `Taller Demostrativo Nº ${number}`))
    this.items = [
      demoItem('9a9a9a9a-0000-4000-8000-000000000001', null, 'Gran Logia Mixta de Chile', { type: 'grand_lodge', protected: true }),
      ...workshops,
      demoItem('7c3e51b0-2300-4000-8000-000000000230', '230', 'Taller Demostrativo Nº 230', { origin: 'initial_load', hasData: false, createdAtUtc: '2026-11-08T22:14:00Z' }),
      demoItem('5d1f0a77-0024-4000-8000-000000000024', '24', 'Taller en receso (ejemplo)', { origin: 'initial_load', status: 'retired', retiredAtUtc: '2026-11-01T15:00:00Z' }),
    ]
  }
  private authorize() { if (!this.canRead()) throw new Error('Su cuenta no tiene permiso para ver el registro de Talleres.') }
  registry(): WorkshopRegistry { this.authorize(); return { items: structuredClone(this.items), canManage: this.isMaster(), reasonMinLength: 10, reasonMaxLength: 500 } }
  csv(): { blob: Blob; fileName: string } { this.authorize(); return { blob: new Blob([workshopRegistryCsv(this.items)], { type: 'text/csv;charset=utf-8' }), fileName: 'talleres-registrados.csv' } }
  run(id: string, action: WorkshopRegistryAction, request: WorkshopRegistryRequest): WorkshopRegistryResult {
    this.authorize()
    if (!this.isMaster()) throw new Error('Solo el Máster puede eliminar, dar de baja o reactivar Talleres.')
    const item = this.items.find(value => value.id === id)
    if (!item) throw new Error('El Taller ya no existe.')
    if (item.protected) throw new Error('La Gran Logia y el Taller de soporte no se pueden eliminar ni dar de baja.')
    if (!workshopRequestValid(action, item, request, { reasonMinLength: 10, reasonMaxLength: 500 })) throw new Error('Revise el motivo (10 a 500 caracteres) y escriba el número del Taller tal como aparece.')
    if (action === 'delete') { if (item.hasData || item.status !== 'active') throw new Error('Este Taller tiene datos asociados: no se puede eliminar, solo dar de baja.'); this.items = this.items.filter(value => value.id !== id) }
    else if (action === 'retire') { if (item.status !== 'active') throw new Error('El Taller ya está de baja.'); item.status = 'retired'; item.retiredAtUtc = new Date().toISOString() }
    else { if (item.status !== 'retired') throw new Error('El Taller ya está activo.'); item.status = 'active'; item.retiredAtUtc = null }
    return { id, action, completed: true }
  }
}

/** Miembros del cliente principal (PmgmApiClient) que usa este módulo cargado bajo demanda. Si cambian sus nombres, fallan las pruebas de WorkshopRegistryPanel. */
export interface WorkshopRegistryClient {
  useMocks: boolean
  demoAccessSubject: string
  baseUrl: string
  getAccessToken?: () => Promise<string | null | undefined>
  onUnauthorized?: () => Promise<void> | void
  request<T>(path: string): Promise<T>
  postJson<T>(path: string, body: unknown): Promise<T>
}
export interface WorkshopRegistryApi {
  registry(): Promise<WorkshopRegistry>
  run(id: string, action: WorkshopRegistryAction, request: WorkshopRegistryRequest): Promise<WorkshopRegistryResult>
  downloadCsv(): Promise<{ blob: Blob; fileName: string }>
}
/** Contrato de servidor (Issue para ChatGPT): GET /api/system/workshops · POST /api/system/workshops/{id}/{delete|retire|reactivate} · GET /api/system/workshops/export.csv */
export function createWorkshopRegistryApi(client: WorkshopRegistryClient): WorkshopRegistryApi {
  if (client.useMocks) {
    const demo = new WorkshopRegistryDemo(() => client.demoAccessSubject === 'demo:systemAdmin', () => client.demoAccessSubject === 'demo:systemAdmin' || client.demoAccessSubject === 'demo:supportUser')
    return { registry: async () => demo.registry(), run: async (id, action, request) => demo.run(id, action, request), downloadCsv: async () => demo.csv() }
  }
  return {
    registry: () => client.request<WorkshopRegistry>('/api/system/workshops'),
    run: (id, action, request) => client.postJson<WorkshopRegistryResult>(`/api/system/workshops/${encodeURIComponent(id)}/${action}`, request),
    async downloadCsv() {
      const token = await client.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para descargar el registro de Talleres.')
      const response = await fetch(`${client.baseUrl}/api/system/workshops/export.csv`, { credentials: 'omit', redirect: 'error', cache: 'no-store', headers: new Headers({ Authorization: `Bearer ${token}` }) })
      if (!response.ok) {
        if (response.status === 401) await client.onUnauthorized?.()
        throw new Error(response.status === 403 ? 'Su cuenta no tiene permiso para descargar el registro de Talleres.' : response.status === 404 ? 'La descarga del registro de Talleres aún no está disponible en este servidor.' : `La API respondió ${response.status}.`)
      }
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(response.headers.get('Content-Disposition') ?? '')
      return { blob: await response.blob(), fileName: match ? decodeURIComponent(match[1]) : 'talleres-registrados.csv' }
    },
  }
}
