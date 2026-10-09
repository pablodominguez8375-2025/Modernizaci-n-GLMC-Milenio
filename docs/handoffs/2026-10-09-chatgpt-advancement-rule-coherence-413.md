# Handoff — Issue #47 / PR #413: coherencia de reglas de ascenso

## Alcance y resultado
- Repositorio: pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio.
- Base al iniciar: dev@b72479331213ec97e04d7b02782d8ca3f93ec7f3.
- PR funcional: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/413.
- Head exacto validado: b70f40d7dcc1a004f056ad3ad18a5625e3cd122f.
- Fusionada squash a dev@db087d6750e312eea0149a3e2e45e6c2f7097713; main@6dfb9546a4873baff15955cf86abfd7d47e3d111, intacta.

## Cambio concreto
Se endurece únicamente la matriz privada de revisión de Aumento de Salario y Exaltación:
- Una regla de mínimos de un grado distinto al de la transición no suministra mínimos, versión ni identificador válidos: estado rule_missing.
- Una regla futura o vencida para la fecha del corte de evidencia tampoco se aplica.
- Una regla del grado correcto y vigente conserva los mínimos y su versión aunque falten asistencias; ausencia de evidencia sigue siendo no acreditada.
- La fila de planchas no certifica presentación ni aprobación desde un archivo o vínculo a Tenida.
- authorizesCeremony permanece false: sin habilitación automática ni modificación de POST /autorizar.

Archivos del PR funcional:
- backend/src/PMGM.Api/Modules/Ceremonies/AdvancementReviewMatrixPolicy.cs
- backend/tests/PMGM.Api.Tests/Ceremonies/AdvancementReviewMatrixPolicyTests.cs

No se agregan migraciones, endpoints, permisos, UI, nuevas tarifas ni valores institucionales.

## Evidencia técnica
Para head b70f40d7:
- PMGM CI run #37965761585: SUCCESS, incluyendo backend unit/policy, integración shards a/b, frontend, smoke, infraestructura y HTTPS/recovery.
- PMGM Showcase Demo run #37965761560: SUCCESS como control de PR. No equivale por sí solo a publicación pública postmerge.
- Proyecto Centenario QA srv01 Installable run #37965761635: SUCCESS (artefacto generado, no instalado). Digest de paquete no inspeccionado en este corte.
- La publicación Pages correspondiente al merge de dev y los gates postmerge requieren verificación por su SHA; no afirmar despliegue físico.

## Fuentes, límites y próximos pasos
- AGENTS.md, START-HERE.md y PMGM-GOV-003 en dev.
- Línea Base Maestra: https://docs.google.com/document/d/1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM/edit
- Adenda de ascensos y dispensas: https://docs.google.com/document/d/15KCbiQVa8S4dkDUkQVUjCQBeKGGlJMPrOy4HSPBtbAg/edit
- Issue #47 sigue abierto: constancia auténtica de trabajos aprobados, continuidad certificada y expediente completo de elegibilidad; no inferir cumplimiento desde registros candidatos.
- Issue #48 sigue abierto: votación/acta unánime de dispensa conforme a norma y resolución de Régimen Interior; nunca dispensar los trabajos obligatorios.
- Issue #97: srv01 físico y UAT pausados expresamente; no desplegar.
- PR #1: no promover main sin UAT y aprobación del Sponsor.

Continuidad: partir del HEAD vivo de dev, no repetir este cambio, y exigir CI/Showcase/QA exact-head en cada nueva PR.
