import { describe, expect, it } from 'vitest'
import { BootstrapApiClient } from './api/bootstrapApi'
import { INITIAL_LOAD_FILE_NAMES, MAX_CHARACTERS_PER_FILE, checkChosenFile, checkTexts, describeField, describeFinding, describePreview, describePreviewHttpError } from './initialLoadPreview'

const good = {
  'Talleres.csv': 'workshopCode,number,name,orienteCode,city,country\nT1,1,Taller Ficticio,O1,Ciudad,CL',
  'Hermanos.csv': 'personRef,identityType,identityNumber,firstNames,lastNames,email,workshopCode,grade,institutionalStatus\nP1,rut,1-9,Ana,Ficticia,a@ejemplo.cl,T1,3,active',
  'CargosVigentes.csv': 'personRef,workshopCode,officeCode,startDate\nP1,T1,office-1,2026-01-01',
}

describe('Carga inicial · revisión de los tres CSV (#750)', () => {
  it('traduce cada código del servidor a palabras simples y no inventa datos', () => {
    for (const code of ['HEADER_MISMATCH', 'COLUMN_COUNT', 'REQUIRED_ROWS', 'TOO_MANY_ROWS', 'INVALID_QUOTING']) expect(describeFinding(code)).not.toBe(code)
    expect(describeField('columns')).toBe('Cantidad de columnas')
    expect(describeField('email')).toBe('email')
    expect(describeFinding('CODIGO_NUEVO')).toBe('Hay un problema en este dato')
  })
  it('cada estado HTTP tiene un mensaje en español y ninguno afirma que se guardó algo', () => {
    for (const status of [400, 401, 403, 409, 413, 415, 503, 500]) { const text = describePreviewHttpError(status); expect(text.length).toBeGreaterThan(10); expect(text.toLowerCase()).not.toContain('guardado correctamente') }
    expect(describePreviewHttpError(409)).toContain('ya se cerró')
  })
  it('valida nombre y tamaño del archivo elegido y los topes del envío', () => {
    expect(checkChosenFile('Talleres.csv', { name: 'a.xlsx', size: 10 })).toMatch(/CSV/)
    expect(checkChosenFile('Talleres.csv', { name: 'a.csv', size: 0 })).toMatch(/vacío/)
    expect(checkChosenFile('Talleres.csv', { name: 'a.csv', size: 100 })).toBeNull()
    expect(checkTexts({ 'Talleres.csv': 'x' })).toMatch(/Falta elegir Hermanos/)
    expect(checkTexts({ ...good, 'Talleres.csv': 'x'.repeat(MAX_CHARACTERS_PER_FILE + 1) })).toMatch(/supera/)
    expect(checkTexts(good)).toBeNull()
  })
  it('la demostración revisa solo la forma, no guarda y nunca habilita aplicar', async () => {
    const api = new BootstrapApiClient({ baseUrl: '', useMocks: true })
    const ok = await api.previewInitialLoad(good)
    expect(ok).toMatchObject({ valid: true, canApply: false, writesPerformed: 0, status: 'PREVIEW_VALID' })
    expect(describePreview(ok).tone).toBe('ok')
    const bad = await api.previewInitialLoad({ ...good, 'Hermanos.csv': 'otra,cosa\n1,2' })
    expect(bad.valid).toBe(false)
    expect(bad.findings.some(f => f.sheet === 'Hermanos.csv' && f.code === 'HEADER_MISMATCH')).toBe(true)
    // Los hallazgos no contienen el contenido de las celdas.
    expect(JSON.stringify(bad.findings)).not.toContain('Ficticia')
    const view = describePreview(bad)
    expect(view.tone).toBe('warn')
    expect(view.counts.map(c => c.name)).toEqual([...INITIAL_LOAD_FILE_NAMES])
  })
})
