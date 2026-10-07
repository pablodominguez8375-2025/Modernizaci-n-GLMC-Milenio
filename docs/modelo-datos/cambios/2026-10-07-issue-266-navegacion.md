# #266 / #359 — Navegación con permisos efectivos

Base dev e0300e7a7c8b32b281e00340a6581baa03546747. Sin cambio de esquema, migraciones, catálogo persistente ni DTO backend. Se reutilizan las proyecciones propias documentadas en los cortes de Gran Tesorería y Hospitalaria.

| Estado frontend derivado | Tipo | Fuente / regla |
|---|---|---|
| treasury | boolean | Capacidad institucional + acciones view de Gran Tesorería |
| grandHospitalaria | boolean | Capacidad institucional + acciones view de Gran Hospitalaria |
| lodgeHospitalaria | boolean | Capacidad institucional + al menos un Taller autorizado con acciones view de Hospitalaria local |

Metadatos privados de sesión; no contienen sujetos ajenos, catálogo, datos personales ni asignaciones. Antes: algunos menús/buscador dependían sólo del cargo. Después: también se restringen por permisos efectivos y no montan pantallas denegadas. Sin autoridad no se solicita la proyección; error o ausencia de view deniega. Cada módulo falla independientemente. Cambiar cliente/sujeto/perfil oculta estado anterior inmediatamente; respuestas tardías no lo reactivan. Foco y sondeo conservan acceso estable durante la comprobación; fallo/revocación lo retira al concluir. Cambio explícito de catálogo invalida antes de consultar. El backend sigue validando cada operación.

No altera PK/FK, entidades, índices, cardinalidades, retención, borrado, historial, importes, permisos institucionales o funcionalidades del editor de perfiles. No requiere transformación ni rollback de datos. Pruebas y SHA final en handoff/PR #359.
