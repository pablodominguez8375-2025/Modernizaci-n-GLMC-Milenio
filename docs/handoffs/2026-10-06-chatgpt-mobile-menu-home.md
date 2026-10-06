# Menú móvil → Inicio — ChatGPT — 06-10

Issue #341 · base dev 86302aaba94b6e6e7ceb6df2120c252854e229c5. Fuente: reporte directo del PO y captura móvil adjunta. Reserva MobileTabBar y prueba de interacción; sin hot files, identidad, backend/modelo/contratos/reglas de datos. Main y srv01 preservados.

Causa: App cierra menú con useEffect([view]); si Inicio ya es la vista activa, pulsarlo no cambia view y el menú sigue abierto. Cierre explícito al navegar desde la barra evita depender de ese cambio. Debe cubrir también Mi ficha/Pendientes, Agenda y Avisos, sin que pulsar una pestaña con menú cerrado lo abra.

Drive revisado desde el cierre anterior: no archivos nuevos/modificados; propuesta Claude sobre menús sigue pendiente PO, no se aplica. #116/#60/#58 históricos sin cambios; no otro agente activo sobre MobileTabBar. Estado: preparación antes de programar. Evidencias y corte final se completarán mediante recibo persistente del PR.
