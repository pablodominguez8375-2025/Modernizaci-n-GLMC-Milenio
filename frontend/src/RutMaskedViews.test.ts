import { readFileSync } from 'node:fs'
import { expect, it } from 'vitest'
import { maskRut } from './MaskedRut'

const read = (file: string) => readFileSync(new URL(file, import.meta.url), 'utf8')

it('masks a RUT keeping the first digits and the verifier', () => {
  expect(maskRut('12345678-5')).toBe('12.345.***-5')
  expect(maskRut('7.654.321-K')).toBe('7.654.***-K')
})

it('third-party views never print the full RUT (#609)', () => {
  expect(read('InternalAffairsMemberControlPage.tsx')).toContain('maskRut(item.rut)')
  expect(read('InternalAffairsMemberControlPage.tsx')).not.toMatch(/RUT \$\{item\.rut\}/)
  expect(read('MemberDirectoryPage.tsx')).toContain('profile.member.rutMasked')
})
