import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import AccessScreen from './AccessScreen'

describe('Pantalla de ingreso corporativa', () => {
  it('muestra identidad, una sola acción y el aviso de uso restringido', () => {
    const html = renderToStaticMarkup(<AccessScreen message="Ingrese con su cuenta institucional." onLogin={() => undefined} />)
    expect(html).toContain('Gran Logia Mixta de Chile')
    expect(html).toContain('Proyecto Centenario')
    expect(html).toContain('Ingresar con mi cuenta')
    expect(html).toContain('Uso restringido')
    expect(html).toContain('mailto:soporte@granlogiamixta.cl')
    expect((html.match(/<button/g) ?? []).length).toBe(1)
    expect(html).toContain('role="status"')
  })
  it('sin acción de ingreso queda cargando y deshabilitada', () => {
    const html = renderToStaticMarkup(<AccessScreen message="Preparando acceso…" />)
    expect(html).toContain('disabled')
    expect(html).toContain('aria-busy="true"')
  })
  it('un error se anuncia como alerta', () => {
    const html = renderToStaticMarkup(<AccessScreen tone="error" message="No se pudo completar el ingreso." onLogin={() => undefined} loginLabel="Intentar de nuevo" />)
    expect(html).toContain('role="alert"')
    expect(html).toContain('Intentar de nuevo')
  })
})
