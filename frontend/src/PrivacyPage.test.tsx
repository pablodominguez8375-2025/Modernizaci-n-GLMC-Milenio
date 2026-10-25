import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import PrivacyPage from './PrivacyPage'

describe('Privacidad y mis datos', () => {
  it('avisa que el acceso de soporte es solo con fines de soporte, por tiempo predeterminado y en solo lectura', () => {
    const html = renderToStaticMarkup(<PrivacyPage contactEmail="privacidad@example.test" />)
    expect(html).toContain('Acceso de soporte')
    expect(html).toContain('Solo con fines de soporte técnico')
    expect(html).toContain('tiempo predeterminado')
    expect(html).toContain('solo lectura')
  })

  it('muestra al Hermano las correcciones que hizo el soporte, sin el dato corregido', () => {
    const html = renderToStaticMarkup(<PrivacyPage contactEmail="privacidad@example.test" corrections={[
      { id: '1', field: 'phone', label: 'Teléfono', operatorName: 'Soporte Demostrativo', reason: 'Teléfono mal digitado', createdAtUtc: '2026-10-25T18:00:00Z' },
    ]} />)
    expect(html).toContain('Acciones de soporte sobre tus datos')
    expect(html).toContain('Teléfono')
    expect(html).toContain('Soporte Demostrativo')
    expect(html).toContain('Teléfono mal digitado')
  })
  it('sin correcciones no muestra la sección', () => {
    expect(renderToStaticMarkup(<PrivacyPage contactEmail="privacidad@example.test" />)).not.toContain('Acciones de soporte sobre tus datos')
  })
})
