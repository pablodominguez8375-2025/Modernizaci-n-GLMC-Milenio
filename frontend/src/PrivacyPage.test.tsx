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
})
