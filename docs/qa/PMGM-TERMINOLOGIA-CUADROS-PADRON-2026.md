# Proyecto Centenario — Terminología «Cuadro» y «Padrón» (corrección de interfaz)

**Corte:** 19-09-2026. **Origen:** instrucción del Sponsor / Product Owner y sección 31 de la Línea Base Maestra de Google Drive, ya consolidada mediante PR #104. **Rama:** `feature/cuadro-taller-terminologia-2026`, derivada de `dev` HEAD `4a607fcdfbc5bbcd8fcbd32d5acc425f2113bcfe`.

## Regla institucional vigente

- **Cuadro del Taller:** conjunto oficial de hermanos de un Taller. La consulta de fichas históricas, traslados o pertenencias cerradas debe identificarse explícitamente como fichas/historial, sin presentarlas como miembros vigentes del Cuadro.
- **Cuadro General de la Orden:** consolidación de los hermanos de toda la Gran Logia.
- **Padrón de la Gran Asamblea:** exclusivamente los electores vigentes habilitados de la Gran Asamblea en el corte electoral correspondiente. No denominar «padrón» al listado general de hermanos, al Cuadro del Taller, a la ficha de Taller, a la calidad histórica de asambleísta, a los candidatos pendientes de validación ni a la asistencia efectiva.
- **Sufragio:** habilitación, registro de asistencia efectiva y escrutinio agregado son fuentes separadas; nunca se guarda, deduce ni relaciona el voto individual con una identidad. Conservar los snapshots/auditorías electorales de la Asamblea y sus permisos existentes.

## Correcciones concretas en esta rama

1. `frontend/src/MemberDirectoryPage.tsx`: «Padrón institucional» → «Cuadro del Taller»; título de listado (incluye estados históricos y traslados) «Padrón» → «Fichas e historial».
2. `frontend/src/LodgeProfilePage.tsx`: «padrón agregado» → «composición del Cuadro del Taller».
3. `frontend/src/InternalAffairsDataQualityPage.tsx`: sustituida la referencia a «integridad del padrón» por «integridad de los datos institucionales», ya que la verificación cubre historial y pertenencias y no certifica una lista electoral.

Se trata de correcciones de textos visibles; sin cambios en API, datos, permisos, estados, estructura, reglas de afiliación, funcionalidad electoral ni controles de Asamblea. El uso de padrón para el universo electoral de la Gran Asamblea permanece permitido; cualquier lista preliminar con pendientes/no habilitados debe identificarse como revisión de habilitaciones, no como padrón definitivo.

## Criterios de comprobación

- Inspeccionar los tres textos en frontend sobre la rama y verificar que «padrón» no identifica membresía o datos maestros.
- Ejecutar lint, pruebas y build React del HEAD exacto, más Showcase Demo y QA Installable según workflows existentes; un workflow `success` no equivale a despliegue.
- Antes de cerrar, verificar con la demo `dev` del mismo SHA y con QA manual que el módulo Gran Asamblea conserva lista electoral propia, control de asistentes y sufragio agregado anónimo, sin mezclarse con Cuadro del Taller.
- El cambio no cierra Issue #97: despliegue físico `srv01`, smoke, regresión QA-001..QA-025 y UAT institucional siguen independientes. No supone integración del PR #116 de Admissions.

**Seguimiento:** actualización visual y de documentación propuesta por PR separado hacia `dev`; no promover `main` sin control de cambios. Cualquier uso residual en backend, generación de documentos, reportes o mocks fuera de los tres textos aquí corregidos debe revisarse en su contexto antes de renombrarlo; jamás cambiar automáticamente la semántica técnica de un padrón electoral auténtico.
