# Revisión vigente del cruce de menús del Sistema Logial referencial — 28-09-2026

## Base y alcance

Corte comparado con `dev@c05fa74e4e481a481a4e5054512675b006c4c57a`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece estable. PR #182 conserva base histórica `14019d2887a782250021a1eac2f3e8dc936e360c` y head `8bc4951a842154837abe504b148012b417034b52`; no representa el estado actual y no debe integrarse sin actualizar.

El documento de Drive `Documento_Maestro_App_Gestion_Taller_Masonico_FINAL.md` pertenece a “Sistema Logial de Ejemplo - solo como referencia”. Drive muestra modificación el 21-09-2026; el texto declara versión interna 03-07-2026. Se consultó como referencia funcional, no como fuente de arquitectura, permisos o gobierno de Centenario. La Línea Base Maestra y la decisión aprobada de la ficha prevalecen en los perfiles con acceso. Propone PHP/SQLite/cPanel y perfiles SaaS que no se importan al monolito ASP.NET/React vigente.

Esta revisión actualiza contradicciones materiales de la revisión de PR #182. No constituye una auditoría completa de cada criterio de ese documento ni autoriza funciones adicionales. No se alteran cargos, atribuciones, firmas, aprobaciones o procedimientos institucionales.

## Hallazgos reconciliados

| Tema del documento referencial | Estado verificado en Centenario | Evidencia vigente | Decisión para continuidad |
| --- | --- | --- | --- |
| Identidad de cada Taller y acceso a la ficha | Implementado e integrado por PR #200: nombre, fecha histórica de iniciación, ciudad/Oriente, país y logo opcional. La entrada directa está en **Taller → Ficha del Taller**. | `frontend/src/App.tsx`, `frontend/src/LodgeProfilePage.tsx`, `docs/PMGM-ARCH-017-ficha-taller-origen-y-permisos.md`, `docs/qa/PMGM-QA-V071-FICHA-TALLER-LOGO.md`. | No duplicar ficha ni ampliar permisos por el modelo del ejemplo. El menú se omite cuando la sesión carece de capacidad: en Demo, **Secretaría del Taller** puede editar; **Venerable Maestro** ve la ficha en modo consulta; **Hermano** no tiene ese acceso. Para editar logo, usar Secretaría del Taller (o perfil central autorizado según la matriz vigente). |
| “Mis planchas” / planchas de trabajo | Integrado por PR #185 después del corte que originó PR #182. | `frontend/src/MemberWorkPapersPanel.tsx`, documentación vigente de planchas y registro en Estado Maestro. | La afirmación de que seguía pendiente quedó superada. Reutilizar el flujo existente; no reimplementar a partir del documento SaaS. |
| “Libro de Oro” | Adaptado como **Resumen del Taller** por PR #183, con alcance de consulta y delegación limitada según la decisión ya registrada. | Ficha/resumen existente, `docs/PMGM-BASE-001-estado-maestro.md` y cierre de PR #183. | Conservar el nombre y los límites aprobados de Resumen del Taller; no incorporar saldos ni las reglas de visibilidad del Libro de Oro referencial. |
| Oficialidad e historial de cargos | Hay superficies actuales de autoridades y de historial de cargos dentro del código vigente; PR #182 no sirve para afirmar que falten. Esta revisión no certifica paridad campo por campo ni todos los filtros. | `frontend/src/LodgeProfilePage.tsx`, `frontend/src/MemberDirectoryPage.tsx` y módulos de membresía. | No abrir una función duplicada con esta referencia. Si el Product Owner necesita una brecha concreta, definir primero el resultado, campos y filtros requeridos. |
| Asistencia e instrucciones por grado | Gestión Logial y las vistas de miembros contienen consulta/registro de tenidas, asistencia e instrucciones. La matriz vieja propone reportes/filtros adicionales cuya paridad completa no se verificó aquí. | `frontend/src/LodgeManagementPage.tsx`, pruebas `backend/tests/PMGM.Api.Tests/Integration/LodgeInstructionHttpWorkflowTests.cs` y documentación vigente de Gestión Logial. | Mantener como área por revisar, no como brecha confirmada ni autorización de desarrollo. Comparar criterios específicos antes de proponer cambios. |
| Comisiones y configuración SaaS | El modelo referencial atribuye perfiles y acciones globales propias de SaaS; no se trasladan automáticamente al gobierno institucional de Centenario. | `docs/PMGM-GOV-001-instrucciones-decisiones-consolidadas.md`, `docs/PMGM-GOV-002-continuidad-multichat-ia.md`, `docs/PERFILES-TALLER-Y-FIRMAS.md` y matriz vigente de perfiles. | Sin incremento aprobado. Cualquier cambio de permisos requiere alcance y sustento institucional vigentes. |

## Ayuda de acceso a Ficha del Taller

La ficha está en el grupo lateral **Taller** como entrada directa, no dentro de Gestión Logial. En la Demo, abra el selector de perfil del encabezado y elija **Secretaría del Taller · Demostración** para editar nombre, fecha, Oriente/ciudad, país y logo. **Venerable Maestro · Demostración** permite comprobar lectura, pero no edición de identidad. **Hermano · Demostración** no muestra la opción porque no posee esa capacidad. En producción, el backend verifica la autorización además del menú; cambiar el perfil de la Demo no cambia permisos reales.

La edición se limita a Secretaría del Taller en su organización, Gran Secretaría y Régimen Interior con alcance institucional; los lectores sólo consultan. Tesorería, Venerable y administrador técnico no obtienen edición específica de la ficha. La ubicación permanece separada de `TreasuryTerritory` y no determina tarifas.

## Límites y próximo paso

- PR #182 debe refrescarse o cerrarse como obsoleta; no fusionar su rama histórica.
- Para asistencia/instrucciones, la API ya ofrece filtros que faltan en el cliente/vista; cualquier cambio sigue sujeto a definición de prioridad y criterios del Product Owner. Para Oficialidad por períodos, falta confirmar la fuente institucional, el modelo de períodos y el alcance antes de proponer implementación.
- Issues #190 y #191 siguen gobernando las definiciones tarifarias/monetarias y contables; esta revisión no cambia esos estados.
- Issue #97 mantiene pendiente QA física/UAT. `srv01` continúa pausado; no hubo instalación, smoke/regresión física ni UAT. Pages, CI y paquetes no prueban esas etapas.
- No se modifica `main`.

## Verificación documental

Revisión de escritorio contra el HEAD indicado, archivos de implementación/documentación y metadatos/contenido de la referencia Drive. `git diff --check` limpio en el árbol de origen. Sin cambios de código, migraciones, permisos ni pruebas funcionales nuevos.
