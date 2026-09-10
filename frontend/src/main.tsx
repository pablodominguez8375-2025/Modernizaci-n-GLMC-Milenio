import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import AuthRoot from './auth/AuthRoot'
import './styles.css'
import './calendar.css'
import './showcase.css'
import './member-portal.css'
import './candidate-profile.css'

const root = document.getElementById('root')

if (!root) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

createRoot(root).render(
  <StrictMode>
    <AuthRoot />
  </StrictMode>,
)
