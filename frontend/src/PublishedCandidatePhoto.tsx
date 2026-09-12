import { useEffect, useState } from 'react'
import { type CandidateIntakeApiClient } from './api/candidateIntakeApi'
import { type CandidatePublication } from './api/pmgmApi'

interface PublishedCandidatePhotoProps {
  candidate: CandidatePublication
  api: CandidateIntakeApiClient
}

type CandidateWithPhoto = CandidatePublication & { photoUrl?: string | null }

export default function PublishedCandidatePhoto({ candidate, api }: PublishedCandidatePhotoProps) {
  const photoUrl = (candidate as CandidateWithPhoto).photoUrl ?? null
  const [src, setSrc] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    setSrc(null)

    if (!photoUrl) return () => { active = false }

    if (api.useMocks) {
      setSrc(`${import.meta.env.BASE_URL}demo-candidate-passport.svg`)
      return () => { active = false }
    }

    api.getPublishedPhoto(photoUrl)
      .then(blob => {
        if (!active || !blob) return
        objectUrl = URL.createObjectURL(blob)
        setSrc(objectUrl)
      })
      .catch(() => {
        if (active) setSrc(null)
      })

    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [api, photoUrl])

  if (!src) return <>{initials(candidate.displayName)}</>
  return <img src={src} alt={`Foto institucional de ${candidate.displayName}`} style={{ width: '100%', height: '100%', objectFit: 'cover', borderRadius: 'inherit' }} />
}

function initials(value: string) {
  return value.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]?.toUpperCase() ?? '').join('')
}
