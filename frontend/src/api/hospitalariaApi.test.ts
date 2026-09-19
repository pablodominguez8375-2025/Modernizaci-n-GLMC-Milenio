import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

describe('Hospitalaria demo workflow', () => {
  it('keeps private aid details local while Gran Hospitalaria receives only aggregate submission data', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'

    let summary = await api.getLodgeHospitalariaSummary(organizationId, '2026-09-01', '2026-09-30')
    const aid = summary.items.find(item => item.category === 'charity_aid')
    expect(aid).toBeDefined()
    expect(aid?.memberReference).toContain('reservada')
    expect(aid?.observation).toContain('sensible')

    const decisions = await api.getHospitalariaCouncilAidDecisions(organizationId)
    expect(decisions.items).toHaveLength(1)
    const approved = await api.approveLodgeHospitalariaExpenseByCouncil(aid!.id, decisions.items[0].id)
    expect(approved.approvalSource).toBe('lodge_council')

    summary = await api.getLodgeHospitalariaSummary(organizationId, '2026-09-01', '2026-09-30')
    expect(summary.pendingExpenses).toBe(0)
    expect(summary.approvedExpenses).toBe(45000)

    const reviews = await api.getHospitalariaCouncilFinancialReviews(organizationId)
    const saved = await api.upsertHospitalariaMonthlySubmission(organizationId, 2026, 9, {
      replenishmentDueAmount: 20000,
      replenishmentPaidAmount: 20000,
      paymentReference: 'TRX-HOSP-DEMO-001',
      councilFinancialReviewId: reviews.items[0].id,
      sourceReference: 'ESTADO-HOSP-DEMO-2026-09',
    })
    expect(saved.differenceAmount).toBe(0)
    expect(saved.pendingExpenseCount).toBe(0)

    const submitted = await api.submitHospitalariaMonthlySubmission(saved.id)
    expect(submitted.status).toBe('submitted')

    const grand = await api.getGrandHospitalariaSubmissions({ organizationId, year: 2026, month: 9 })
    expect(grand.items).toHaveLength(1)
    const projection = grand.items[0] as unknown as Record<string, unknown>
    expect(projection.memberReference).toBeUndefined()
    expect(projection.destination).toBeUndefined()
    expect(projection.observation).toBeUndefined()
    expect(grand.items[0].movementCount).toBe(2)
    expect(grand.items[0].replenishmentPaidAmount).toBe(20000)

    const reconciled = await api.reviewGrandHospitalariaSubmission(saved.id, 'reconciled', 'Conforme · demo')
    expect(reconciled.status).toBe('reconciled')

    const regularity = await api.getHospitalariaWorkshopRegularity(organizationId, '2026-09-30')
    expect(regularity?.status).toBe('up_to_date')
    expect(regularity?.sourceReference).toContain('hospitalaria-rendicion:')
  })
})
