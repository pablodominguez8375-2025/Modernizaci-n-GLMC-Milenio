import { describe, expect, it } from 'vitest'
import { LatestResponseGate } from './latestResponseGate'

describe('private historical capture response guard', () => {
  it('prevents the older revision from replacing the latest selection', () => {
    const gate = new LatestResponseGate()
    const firstRevision = gate.begin()
    const secondRevision = gate.begin()

    expect(gate.isCurrent(firstRevision)).toBe(false)
    expect(gate.isCurrent(secondRevision)).toBe(true)
  })

  it('invalidates an in-flight revision when the expediente changes or unmounts', () => {
    const gate = new LatestResponseGate()
    const previousDossier = gate.begin()
    gate.invalidate()

    expect(gate.isCurrent(previousDossier)).toBe(false)
    expect(gate.isCurrent(gate.begin())).toBe(true)
  })

  it('rejects tokens from an older load after a new load starts', () => {
    const gate = new LatestResponseGate()
    const previousLoad = gate.begin()
    const currentLoad = gate.begin()

    expect(gate.isCurrent(previousLoad)).toBe(false)
    expect(gate.isCurrent(currentLoad)).toBe(true)
    expect(gate.isCurrent(0)).toBe(false)
  })
})
