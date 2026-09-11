import { describe, expect, it } from 'vitest'
import { resolveShowcaseInitialState } from './showcaseDeepLink'

describe('showcase deep links', () => {
  it('ignores showcase parameters outside mock mode', () => {
    expect(resolveShowcaseInitialState(false, '?showcaseView=treasury&showcaseProfile=grandLodge')).toEqual({ view: 'memberPortal', profile: 'brother' })
  })

  it('opens an allowed QA view with an allowed demo profile', () => {
    expect(resolveShowcaseInitialState(true, '?showcaseView=grandArchive&showcaseProfile=grandLodge')).toEqual({ view: 'grandArchive', profile: 'grandLodge' })
  })

  it('falls back safely for unknown values', () => {
    expect(resolveShowcaseInitialState(true, '?showcaseView=unknown&showcaseProfile=root')).toEqual({ view: 'memberPortal', profile: 'brother' })
  })
})
