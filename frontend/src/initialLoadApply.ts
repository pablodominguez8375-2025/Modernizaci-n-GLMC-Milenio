/** #694 — Aplicar / abrir / cerrar la carga inicial y relectura. Solo el Máster; el servidor decide todo. Nunca se reproduce el contenido de las celdas. */
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

export function describeApplyHttpError(status: number, code?: string): string {
  if (code && applyErrors[code]) return applyErrors[code]
  switch (status) {
    case 401: return 'Su sesión venció. Ingrese de nuevo.'
    case 403: return 'Solo el Máster registrado en el servidor puede hacer esta operación.'
    case 404: return 'Esta función no está habilitada en este servidor.'
    case 409: return 'La ventana de carga no lo permite en este momento.'
    case 413: return 'El envío es demasiado grande.'
    case 422: return 'El servidor rechazó la operación. No se guardó nada.'
    case 503: return 'No fue posible confirmar el estado o la configuración del servidor. No se guardó nada.'
    default: return `El servidor respondió ${status}. No se guardó nada.`
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
  if (preview.receiptExpiresAtUtc && Date.parse(preview.receiptExpiresAtUtc) <= nowMs) return { canApply: false, reason: 'La revisión venció. Vuelva a revisar los archivos.' }
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
