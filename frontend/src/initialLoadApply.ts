/** #694 — Aplicar / abrir / cerrar la carga inicial y relectura. Solo el Máster; el servidor decide todo. Nunca se reproduce el contenido de las celdas. */
import type { BootstrapApiClient } from './api/bootstrapApi'
import type { InitialLoadFinding, InitialLoadPreviewResult } from './initialLoadPreview'

export const MIN_REASON = 10
export const MAX_REASON = 500

export interface InitialLoadApplyCounts { workshopsCreated: number; membersCreated: number; memberExactSkips: number; officesCreated: number; officeExactSkips: number }
export interface InitialLoadApplyResult { applied: boolean; alreadyApplied: boolean; code: string; counts: InitialLoadApplyCounts }
export interface InitialLoadWindowTransition { status: string; version: number; code: string }
export interface InitialLoadReadBack {
  windowStatus: string; workshops: number; members: number; memberships: number; officeAssignments: number
  batches: { status: string; count: number }[]
}

const blockerTexts: Record<string, string> = {
  ORIENTE_REQUIRED_FOR_APPLY: 'El Taller necesita su Oriente para poder cargarse',
  STATUS_NOT_APPLICABLE: 'Solo se pueden cargar Hermanos con estado «Activo»',
  IDENTITY_TYPE_UNSUPPORTED: 'Solo se aceptan Hermanos identificados con RUT',
  PLAN_NOT_BUILDABLE: 'Con estos archivos no se puede armar la carga',
  PREFLIGHT_INVALID: 'Primero hay que corregir los problemas de la revisión',
  SHEETS_REQUIRED: 'Faltan archivos',
  ONE_WORKSHOP_PER_BATCH_REQUIRED: 'Cada carga debe traer un solo Taller',
  MEMBER_BATCH_LIMIT_30: 'Cada carga admite como máximo 30 Hermanos; divida la nómina en varias cargas',
  CROSS_WORKSHOP_BATCH: 'Hay filas de otro Taller dentro de la misma carga',
}
export function describeBlocker(code: string): string { return blockerTexts[code] ?? 'Este dato impide cargar el lote' }

const applyErrors: Record<string, string> = {
  WINDOW_NOT_OPEN: 'La ventana de carga no está abierta. Ábrala primero.',
  CONCURRENT_OR_CONFLICTING_BATCH: 'Hay otra carga en curso o que choca con esta. No se guardó nada; intente de nuevo.',
  INVALID_RECEIPT: 'La revisión ya no corresponde a estos archivos. Vuelva a revisar los archivos.',
  RECEIPT_EXPIRED: 'La revisión venció (dura 15 minutos). Vuelva a revisar los archivos.',
  BATCH_NOT_APPLICABLE: 'Los archivos no cumplen las condiciones para cargarse. No se guardó nada.',
  MEMBERS_INVALID: 'Hay datos de Hermanos que impiden la carga. No se guardó nada.',
  OFFICES_INVALID: 'Hay datos de Cargos que impiden la carga. No se guardó nada.',
  MEMBER_CONFLICT_EXISTING_PERSON: 'Una persona ya existe con datos distintos. No se guardó nada.',
  WORKSHOP_RESOLUTION_AMBIGUOUS: 'No se pudo identificar el Taller de forma única. No se guardó nada.',
  OFFICE_CODE_NOT_EXACT: 'Un cargo no coincide con el catálogo de cargos. No se guardó nada.',
}
export function describeApplyCode(code: string): string { return applyErrors[code] ?? 'La carga no se aplicó y no se guardó nada.' }

/** Resultado NO confirmado (5xx, 503, respuesta perdida, red): el servidor pudo haber confirmado la carga. Nunca afirma que no se guardó. */
export const INDETERMINATE_MESSAGE = 'No fue posible confirmar el resultado de la operación: el servidor puede haberla completado. Revise el estado con «Ver lo cargado» antes de repetirla; no la repita a ciegas.'

export function describeApplyHttpError(status: number, code?: string): string {
  if (code && applyErrors[code]) return applyErrors[code]
  switch (status) {
    case 401: return 'Su sesión venció o no ha ingresado. Ingrese de nuevo.'
    case 403: return 'Solo el Máster registrado en el servidor puede hacer esta operación.'
    case 501: return 'En la demostración no se guarda nada: esta operación solo existe en el servidor real.'
    case 404: return 'Esta función no está habilitada en este servidor.'
    case 409: return 'La ventana de carga no lo permite en este momento.'
    case 413: return 'El envío es demasiado grande.'
    case 422: return 'El servidor rechazó la operación. No se guardó nada.'
    // 503 y cualquier otro estado: pudo fallar antes o DESPUÉS de confirmar la transacción; no se afirma rechazo.
    default: return INDETERMINATE_MESSAGE
  }
}

/** Razón de auditoría: 10 a 500 caracteres; devuelve el mensaje de error o null. */
export function checkReason(reason: string): string | null {
  const length = reason.trim().length
  if (length < MIN_REASON) return `Escriba el motivo (al menos ${MIN_REASON} caracteres).`
  if (length > MAX_REASON) return `El motivo es demasiado largo (máximo ${MAX_REASON}).`
  return null
}

export interface ApplyReadiness { canApply: boolean; reason: string }
/** Qué hace falta para habilitar «Aplicar»: revisión aplicable con recibo vigente y ventana abierta. */
export function applyReadiness(preview: InitialLoadPreviewResult | null, windowStatus: string | undefined, nowMs: number): ApplyReadiness {
  if (!preview) return { canApply: false, reason: 'Primero revise los archivos.' }
  if (!preview.applicable || !preview.receipt) {
    return { canApply: false, reason: preview.valid ? 'Los archivos no cumplen las condiciones para cargarse (o la función no está habilitada).' : 'Corrija los problemas de la revisión.' }
  }
  const expiresAt = preview.receiptExpiresAtUtc ? Date.parse(preview.receiptExpiresAtUtc) : NaN
  if (!Number.isFinite(expiresAt) || expiresAt <= nowMs) return { canApply: false, reason: 'La revisión no tiene una vigencia verificable o ya venció. Vuelva a revisar los archivos.' }
  if (windowStatus !== 'open') return { canApply: false, reason: 'La ventana de carga debe estar abierta.' }
  return { canApply: true, reason: '' }
}

export interface ApplyView { tone: 'ok' | 'warn'; title: string; lines: string[] }
export function describeApplyResult(result: InitialLoadApplyResult): ApplyView {
  const c = result.counts
  const lines = [`Talleres creados: ${c.workshopsCreated}`, `Hermanos creados: ${c.membersCreated}`, `Cargos creados: ${c.officesCreated}`]
  const skipped = c.memberExactSkips + c.officeExactSkips
  if (skipped > 0) lines.push(`Ya existían idénticos (sin cambios): ${skipped}`)
  if (result.alreadyApplied) return { tone: 'ok', title: 'Esta misma carga ya estaba aplicada', lines: ['No se duplicó nada.', ...lines] }
  return result.applied
    ? { tone: 'ok', title: 'La carga se aplicó', lines }
    : { tone: 'warn', title: 'La carga no se aplicó', lines: [describeApplyCode(result.code)] }
}

export function describeReadBack(r: InitialLoadReadBack): string[] {
  return [`Talleres cargados: ${r.workshops}`, `Hermanos: ${r.members}`, `Membresías: ${r.memberships}`, `Cargos: ${r.officeAssignments}`]
}

export function blockerRows(blockers: InitialLoadFinding[] | undefined): { where: string; text: string }[] {
  return (blockers ?? []).map(b => ({ where: b.row > 0 ? `${b.sheet}, fila ${b.row}` : b.sheet, text: describeBlocker(b.code) }))
}

/** Error de una operación Máster; el texto en español sale de describeApplyHttpError. */
export class InitialLoadOperationError extends Error { constructor(readonly status: number, readonly code?: string) { super(`initial-load ${status}`) } }

async function call<T>(api: BootstrapApiClient, path: string, method: 'GET' | 'POST', body?: unknown): Promise<T> {
  const token = api.useMocks ? undefined : await api.getAccessToken?.()
  if (!token) throw new InitialLoadOperationError(api.useMocks ? 501 : 401)
  const response = await fetch(`${api.baseUrl}/api/platform/bootstrap/initial-load/${path}`, {
    method, body: body === undefined ? undefined : JSON.stringify(body), credentials: 'omit', cache: 'no-store', redirect: 'error',
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
  })
  if (response.status === 401) await api.onUnauthorized?.()
  if (!response.ok) {
    let code: string | undefined
    try { code = ((await response.json()) as { code?: string }).code } catch { /* sin cuerpo */ }
    throw new InitialLoadOperationError(response.status, code)
  }
  return response.json() as Promise<T>
}
export const applyInitialLoad = (api: BootstrapApiClient, files: Record<string, string>, receipt: string) => call<InitialLoadApplyResult>(api, 'apply', 'POST', { files, receipt })
export const openInitialLoad = (api: BootstrapApiClient, reason: string) => call<InitialLoadWindowTransition>(api, 'window/open', 'POST', { reason })
export const closeInitialLoad = (api: BootstrapApiClient, reason: string) => call<InitialLoadWindowTransition>(api, 'window/close', 'POST', { reason })
export const initialLoadReadBack = (api: BootstrapApiClient) => call<InitialLoadReadBack>(api, 'result', 'GET')

export const READ_BACK_FAILED_MESSAGE = 'La carga fue aceptada por el servidor, pero no fue posible verificar los conteos. Use «Ver lo cargado» para consultar nuevamente; no repita la importación sin comprobar el estado.'

/** Tras un Apply aceptado, un fallo de la relectura NUNCA se presenta como «no se guardó nada»: se conserva el resultado y se avisa aparte. */
export async function applyThenVerify(apply: () => Promise<InitialLoadApplyResult>, readBack: () => Promise<InitialLoadReadBack>):
  Promise<{ result: InitialLoadApplyResult; readBack: InitialLoadReadBack | null; readBackFailed: boolean }> {
  const result = await apply()
  try { return { result, readBack: await readBack(), readBackFailed: false } } catch { return { result, readBack: null, readBackFailed: true } }
}
