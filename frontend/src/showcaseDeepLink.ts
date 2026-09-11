import type { DemoProfileKey } from './demoProfiles'

export type ShowcaseView =
  | 'memberPortal'
  | 'dashboard'
  | 'bootstrap'
  | 'candidates'
  | 'candidateProfile'
  | 'members'
  | 'lodgeProfile'
  | 'reporting'
  | 'memberControl'
  | 'dataQuality'
  | 'caseQueue'
  | 'calendar'
  | 'notifications'
  | 'ceremonies'
  | 'regimen'
  | 'treasury'
  | 'hospitalaria'
  | 'secretariat'
  | 'lodge'
  | 'library'
  | 'documents'
  | 'grandArchive'

export interface ShowcaseInitialState {
  view: ShowcaseView
  profile: DemoProfileKey
}

const allowedViews = new Set<ShowcaseView>([
  'memberPortal', 'dashboard', 'bootstrap', 'candidates', 'candidateProfile', 'members', 'lodgeProfile',
  'reporting', 'memberControl', 'dataQuality', 'caseQueue', 'calendar', 'notifications', 'ceremonies',
  'regimen', 'treasury', 'hospitalaria', 'secretariat', 'lodge', 'library', 'documents', 'grandArchive',
])

const allowedProfiles = new Set<DemoProfileKey>(['brother', 'lodge', 'grandLodge'])

export function resolveShowcaseInitialState(useMocks: boolean, search: string): ShowcaseInitialState {
  const fallback: ShowcaseInitialState = { view: 'memberPortal', profile: 'brother' }
  if (!useMocks) return fallback

  const params = new URLSearchParams(search)
  const requestedView = params.get('showcaseView')
  const requestedProfile = params.get('showcaseProfile')

  return {
    view: requestedView && allowedViews.has(requestedView as ShowcaseView) ? requestedView as ShowcaseView : fallback.view,
    profile: requestedProfile && allowedProfiles.has(requestedProfile as DemoProfileKey) ? requestedProfile as DemoProfileKey : fallback.profile,
  }
}
