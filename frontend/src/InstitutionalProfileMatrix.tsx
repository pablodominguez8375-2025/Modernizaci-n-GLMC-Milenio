import { institutionalProfileMatrix, matrixLevels } from './institutionalProfileMatrix'
import { scopeLabel } from './api/dynamicAccess'
import type { PmgmApiClient } from './api/pmgmApi'
import { useDynamicCatalog } from './useDynamicCatalog'

/** Matriz vigente de perfiles: todos los perfiles base con los módulos de su menú, más los perfiles creados por el Máster. */
export default function InstitutionalProfileMatrix({ api }: { api: PmgmApiClient }) {
  const { catalog } = useDynamicCatalog(api)
  const dynamic = catalog.profiles.filter(profile => profile.isActive && !profile.isSystem)
  const menuName = (code: string) => catalog.menus.find(menu => menu.code === code)?.name ?? code
  const total = institutionalProfileMatrix.length + dynamic.length
  return <section className="panel system-profile-matrix">
    <header><div><p className="eyebrow">Control de acceso</p><h2>Matriz vigente de perfiles</h2></div><span className="count-badge">{total} perfiles</span></header>
    <div className="system-profile-table">
      <table>
        <thead><tr><th>Perfil</th><th>Módulos habilitados en su menú</th><th>Alcance</th></tr></thead>
        {matrixLevels.map(level => <tbody key={level}>
          <tr className="system-profile-group"><th colSpan={3}>{level}</th></tr>
          {institutionalProfileMatrix.filter(profile => profile.level === level).map(profile => <tr key={profile.key}>
            <th scope="row">{profile.label}<small>{profile.summary}</small></th>
            <td><div className="system-permission-chips">{profile.views.length === 0 ? <span>Solo su espacio personal</span> : profile.views.map(view => <span key={view}>{view}</span>)}</div></td>
            <td>{profile.scope}</td>
          </tr>)}
        </tbody>)}
        {dynamic.length > 0 && <tbody>
          <tr className="system-profile-group"><th colSpan={3}>Perfiles creados en el sistema</th></tr>
          {dynamic.map(profile => <tr key={profile.code}>
            <th scope="row">{profile.name}<small>{profile.baseRoles?.length ? 'Concede cargos configurados por el Máster.' : 'Perfil dinámico configurado por el Máster.'}</small></th>
            <td><div className="system-permission-chips">{profile.menuCodes.map(code => <span key={code}>{menuName(code)}</span>)}</div></td>
            <td>{scopeLabel(profile.scope)}</td>
          </tr>)}
        </tbody>}
      </table>
    </div>
    <p className="system-profile-note">Cada perfil base figura aquí con los módulos que realmente ve en su menú. Los perfiles que cree el Máster se suman solos. Todos los perfiles incluyen además «Inicio» y «Mi espacio». Las autorizaciones críticas continúan sujetas a segregación de funciones y auditoría.</p>
  </section>
}
