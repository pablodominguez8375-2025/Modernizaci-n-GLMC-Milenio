import { expect, it } from 'vitest'
import { outputSide, squareCropRect } from './photoCrop'

it('recorta un cuadrado centrado', () => {
  expect(squareCropRect(1600, 900)).toEqual({ sx: 350, sy: 0, side: 900 })
  expect(squareCropRect(800, 1200)).toEqual({ sx: 0, sy: 200, side: 800 })
  expect(squareCropRect(500, 500)).toEqual({ sx: 0, sy: 0, side: 500 })
})

it('no agranda fotos pequeñas y reduce las grandes a 512', () => {
  expect(outputSide(300)).toBe(300)
  expect(outputSide(4000)).toBe(512)
})
