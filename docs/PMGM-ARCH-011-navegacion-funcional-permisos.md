# PMGM-ARCH-011 — Navegación funcional y perfilamiento por capacidades

**Estado:** Implementado en candidato PR #116  
**Decisión:** cada ámbito institucional mantiene un menú operativo único; las acciones se habilitan por capacidad, cargo, alcance, grado y estado del expediente.

## Regla de navegación

- Secretaría del Taller opera bajo **Secretaría**.
- Tesorería del Taller opera bajo **Tesorería**.
- Hospitalaria del Taller opera bajo **Hospitalaria**.
- Vigilantes e Inmediato Ex-Venerable Maestro operan bajo **Docencia**.
- Los órganos de la Orden mantienen **Gran Secretaría**, **Gran Tesorería**, **Gran Hospitalaria** y **Régimen Interior**.
- Una persona con más de un perfil ve la unión de sus capacidades, sin menús duplicados.
- Ocultar una vista no reemplaza la autorización: el backend conserva la decisión final.

## Secretaría

El menú único Secretaría agrupa una navegación secundaria hacia:

1. operación secretarial, correspondencia, pendientes, agenda, Tenidas, asistencia, actas y cierre;
2. insinuaciones y alta mediante “Nuevo insinuado”;
3. circuito de iniciación;
4. afiliación e incorporación;
5. solicitudes de ceremonia y Plancha, retiros y traslados según permisos vigentes.

Gran Secretaría conserva un menú distinto y una navegación secundaria para operación institucional, revisión de insinuados, circuito de iniciación y Afiliación/Incorporación.

## Docencia por grado

| Cargo | Grado operativo | Contenido |
|---|---:|---|
| Segundo Vigilante | 1.er grado | Aprendices |
| Primer Vigilante | 2.º grado | Compañeros |
| Inmediato Ex-Venerable Maestro | 3.er grado | Maestros |

El perfil de sesión expone capacidades separadas para cada grado. La interfaz muestra solamente el grado autorizado y la API vuelve a validar cargo, Taller y grado.

## Matriz de decisión

La autorización efectiva se obtiene de:

`perfil técnico + cargo vigente + alcance Taller/Orden + grado + acción + estado del expediente`

Las acciones normalizadas incluyen consultar, crear, modificar, registrar, cargar archivos, remitir, revisar, observar, aprobar, autorizar, emitir, firmar, descargar y exportar.

## Terminología

- **Cuadro del Taller** para los hermanos de un Taller.
- **Cuadro General de la Orden** para la consolidación institucional.
- **Padrón de la Gran Asamblea** sólo para el universo electoral de dicha Asamblea.

## No regresión

La reorganización no elimina páginas ni operaciones existentes. Cambia su punto de entrada y evita duplicar menús. Las rutas internas, contratos API, auditoría y controles de backend se conservan.
