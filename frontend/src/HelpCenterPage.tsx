import { useMemo, useState } from 'react'
import InstitutionalIcon from './InstitutionalIcon'
import { helpTopics, searchGuides, visibleGuides, type HelpAccess, type HelpGuide, type HelpTarget, type HelpTopic } from './help/helpGuides'

type Props = {
  access: HelpAccess
  onOpenView?: (view: HelpTarget) => void
  /** Pantallas que esta persona puede abrir; un destino no incluido no muestra «Llévame ahí». */
  canOpen?: (view: HelpTarget) => boolean
}

const topicIcon: Record<HelpTopic, 'search' | 'member' | 'lodge' | 'secretariat' | 'settings'> = {
  'Primeros pasos': 'search', 'Mi espacio': 'member', 'Mi Taller': 'lodge', 'Gran Logia': 'secretariat', Sistema: 'settings',
}

/** Centro de ayuda (etapa 1): guías paso a paso, con buscador, según el cargo de la persona. Sin pestañas: una lista y, al elegir, una guía con «← Volver a Ayuda». */
export default function HelpCenterPage({ access, onOpenView, canOpen }: Props) {
  const [query, setQuery] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)
  const guides = useMemo(() => visibleGuides(access), [access])
  const found = useMemo(() => searchGuides(guides, query), [guides, query])
  const open = openId ? guides.find(guide => guide.id === openId) ?? null : null

  const back = () => { setOpenId(null); window.scrollTo({ top: 0 }) }
  const choose = (guide: HelpGuide) => { setOpenId(guide.id); window.scrollTo({ top: 0 }) }

  if (open) {
    const target = open.target && onOpenView && (!canOpen || canOpen(open.target.view)) ? open.target : null
    return <section className="help-center" aria-labelledby="ayuda-guia-titulo">
      <button type="button" className="secondary section-hub-back" onClick={back}>← Volver a Ayuda</button>
      <article className="help-guide">
        <p className="eyebrow">{open.topic}</p>
        <h1 id="ayuda-guia-titulo">{open.title}</h1>
        <p className="help-guide-summary">{open.summary}</p>
        <ol className="help-steps">
          {open.steps.map((step, index) => <li key={index}><span className="help-step-number" aria-hidden="true">{index + 1}</span><span>{step}</span></li>)}
        </ol>
        {open.tip && <p className="help-tip"><strong>Para tener en cuenta: </strong>{open.tip}</p>}
        {target && <button type="button" className="primary-action help-go" onClick={() => onOpenView?.(target.view)}>{target.label}</button>}
      </article>
    </section>
  }

  const searching = query.trim().length > 0
  return <section className="help-center" aria-labelledby="ayuda-titulo">
    <div className="page-heading"><div><p className="eyebrow">Centro de ayuda</p><h1 id="ayuda-titulo">¿En qué podemos ayudarte?</h1><p>Elige un tema o escribe lo que quieres hacer. Cada guía te explica el paso a paso.</p></div></div>
    <label className="help-search">
      <span className="sr-only">Buscar en la ayuda</span>
      <InstitutionalIcon name="search" size={20} />
      <input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Por ejemplo: pagos, templo, foto, clave…" />
    </label>
    {found.length === 0
      ? <p className="help-empty" role="status">No encontramos una guía con esas palabras. Prueba con otra palabra, por ejemplo «pagos» o «agenda», o escribe a la Secretaría de tu Taller.</p>
      : searching
        ? <div className="section-hub-grid" role="list">{found.map(guide => <GuideCard key={guide.id} guide={guide} onChoose={choose} showTopic />)}</div>
        : helpTopics.map(topic => {
          const list = found.filter(guide => guide.topic === topic)
          if (list.length === 0) return null
          return <section key={topic} className="help-topic" aria-labelledby={`ayuda-tema-${topic}`}>
            <h2 id={`ayuda-tema-${topic}`} className="help-topic-title"><InstitutionalIcon name={topicIcon[topic]} size={20} /> {topic}</h2>
            <div className="section-hub-grid" role="list">{list.map(guide => <GuideCard key={guide.id} guide={guide} onChoose={choose} />)}</div>
          </section>
        })}
  </section>
}

function GuideCard({ guide, onChoose, showTopic }: { guide: HelpGuide; onChoose: (guide: HelpGuide) => void; showTopic?: boolean }) {
  return <button type="button" role="listitem" className="section-hub-card help-card" onClick={() => onChoose(guide)}>
    {showTopic && <small className="help-card-topic">{guide.topic}</small>}
    <strong>{guide.title}</strong>
    <small>{guide.summary}</small>
  </button>
}
