import { useEffect, useMemo, useState } from 'react'
import {
  createDefaultPmgmApiClient,
  type CandidatePublication,
  type CandidatePortalResponse,
  type SystemInfo,
} from './api/pmgmApi'

type View = 'dashboard' | 'candidates'

const api = createDefaultPmgmApiClient()

export default function App() {
  const [view, setView] = useState<View>('dashboard')
  const [portal, setPortal] = useState<CandidatePortalResponse | null>(null)
  const [systemInfo, setSystemInfo] = useState<SystemInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true

    Promise.all([api.getCandidatePortal(), api.getSystemInfo()])
      .then(([portalResponse, systemResponse]) => {
        if (!active) return
        setPortal(portalResponse)
        setSystemInfo(systemResponse)
      })
      .catch((reason: unknown) => {
        if (!active) return
        setError(reason instanceof Error ? reason.message : 'No fue posible cargar la plataforma.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [])

  return (
    <div className="app-shell">
      <header className="topbar">
        <button className="brand" type="button" onClick={() => setView('dashboard')} aria-label="Ir al inicio">
          <span className="brand-mark" aria-hidden="true">M</span>
          <span>
            <strong>Proyecto Milenio</strong>
            <small>Gran Logia Mixta de Chile</small>
          </span>
        </button>
        <div className="topbar-meta">
          {api.useMocks && <span className="demo-badge">Modo demostración</span>}
          <span className="environment-badge">v{systemInfo?.version ?? '0.10.0'}</span>
        </div>
      </header>

      <div className="workspace">
        <nav className="sidebar" aria-label="Navegación principal">
          <button className={view === 'dashboard' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('dashboard')}>
            <span aria-hidden="true">⌂</span> Inicio
          </button>
          <button className={view === 'candidates' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('candidates')}>
            <span aria-hidden="true">◎</span> Insinuados
          </button>
          <div className="nav-section">Gestión institucional</div>
          <span className="nav-item disabled"><span aria-hidden="true">◇</span> Régimen Interior <em>próximo</em></span>
          <span className="nav-item disabled"><span aria-hidden="true">▤</span> Gran Secretaría <em>próximo</em></span>
          <span className="nav-item disabled"><span aria-hidden="true">□</span> Gestión Logial <em>próximo</em></span>
        </nav>

        <main className="content" id="contenido-principal">
          {error && <ErrorBanner message={error} />}
          {view === 'dashboard' ? (
            <Dashboard portal={portal} systemInfo={systemInfo} loading={loading} onOpenCandidates={() => setView('candidates')} />
          ) : (
            <CandidatePortal portal={portal} loading={loading} />
          )}
        </main>
      </div>
    </div>
  )
}

function Dashboard({
  portal,
  systemInfo,
  loading,
  onOpenCandidates,
}: {
  portal: CandidatePortalResponse | null
  systemInfo: SystemInfo | null
  loading: boolean
  onOpenCandidates: () => void
}) {
  const completedPublications = portal?.items.filter((item) => item.elapsedDays >= item.requiredDays).length ?? 0

  return (
    <>
      <section className="hero-panel">
        <div>
          <p className="eyebrow">Plataforma institucional unificada</p>
          <h1>Panel de inicio</h1>
          <p className="lead">Una sola base maestra para miembros, Talleres, ceremonias, documentos y control institucional.</p>
        </div>
        <div className="hero-status">
          <span className="status-dot" aria-hidden="true" />
          Núcleo API {systemInfo ? 'disponible' : loading ? 'consultando' : 'sin datos'}
        </div>
      </section>

      <section className="metric-grid" aria-label="Indicadores principales">
        <MetricCard label="Insinuados publicados" value={loading ? '—' : String(portal?.total ?? 0)} detail="Período institucional vigente" />
        <MetricCard label="Plazo cumplido" value={loading ? '—' : String(completedPublications)} detail="Listos para continuar validaciones" />
        <MetricCard label="Regla de publicación" value="20 días" detail="Parametrizable por la institución" />
        <MetricCard label="Backend" value={systemInfo?.runtime ?? '.NET 10'} detail="PostgreSQL · Auditoría persistente" />
      </section>

      <section className="dashboard-grid">
        <article className="panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Ceremonias</p>
              <h2>Publicaciones activas</h2>
            </div>
            <button className="text-button" type="button" onClick={onOpenCandidates}>Ver portal</button>
          </div>
          {loading ? <LoadingRows /> : <CandidateSummary items={portal?.items ?? []} />}
        </article>

        <article className="panel milestones">
          <p className="eyebrow">Hoja de ruta MVP</p>
          <h2>Próximos hitos</h2>
          <ol>
            <li><span className="milestone-state done">✓</span><div><strong>Gran Secretaría</strong><small>Reservas, autorizaciones y documentos con auditoría.</small></div></li>
            <li><span className="milestone-state current">2</span><div><strong>Portal e intranet</strong><small>Experiencia responsive por roles.</small></div></li>
            <li><span className="milestone-state">3</span><div><strong>SSO institucional</strong><small>OIDC/PKCE y proveedor definitivo.</small></div></li>
            <li><span className="milestone-state">4</span><div><strong>Gestión Logial</strong><small>Tenidas, asistencia, actas y Secretaría de Taller.</small></div></li>
          </ol>
        </article>
      </section>
    </>
  )
}

function CandidatePortal({ portal, loading }: { portal: CandidatePortalResponse | null; loading: boolean }) {
  const [query, setQuery] = useState('')

  const filtered = useMemo(() => {
    const normalized = normalize(query)
    if (!normalized) return portal?.items ?? []

    return (portal?.items ?? []).filter((item) =>
      normalize(`${item.displayName} ${item.workshopName} ${item.workshopNumber ?? ''}`).includes(normalized),
    )
  }, [portal, query])

  return (
    <>
      <section className="page-heading">
        <div>
          <p className="eyebrow">Transparencia institucional controlada</p>
          <h1>Insinuados en período de publicación</h1>
          <p>Publicaciones vigentes según el plazo configurado para solicitudes de iniciación.</p>
        </div>
        <span className="count-badge">{loading ? '…' : `${filtered.length} registros`}</span>
      </section>

      <section className="panel">
        <label className="search-field">
          <span>Buscar por nombre o Taller</span>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Ej.: Taller 23"
          />
        </label>

        {loading ? (
          <LoadingRows />
        ) : filtered.length === 0 ? (
          <div className="empty-state"><strong>No hay coincidencias.</strong><span>Pruebe con otro nombre o Taller.</span></div>
        ) : (
          <div className="candidate-list">
            {filtered.map((candidate) => <CandidateCard key={`${candidate.displayName}-${candidate.publishedFromUtc}`} candidate={candidate} />)}
          </div>
        )}
      </section>
    </>
  )
}

function CandidateCard({ candidate }: { candidate: CandidatePublication }) {
  const percentage = Math.min(100, Math.round((candidate.elapsedDays / Math.max(1, candidate.requiredDays)) * 100))
  const complete = candidate.elapsedDays >= candidate.requiredDays

  return (
    <article className="candidate-card">
      <div className="candidate-avatar" aria-hidden="true">{initials(candidate.displayName)}</div>
      <div className="candidate-main">
        <div className="candidate-title-row">
          <div><h3>{candidate.displayName}</h3><p>{candidate.workshopName}{candidate.workshopNumber ? ` · Nº ${candidate.workshopNumber}` : ''}</p></div>
          <span className={complete ? 'status-pill complete' : 'status-pill active'}>{complete ? 'Plazo cumplido' : 'En publicación'}</span>
        </div>
        <div className="progress-row">
          <div className="progress-track" aria-label={`${percentage}% del plazo de publicación`} role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percentage}>
            <span style={{ width: `${percentage}%` }} />
          </div>
          <strong>{candidate.elapsedDays}/{candidate.requiredDays} días</strong>
        </div>
        <dl className="candidate-meta">
          <div><dt>Publicado</dt><dd>{formatDate(candidate.publishedFromUtc)}</dd></div>
          <div><dt>Cumplimiento</dt><dd>{formatDate(candidate.complianceDateUtc)}</dd></div>
        </dl>
      </div>
    </article>
  )
}

function MetricCard({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <article className="metric-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>
}

function CandidateSummary({ items }: { items: CandidatePublication[] }) {
  if (items.length === 0) return <div className="empty-state"><strong>Sin publicaciones vigentes.</strong></div>
  return <div className="summary-list">{items.slice(0, 4).map((item) => <div key={`${item.displayName}-${item.publishedFromUtc}`}><span className="mini-avatar">{initials(item.displayName)}</span><div><strong>{item.displayName}</strong><small>{item.workshopName}</small></div><span>{item.elapsedDays}/{item.requiredDays} d.</span></div>)}</div>
}

function LoadingRows() {
  return <div className="loading-rows" aria-label="Cargando"><span /><span /><span /></div>
}

function ErrorBanner({ message }: { message: string }) {
  return <div className="error-banner" role="alert"><strong>No fue posible conectar con la información institucional.</strong><span>{message}</span></div>
}

function normalize(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()
}

function initials(value: string): string {
  return value.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]?.toUpperCase()).join('')
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value))
}
