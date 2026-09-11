export type InstitutionalIconName =
  | 'home'
  | 'member'
  | 'calendar'
  | 'bell'
  | 'settings'
  | 'candidate'
  | 'members'
  | 'report'
  | 'memberControl'
  | 'dataQuality'
  | 'check'
  | 'ceremony'
  | 'shield'
  | 'treasury'
  | 'hospitalaria'
  | 'secretariat'
  | 'archive'
  | 'lodge'
  | 'library'
  | 'documents'

interface InstitutionalIconProps {
  name: InstitutionalIconName
  size?: number
  className?: string
}

export default function InstitutionalIcon({ name, size = 20, className }: InstitutionalIconProps) {
  const common = {
    width: size,
    height: size,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
    focusable: false,
    className,
  }

  switch (name) {
    case 'home':
      return <svg {...common}><path d="M3 10.8 12 3l9 7.8"/><path d="M5.5 9.4V21h13V9.4"/><path d="M9.5 21v-6h5v6"/></svg>
    case 'member':
      return <svg {...common}><circle cx="12" cy="8" r="3.5"/><path d="M5.5 20c.7-4 3-6 6.5-6s5.8 2 6.5 6"/></svg>
    case 'calendar':
      return <svg {...common}><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M8 3v4M16 3v4M3 10h18"/><path d="M8 14h2M14 14h2M8 17.5h2M14 17.5h2"/></svg>
    case 'bell':
      return <svg {...common}><path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></svg>
    case 'settings':
      return <svg {...common}><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.8 1.8 0 0 0 .4 2l.1.1-2.8 2.8-.1-.1a1.8 1.8 0 0 0-2-.4 1.8 1.8 0 0 0-1.1 1.6V21H10v-.1A1.8 1.8 0 0 0 8.9 19a1.8 1.8 0 0 0-2 .4l-.1.1L4 16.7l.1-.1a1.8 1.8 0 0 0 .4-2A1.8 1.8 0 0 0 3 13.5H3v-4h.1a1.8 1.8 0 0 0 1.6-1.1 1.8 1.8 0 0 0-.4-2l-.1-.1L7 3.5l.1.1a1.8 1.8 0 0 0 2 .4A1.8 1.8 0 0 0 10.2 2H14v.1A1.8 1.8 0 0 0 15.1 4a1.8 1.8 0 0 0 2-.4l.1-.1L20 6.3l-.1.1a1.8 1.8 0 0 0-.4 2A1.8 1.8 0 0 0 21 9.5h.1v4H21a1.8 1.8 0 0 0-1.6 1.5Z"/></svg>
    case 'candidate':
      return <svg {...common}><circle cx="9" cy="8" r="3"/><path d="M3.5 19c.6-3.6 2.5-5.4 5.5-5.4 1.3 0 2.4.3 3.3 1"/><circle cx="17.5" cy="16.5" r="3.5"/><path d="m16 16.5 1 1 2-2"/></svg>
    case 'members':
      return <svg {...common}><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.3"/><path d="M3 20c.5-4.2 2.5-6.2 6-6.2s5.5 2 6 6.2M15 14.5c3.2-.4 5.2 1.4 5.8 4.5"/></svg>
    case 'report':
      return <svg {...common}><path d="M5 3h10l4 4v14H5z"/><path d="M15 3v5h4M8 17v-4M12 17V9M16 17v-6"/></svg>
    case 'memberControl':
      return <svg {...common}><circle cx="9" cy="8" r="3"/><path d="M3.5 19c.6-3.6 2.5-5.4 5.5-5.4 1.7 0 3 .5 4 1.4"/><path d="M17 12v7M14 15.5h6"/></svg>
    case 'dataQuality':
      return <svg {...common}><path d="M12 3 2.8 20h18.4z"/><path d="M12 9v5M12 17.5h.01"/></svg>
    case 'check':
      return <svg {...common}><circle cx="12" cy="12" r="9"/><path d="m8 12 2.6 2.6L16.5 9"/></svg>
    case 'ceremony':
      return <svg {...common}><path d="M12 3v18M7 6h10M8.5 10h7M6 21h12"/><path d="M12 3 9.5 6M12 3l2.5 3"/></svg>
    case 'shield':
      return <svg {...common}><path d="M12 3 20 6v5c0 5-3.2 8.3-8 10-4.8-1.7-8-5-8-10V6z"/><path d="m9 12 2 2 4-4"/></svg>
    case 'treasury':
      return <svg {...common}><rect x="3" y="6" width="18" height="13" rx="2"/><path d="M3 10h18M8 15h3"/><circle cx="16.5" cy="14.5" r="1.7"/></svg>
    case 'hospitalaria':
      return <svg {...common}><path d="M12 20s-7-4.2-7-10a4 4 0 0 1 7-2.7A4 4 0 0 1 19 10c0 5.8-7 10-7 10Z"/><path d="M12 9v6M9 12h6"/></svg>
    case 'secretariat':
      return <svg {...common}><path d="M6 3h9l3 3v15H6z"/><path d="M15 3v4h4M9 11h6M9 15h6M9 18h4"/></svg>
    case 'archive':
      return <svg {...common}><path d="M4 7h16v14H4zM3 3h18v4H3z"/><path d="M9 11h6"/></svg>
    case 'lodge':
      return <svg {...common}><path d="M4 21V9l8-5 8 5v12M8 21v-8h8v8M2 21h20"/></svg>
    case 'library':
      return <svg {...common}><path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v17H7.5A3.5 3.5 0 0 0 4 22z"/><path d="M20 5.5A3.5 3.5 0 0 0 16.5 2H13v17h3.5A3.5 3.5 0 0 1 20 22z"/></svg>
    case 'documents':
      return <svg {...common}><path d="M8 3h9l3 3v14H8z"/><path d="M17 3v4h4M4 7v14h12M11 11h6M11 15h6"/></svg>
    default:
      return null
  }
}
