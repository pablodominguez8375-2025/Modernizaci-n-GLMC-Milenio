# #533 C13 — consulta preliminar de Asamblea con permisos del servidor

Fecha: 2026-10-19. dev@e8b4e6eb2f4dfa49fe0683677115111db8351a1b; main@6dfb9546a4873baff15955cf86abfd7d47e3d111 protegida. QA físico en otro hilo.

Nueva ruta de **consulta**, no cierre: `GET /api/regimen-interior/asambleas/{assemblyId}/revision` solo para identidad autenticada con scope institucional de Orden y rol explícito Régimen Interior o Gran Secretaría; no fallback de plataforma/superadmin ni rol de Taller. Consulta registros `assembly_member_statuses` y evaluaciones de la Asamblea solicitada; resuelve terminales de la cadena append-only `SupersedesEligibilityId`. Si hay 0 o 2+ terminales, decisión pendiente y voto falso. No eleva a habilitado un registro sin referencia GT/corte/derechos RI/TH. Devuelve solo identificadores internos, categoría, estado, asistir/sufragar preliminar, razón codificada y fecha de evaluación; no RUT, nombres, historias clínicas, expedientes disciplinarios, identidad del voto ni datos bancarios.

Nota: aun cuando `mayVotePreliminary=true`, **NO constituye habilitación final para padrón**; fuentes externas aún no conectadas y el cierre institucional exige revisión documental C11, cobertura C12 y aprobación RI. Sin endpoint de creación/decisión/cierre, sin mutación BD ni migraciones. Pruebas unitarias de acceso y cadena de revisiones, integración HTTP/PostgreSQL con perfiles sintéticos, QA físico pendiente. CI/Showcase/Installable 3/3 exact-head antes de fusión.

Cruce con PR C11 #581 (read-only de constancias, Program.cs), C12 #582 (preflight pure): esta PR toca archivo nuevo endpoints, test nuevo y una línea en RegimenInteriorEndpoints.cs, sin rutas superpuestas. Rebasar cuando cambie dev, no tocar main/srv01, no promover.

## Archivos entregados
- Nueva `AssemblyPreliminaryReviewEndpoints`: GET autenticado para roles institucionales RI y Gran Secretaría, sin acceso por superadmin implícito. Obtiene estado de Asamblea y miembros/evaluaciones de **esa** Asamblea, resuelve punta única de historial `SupersedesEligibilityId`, marca pendiente cuando faltan evaluaciones, fuentes GT/RI/TH o existe bifurcación.
- Datos mínimos: IDs internos, categoría, estado, asistencia/sufragio preliminares, `mayVoteConfirmed=false` siempre y código de causal. Nunca devuelve RUT, nombres, notas disciplinarias, montos ni sentido del voto. `officiallyClosed=false` y `mayIssueBallots=false` constantes. Se registra auditoría del acceso exitoso, sin valores personales.
- Ruta agregada a `RegimenInteriorEndpoints.cs`, conservando `/summary`. Tests unitarios `AssemblyPreliminaryReviewProjectionTests` (roles, scope, conflictos, precedencia, fuentes) y PostgreSQL/HTTP `AssemblyPreliminaryReviewHttpTests` con perfil de RI sintético, shard b.
- Estos registros actuales NO acreditan fuente externa íntegra: "habilitado preliminar" no tiene carácter de cierre. La UI Claude debe señalarlo expresamente. No se implementaron creación, evaluación, decisiones, cierre ni exportaciones. No se debe conectar esta ruta a emisión de votos.
- PR #583 DRAFT; requiere PMGM CI/Showcase/Installable 3/3 exact head. Ninguna instalación QA/UAT ni promoción main.
