import { describe, expect, it } from 'vitest'
import { initialLoadColumns, normalizeInitialLoadCsv, spanishTemplate } from './initialLoadSpanish'

describe('Carga inicial en español', () => {
  it('la plantilla en español lleva solo los títulos', () => {
    expect(spanishTemplate('Hermanos.csv')).toContain('Nombres;Apellidos')
  })
  it('traduce encabezados, valores y fechas al formato del servidor (separador ;)', () => {
    const labels = initialLoadColumns['CargosVigentes.csv'].map(c => c.label).join(';')
    const out = normalizeInitialLoadCsv('CargosVigentes.csv', `﻿${labels}\r\nH1;T1;Guarda Templo Externo (Retejador);15-03-2026\r\n`)
    expect(out.text).toBe('personRef,workshopCode,officeCode,startDate\r\nH1,T1,outer_guard,2026-03-15\r\n')
  })
  it('traduce grado y estado de Hermanos', () => {
    const labels = initialLoadColumns['Hermanos.csv'].map(c => c.label).join(',')
    const out = normalizeInitialLoadCsv('Hermanos.csv', `${labels}\r\nH1,Pasaporte,AB1234,Ana,Soto,a@b.cl,T1,Compañero,Retiro voluntario\r\n`)
    expect(out.text).toContain('H1,passport,AB1234,Ana,Soto,a@b.cl,T1,2,voluntary_withdrawal')
  })
  it('deja intacto un archivo ya en formato del servidor', () => {
    const keys = initialLoadColumns['Talleres.csv'].map(c => c.key).join(',')
    const out = normalizeInitialLoadCsv('Talleres.csv', `${keys}\r\nT1,1,Uno,,Stgo,Chile\r\n`)
    expect(out.text).toBe(`${keys}\r\nT1,1,Uno,,Stgo,Chile\r\n`)
  })
})
