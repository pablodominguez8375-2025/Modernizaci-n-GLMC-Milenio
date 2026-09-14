import {describe,expect,it} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import AccessReviewPanel,{reviewSummary} from './AccessReviewPanel'
describe('access review',()=>{it('shows certification and export controls',()=>{const html=renderToStaticMarkup(<AccessReviewPanel/>);expect(html).toContain('Auditoría y certificación');expect(html).toContain('Exportar revisión');expect(html).toContain('Certificar');expect(html).toContain('Revocar')});it('summarizes privileged and pending reviews',()=>{const value=reviewSummary([{id:'1',user:'A',email:'a@x.cl',profile:'Admin',scope:'Orden',risk:'critical',reason:'test',status:'pending'}]);expect(value.privileged).toBe(1);expect(value.pending).toBe(1)})})
