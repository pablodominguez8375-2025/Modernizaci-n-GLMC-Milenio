import {describe,expect,it} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import AccessReviewPanel from './AccessReviewPanel'
import {PmgmApiClient} from './api/pmgmApi'
describe('access review',()=>{it('shows authorized print and real catalog review',()=>{const html=renderToStaticMarkup(<AccessReviewPanel api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Imprimir revisión');expect(html).toContain('Exportar revisión');expect(html).not.toContain('Acceso certificado')})})
