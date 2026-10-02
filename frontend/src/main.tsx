import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import AuthRoot from './auth/AuthRoot'
import './styles.css'
import './calendar.css'
import './showcase.css'
import './member-portal.css'
import './candidate-profile.css'
import './ppt-fidelity.css'
import './archive-ppt-fidelity.css'
import './regularity-ppt-fidelity.css'
import './dashboard-ppt-fidelity.css'
import './secretariat-ppt-fidelity.css'
import './member-responsive-fix.css'
import './mobile-nav-compact.css'
import './institutional-theme.css'
import './role-navigation.css'
import './header-p2.css'
import './listing.css'
import './action-kit.css'
import './accessibility.css'
import './home-consistency.css'
import { applyTextSize, readTextSize } from './textSize'

const root = document.getElementById('root')

if (!root) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

applyTextSize(readTextSize())

createRoot(root).render(
  <StrictMode>
    <AuthRoot />
  </StrictMode>,
)
