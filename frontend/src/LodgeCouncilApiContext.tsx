import { createContext, type ReactNode, useContext } from 'react'
import { type LodgeCouncilApiClient } from './api/lodgeCouncilApi'

const LodgeCouncilApiContext = createContext<LodgeCouncilApiClient | null>(null)

export function LodgeCouncilApiProvider({ api, children }: { api: LodgeCouncilApiClient; children: ReactNode }) {
  return <LodgeCouncilApiContext.Provider value={api}>{children}</LodgeCouncilApiContext.Provider>
}

export function useLodgeCouncilApi() {
  const api = useContext(LodgeCouncilApiContext)
  if (!api) throw new Error('El cliente del Consejo de Administración no está disponible.')
  return api
}
