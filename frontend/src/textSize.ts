/** PMGM-UX-004 · Tamaño de letra elegido por la persona (Normal / Grande / Muy grande). */
export type TextSize = 'normal' | 'large' | 'xlarge'

export const TEXT_SIZE_OPTIONS: { value: TextSize; label: string; sample: string }[] = [
  { value: 'normal', label: 'Normal', sample: 'A' },
  { value: 'large', label: 'Grande', sample: 'A+' },
  { value: 'xlarge', label: 'Muy grande', sample: 'A++' },
]

const STORAGE_KEY = 'pmgm.textSize'

export function isTextSize(value: unknown): value is TextSize {
  return value === 'normal' || value === 'large' || value === 'xlarge'
}

export function readTextSize(): TextSize {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    return isTextSize(stored) ? stored : 'normal'
  } catch {
    return 'normal'
  }
}

export function applyTextSize(size: TextSize) {
  document.documentElement.dataset.textSize = size
}

export function saveTextSize(size: TextSize) {
  applyTextSize(size)
  try { window.localStorage.setItem(STORAGE_KEY, size) } catch { /* sin almacenamiento: se aplica solo en esta sesión */ }
  window.dispatchEvent(new CustomEvent('pmgm:text-size', { detail: size }))
}
