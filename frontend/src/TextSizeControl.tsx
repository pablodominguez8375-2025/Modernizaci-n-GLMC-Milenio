import { useEffect, useState } from 'react'
import { readTextSize, saveTextSize, TEXT_SIZE_OPTIONS, type TextSize } from './textSize'

/** PMGM-UX-004 · Control «Tamaño de letra»: tres botones grandes, uno marcado. */
export default function TextSizeControl({ className = '' }: { className?: string }) {
  const [size, setSize] = useState<TextSize>(() => readTextSize())
  useEffect(() => {
    const onChange = (event: Event) => setSize((event as CustomEvent<TextSize>).detail)
    window.addEventListener('pmgm:text-size', onChange)
    return () => window.removeEventListener('pmgm:text-size', onChange)
  }, [])
  return <div className={`text-size-control ${className}`.trim()} role="group" aria-label="Tamaño de letra">
    <span className="text-size-title">Tamaño de letra</span>
    <div className="text-size-options">
      {TEXT_SIZE_OPTIONS.map(option => <button key={option.value} type="button" aria-pressed={size === option.value} title={option.label} onClick={() => saveTextSize(option.value)}>
        <span className={`text-size-sample is-${option.value}`} aria-hidden="true">A</span>
        <span className="text-size-label">{option.label}</span>
      </button>)}
    </div>
  </div>
}
