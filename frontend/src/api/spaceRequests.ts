// Issue #531: solicitudes de templo o sala. El Taller solicita; solo Gran Secretaría confirma la reserva.
export type SpaceRequestStatus = 'pending' | 'approved' | 'rejected' | 'cancelled'
export type SpaceRequestType = 'temple' | 'secretariat_room'
export type SpaceRequestPurpose = 'tenida' | 'docencia' | 'ceremonia' | 'otro'

export interface SpaceRequest {
  id: string; organizationId: string; preferredSpaceType: SpaceRequestType; preferredSpaceId: string | null
  startsAtUtc: string; endsAtUtc: string; purpose: SpaceRequestPurpose; ceremonyRequestId: string | null
  notes: string | null; expectedAttendees: number | null; equipmentRequest: string | null
  equipmentResponsible: string | null; vmApprovalReference: string | null
  status: SpaceRequestStatus; responseReason: string | null; spaceReservationId: string | null
  createdAtUtc: string; decidedAtUtc: string | null
}
export interface SpaceRequestList { total: number; items: SpaceRequest[] }
export interface CreateSpaceRequestPayload {
  spaceType: SpaceRequestType; preferredSpaceId: string | null; startsAtUtc: string; endsAtUtc: string
  purpose: SpaceRequestPurpose; ceremonyRequestId: string | null; notes: string | null; expectedAttendees: number | null
  equipmentRequest: string | null; equipmentResponsible: string | null; vmApprovalReference: string | null
}
export interface SpaceOption { id: string; name: string; spaceType: SpaceRequestType; isAvailable: boolean }

export const purposeLabel = (value: SpaceRequestPurpose) =>
  value === 'tenida' ? 'Tenida' : value === 'docencia' ? 'Docencia' : value === 'ceremonia' ? 'Ceremonia' : 'Otra actividad'
export const spaceTypeText = (value: SpaceRequestType) => value === 'temple' ? 'Templo' : 'Sala de Secretaría'
export const requestStatusLabel = (value: SpaceRequestStatus) =>
  value === 'pending' ? 'En revisión' : value === 'approved' ? 'Aprobada' : value === 'rejected' ? 'Rechazada' : 'Cancelada'

const TEMPLE_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const ROOM_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
export const mockSpaceOptions: SpaceOption[] = [
  { id: TEMPLE_ID, name: 'Templo Principal', spaceType: 'temple', isAvailable: true },
  { id: ROOM_ID, name: 'Sala de Secretaría', spaceType: 'secretariat_room', isAvailable: true },
]

// Almacén de demostración compartido entre la vista del Taller y la de Gran Secretaría.
const DEMO_ORG = '11111111-1111-1111-1111-111111111111'
const day = (offset: number, hour: number) => { const d = new Date(); d.setUTCDate(d.getUTCDate() + offset); d.setUTCHours(hour, 0, 0, 0); return d.toISOString() }
const store: SpaceRequest[] = [
  { id: '51000000-0000-0000-0000-000000000001', organizationId: DEMO_ORG, preferredSpaceType: 'secretariat_room', preferredSpaceId: ROOM_ID, startsAtUtc: day(9, 22), endsAtUtc: day(10, 0), purpose: 'docencia', ceremonyRequestId: null, notes: 'Instrucción de Aprendices.', expectedAttendees: 12, equipmentRequest: 'Proyector', equipmentResponsible: 'Hno. Segundo Vigilante', vmApprovalReference: null, status: 'pending', responseReason: null, spaceReservationId: null, createdAtUtc: day(-1, 15), decidedAtUtc: null },
  { id: '51000000-0000-0000-0000-000000000002', organizationId: DEMO_ORG, preferredSpaceType: 'temple', preferredSpaceId: TEMPLE_ID, startsAtUtc: day(16, 23), endsAtUtc: day(17, 2), purpose: 'tenida', ceremonyRequestId: null, notes: null, expectedAttendees: 40, equipmentRequest: null, equipmentResponsible: null, vmApprovalReference: null, status: 'pending', responseReason: null, spaceReservationId: null, createdAtUtc: day(-2, 15), decidedAtUtc: null },
  { id: '51000000-0000-0000-0000-000000000003', organizationId: DEMO_ORG, preferredSpaceType: 'secretariat_room', preferredSpaceId: null, startsAtUtc: day(-12, 22), endsAtUtc: day(-12, 23), purpose: 'otro', ceremonyRequestId: null, notes: 'Reunión de comisión.', expectedAttendees: 6, equipmentRequest: null, equipmentResponsible: null, vmApprovalReference: null, status: 'rejected', responseReason: 'La sala está en mantención ese día. Puede solicitar otra fecha.', spaceReservationId: null, createdAtUtc: day(-20, 15), decidedAtUtc: day(-18, 15) },
]
export const mockSpaceRequests = {
  list(organizationId: string | null): SpaceRequest[] {
    return store.filter(item => !organizationId || item.organizationId === organizationId).map(item => ({ ...item }))
  },
  listAll(status?: SpaceRequestStatus): SpaceRequest[] { return store.filter(item => !status || item.status === status).map(item => ({ ...item })) },
  create(organizationId: string, payload: CreateSpaceRequestPayload): SpaceRequest {
    if (payload.purpose === 'ceremonia' && !payload.ceremonyRequestId) throw new Error('La solicitud de ceremonia requiere un expediente de ceremonia.')
    if (new Date(payload.startsAtUtc) <= new Date()) throw new Error('La solicitud debe tener inicio futuro.')
    const item: SpaceRequest = { id: crypto.randomUUID(), organizationId, preferredSpaceType: payload.spaceType, preferredSpaceId: payload.preferredSpaceId, startsAtUtc: payload.startsAtUtc, endsAtUtc: payload.endsAtUtc, purpose: payload.purpose, ceremonyRequestId: payload.ceremonyRequestId, notes: payload.notes, expectedAttendees: payload.expectedAttendees, equipmentRequest: payload.equipmentRequest, equipmentResponsible: payload.equipmentResponsible, vmApprovalReference: payload.vmApprovalReference, status: 'pending', responseReason: null, spaceReservationId: null, createdAtUtc: new Date().toISOString(), decidedAtUtc: null }
    store.unshift(item); return { ...item }
  },
  decide(id: string, status: 'cancelled' | 'rejected' | 'approved', reason: string | null): SpaceRequest {
    const item = store.find(x => x.id === id); if (!item) throw new Error('La solicitud no existe.')
    if (item.status !== 'pending') throw new Error('La solicitud ya fue resuelta.')
    item.status = status; item.responseReason = reason; item.decidedAtUtc = new Date().toISOString()
    if (status === 'approved') item.spaceReservationId = crypto.randomUUID()
    return { ...item }
  },
}
