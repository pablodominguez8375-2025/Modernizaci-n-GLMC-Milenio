import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const read = (file: string) => readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')

describe('Texto mínimo legible en Gestión Logial (PO: usuarios mayores)', () => {
  it('sin tamaños de letra menores a 0.8125rem (13 px) en los estilos de Gestión Logial', () => {
    for (const file of ['lodgeManagement.css', 'lodgeBallot.css']) expect(read(file)).not.toMatch(/font-size:\s*0?\.(?:[0-7]\d*|80\d*)rem/)
  })
})
