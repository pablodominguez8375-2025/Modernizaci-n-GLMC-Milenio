# START HERE — Proyecto Centenario

**Este archivo es el punto de entrada obligatorio para cualquier chat nuevo, ChatGPT Work, Codex, IA de desarrollo, agente o desarrollador humano.**

No continúes el proyecto desde memoria conversacional, desde un resumen antiguo ni desde una rama elegida por costumbre. Reconstruye siempre el estado vivo desde las fuentes persistentes.

## 1. Identidad

- Proyecto: **Proyecto Centenario / Modernización Gran Logia Mixta de Chile**.
- Repositorio oficial: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- Rama activa de desarrollo e integración: **`dev`**.
- Rama estable/promoción: **`main`**.
- Carpeta oficial de Google Drive: `Proyecto Centenario`.
- Existe **una sola línea de desarrollo**. Demo GitHub Pages y QA instalable son dos salidas del mismo producto, no proyectos separados.

## 2. Regla de arranque

Antes de responder sobre estado, diseñar, programar, corregir, hacer un PR, desplegar o modificar documentación:

1. consulta el **HEAD vivo de `dev`** y el estado de `main`;
2. inspecciona el árbol y el código realmente existente en `dev`;
3. lee `AGENTS.md`, `README.md`, `docs/PMGM-BASE-001-estado-maestro.md` y los `docs/PMGM-GOV-*` vigentes;
4. lee ADR, migraciones, pruebas, workflows y documentación específica de la tarea;
5. consulta en Google Drive la **Línea Base Maestra** y documentos oficiales vigentes aplicables;
6. si la tarea toca cargos, firmas, permisos, aprobaciones o procedimientos institucionales, verifica Constitución/Reglamento, protocolos y formularios oficiales;
7. compara GitHub y Drive y resuelve cualquier discrepancia material antes de cambiar el código;
8. distingue siempre entre: documentado, implementado en rama, integrado en `dev`, visible en demo, desplegado en QA, probado en UAT y promovido a `main`.

## 3. Prevalencia

Cuando dos fuentes difieran:

1. normativa institucional vigente y documentos oficiales aplicables;
2. Línea Base Maestra y cambios formalmente aprobados;
3. código vigente de `dev`, Estado Maestro, ADR y documentación técnica asociada;
4. `main` como rama estable/promoción y otros documentos vigentes de Drive;
5. chats, resúmenes y memoria de IA, solo como contexto histórico.

## 4. No regresión

No elimines, simplifiques, reemplaces ni reconstruyas funcionalidades, modelos, perfiles, permisos, flujos, migraciones, pruebas, documentación o arquitectura ya vigentes sin una decisión explícita y trazable.

No crees una segunda API, un frontend paralelo, un nuevo esquema de autorización o un mock sustitutivo cuando el sistema real ya contiene esa capacidad.

## 5. Flujo de desarrollo

`Requisito/fuente → feature/* desde HEAD de dev → implementación sobre código existente → pruebas → documentación → PR a dev → CI exact-head → merge a dev → Demo Pages + QA instalable → QA/UAT → promoción controlada a main`

No mezcles una corrección documental/gobierno con cambios funcionales si pueden revisarse por separado.

## 6. Definición permanente de terminado: triple salida

Todo incremento funcional del Proyecto Centenario debe producir y mantener **tres salidas sincronizadas del mismo desarrollo y del mismo SHA**:

1. **Programación real:** funcionalidad implementada sobre el código vigente de `dev`, con backend/frontend/datos/permisos/auditoría/migraciones/pruebas/documentación según corresponda.
2. **Demo funcional GitHub Pages:** misma experiencia y mismos flujos funcionales, usando exclusivamente datos ficticios/sintéticos y adaptadores mock equivalentes a los contratos reales. No puede ser una maqueta estática que omita el comportamiento implementado. Debe publicarse desde `dev` y permitir verificar el SHA visible.
3. **Instalable QA para `srv01`:** paquete reproducible generado desde el mismo SHA, con aplicación, infraestructura, configuración de ejemplo segura, migraciones, scripts/runbook, manifiesto/checksums y controles necesarios para montar el stack operacional de QA.

Un incremento **no se considera terminado** solo porque el PR esté fusionado o el CI compile. Debe quedar, como mínimo, código integrado, demo actualizada/publicable y artefacto instalable generado/actualizado. Cuando exista acceso operativo a `srv01`, el ciclo continúa con despliegue, smoke y QA/UAT. Si el despliegue no pudo realizarse, debe declararse expresamente como pendiente y nunca confundirse con un estado operacional.

La demo pública nunca usa datos personales reales, secretos ni servicios institucionales reales. El instalable QA sí valida la arquitectura operacional con API, PostgreSQL, OIDC/Keycloak, almacenamiento S3/MinIO, ClamAV y demás componentes vigentes.

## 7. Handoff obligatorio

Toda intervención debe terminar dejando, como mínimo:

- HEAD de `dev` observado al inicio y al cierre;
- rama/commit/PR trabajados y estado del PR;
- archivos y funciones modificadas;
- migraciones, si existen;
- pruebas/CI y resultado;
- estado de Demo GitHub Pages;
- estado de QA/artefacto instalable;
- documentación GitHub y Drive actualizada;
- pendientes y riesgos abiertos.

## 8. Prompt universal de arranque

Copia este texto al iniciar un chat, Work o IA que no tenga aún el contexto del proyecto:

> **Continuar Proyecto Centenario. No uses memoria de chats como fuente de verdad. Primero consulta el HEAD vivo de `dev` del repositorio `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`, verifica `main` como rama estable, lee `START-HERE.md`, `AGENTS.md`, Estado Maestro, documentos PMGM-GOV/ADR/pruebas/workflows relacionados y consulta la Línea Base Maestra y documentos oficiales vigentes de Google Drive. Preserva todo lo ya programado y aprobado, resuelve discrepancias GitHub/Drive antes de modificar código, trabaja mediante `feature/* → PR/CI exact-head → dev`, mantén paridad Demo GitHub Pages + QA instalable y no promociones a `main` sin el flujo aprobado. Al terminar deja un handoff verificable.**

## 9. Regla final

**Si no puedes consultar `dev` y las fuentes oficiales necesarias, no reconstruyas ni continúes a ciegas: declara qué fuente no pudiste verificar.**

Los SHA escritos en conversaciones o documentos son checkpoints históricos. **El único punto técnico actual es el HEAD vivo de `dev` al comienzo de cada intervención.**
