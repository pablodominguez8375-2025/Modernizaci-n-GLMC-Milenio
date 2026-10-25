import { describe, expect, it } from 'vitest'
import { BootstrapApiClient } from './api/bootstrapApi'
import { describeInitialLoadFiles } from './initialLoadFiles'

describe('#694 preparar carga inicial', () => {
  it('describe las 3 plantillas en palabras simples', async () => {
    const contract = await new BootstrapApiClient({ useMocks: true }).initialLoadContract()
    const files = describeInitialLoadFiles(contract.files)
    expect(files.map(f => f.name)).toEqual(['Talleres.csv', 'Hermanos.csv', 'CargosVigentes.csv'])
    expect(files[1].purpose).toContain('hermano')
    expect(contract.acceptsUploadedData).toBe(false)
    expect(contract.supportsApply).toBe(false)
  })
  it('la demo descarga solo encabezados, sin datos personales', async () => {
    const { blob } = await new BootstrapApiClient({ useMocks: true }).downloadInitialLoadTemplate()
    const text = await blob.text()
    expect(text).toContain('identityNumber')
    expect(text).not.toMatch(/@|\d{7,}-[\dkK]/)
  })
  it('archivos desconocidos conservan nombre', () => {
    expect(describeInitialLoadFiles([{ name: 'Otro.csv', columns: ['a'] }])[0].name).toBe('Otro.csv')
  })
})
