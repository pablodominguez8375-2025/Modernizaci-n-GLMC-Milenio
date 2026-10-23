import { useEffect, useState } from 'react'
import type { MembershipApiClient } from './api/membershipApi'

function initialsOf(value: string) { return value.split(/\s+/).filter(Boolean).slice(0, 2).map(item => item[0]?.toUpperCase()).join('') }

/** Foto del Hermano si él la hizo visible; en cualquier otro caso, avatar con iniciales (#609). */
export default function MemberAvatar({ api, memberId, hasPhoto, name, large = false }: { api: MembershipApiClient; memberId: string; hasPhoto?: boolean; name: string; large?: boolean }) {
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    setUrl(null)
    if (hasPhoto) api.getMemberPhotoUrl(memberId).then(value => { if (active) setUrl(value) }).catch(() => { if (active) setUrl(null) })
    return () => { active = false }
  }, [api, memberId, hasPhoto])
  const className = large ? 'member-avatar large' : 'member-avatar'
  return url
    ? <span className={`${className} has-photo`}><img src={url} alt="" /></span>
    : <span className={className}>{initialsOf(name)}</span>
}
