import { describe, expect, it } from 'vitest'
import { officePeriod } from './MemberDirectoryPage'

describe('member office history', () => {
  it('shows the complete appointment interval', () => {
    const result = officePeriod('2023-03-01', '2025-02-28')
    expect(result).toContain('2023')
    expect(result).toContain('2025')
    expect(result).toContain('→')
  })

  it('marks an ongoing appointment as current', () => {
    expect(officePeriod('2026-01-01', null)).toContain('vigente')
  })
})
