import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import RegimenInteriorPage from './RegimenInteriorPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('Revisión de reinsinuaciones por Régimen Interior', () => {
  it('includes a restricted reviewer entrypoint in the institutional page', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Revisión de reinsinuaciones')
    expect(html).toContain('Sólo una resolución formal aprobada habilita el requisito')
  })

  it('demo reviews are isolated and stateful, never granting approval without a reference', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const queue = await api.getRePresentationReviews()
    expect(queue.total).toBeGreaterThan(0)
    const original = queue.items[0]
    expect(original.status).toBe('observed')
    await expect(api.resolveRePresentation(original.requestId,
      { status: 'approved', sourceReference: '  ' })).rejects.toThrow('referencia')
    const first = await api.resolveRePresentation(original.requestId,
      { status: 'approved', sourceReference: 'ACTA-RI-DEMO-001' })
    expect(first.status).toBe('approved')
    const replay = await api.resolveRePresentation(original.requestId,
      { status: 'approved', sourceReference: 'ACTA-RI-DEMO-001' })
    expect(replay.replayed).toBe(true)
    const changed = await api.resolveRePresentation(original.requestId,
      { status: 'observed', sourceReference: 'ACTA-RI-DEMO-002' })
    expect(changed.replayed).toBe(false)
    expect((await api.getRePresentationReviews()).items[0].status).toBe('observed')
    expect((await new PmgmApiClient({ useMocks: true }).getRePresentationReviews())
      .items[0].status).toBe('observed')
  })
})
