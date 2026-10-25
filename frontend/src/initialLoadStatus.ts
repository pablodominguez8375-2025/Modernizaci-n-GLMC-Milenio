import type { InitialLoadStatus } from './api/bootstrapApi'

export interface InitialLoadStatusView { label: string; tone: 'neutral' | 'open' | 'closed' | 'warn'; detail: string }

/** Traduce el estado de la ventana de carga a palabras simples. Nunca afirma que se pueda aplicar nada. */
export function describeInitialLoadStatus(status: InitialLoadStatus | null): InitialLoadStatusView {
  switch (status?.status) {
    case 'not_opened': return { label: 'Carga no abierta', tone: 'neutral', detail: 'Todavía no se ha abierto la ventana de carga inicial.' }
    case 'open': return { label: 'Carga abierta', tone: 'open', detail: 'La ventana de carga está abierta. Solo el Máster puede cargar hasta cerrarla.' }
    case 'closed': return { label: 'Carga cerrada', tone: 'closed', detail: 'La carga inicial ya se cerró. Desde ahora Gran Secretaría y la Secretaría de cada Taller editan sus datos.' }
    default: return { label: 'Estado no disponible', tone: 'warn', detail: 'No fue posible confirmar el estado de la carga. No se asume que esté abierta.' }
  }
}

export function initialLoadStatusErrorMessage(message: string | undefined): string {
  return /403|permiso|Máster|Superadmin/i.test(message ?? '')
    ? 'Esta consulta estará disponible cuando la cuenta Máster quede registrada en el servidor.'
    : 'No fue posible consultar el estado de la carga inicial.'
}
