# START HERE — Proyecto Centenario

## HANDOFF VIGENTE PARA CUALQUIER CHAT O IA — 27-09-2026

Este resumen es un checkpoint, no reemplaza la verificación en vivo. GitHub y Drive prevalecen sobre mensajes, resúmenes o memoria. La fecha del checkpoint es 27-09-2026 (America/Santiago).

### Estado vivo verificado

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- HEAD de `dev`: `d455d454dff0e8cb135ca8d21c04bbd2242be38b` (PR #200, Ficha del Taller). `main`: `6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin cambios.
- PR #200 / Issue #199 integrados: menú directo **Taller → Ficha del Taller**, identidad y logo opcional. CI #1634, Showcase exact-head #931, QA Installable exact-head #569, Showcase/Pages post-merge #932 y QA Installable post-merge #570 — SUCCESS.
- Pages artifact #10943798335, digest `sha256:56674bd800e26ebd53a77d621a67a03eefc44108e126146d2dfcf76040497023`; `qa-current.json` confirma el SHA integrado y ZIP QA SHA-256 `b189f06728348a78df999d7f48f1bb08898d58e9a046f5d12da3e85656c08080`. QA Actions artifact #10943758277, digest `sha256:fcc17f55691aa078ed3a8d32fbb056275b63f6c7a8be55e9a763491eaa734954`.
- PR #197 es documental y está integrada. En el HEAD exacto de su rama: PMGM CI #1629 SUCCESS, Showcase #924 SUCCESS y QA Installable #562 SUCCESS. Tras integración, Showcase #925 y Deploy showcase SUCCESS; QA Installable #563 SUCCESS.
- Pages artifact #10942713140: digest SHA-256 `745f49aa87c426f1eca2186cf1e3ad16fc55d753a2b0c6a7cfccd02288d7c58a`. Su `qa-current.json` confirma `sourceSha=bd311106f9418181ba8cc82164b7b02a47fcb0b2`; ZIP `Proyecto-Centenario-QA-srv01-bd311106f941.zip`, SHA-256 `29fee7dbc138f6dcd35eb7bead657e4d761c4a6d79bdaa405bf0204f7be8df0a`. QA Actions artifact #10943455063: digest `sha256:ed08c7ce2d3c17058f19be810814007bbe7fc4ea30da591fbb5b0e0ba169793a`.
- Demo publicada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
- La mejora de cobranza PR #195 está integrada: la fecha real de recepción del pago se conserva para contabilidad/caja y el año/mes elegido determina la cuota abonada en historial/cartola; no duplica ingresos. Los artefactos son evidencia de publicación/paquete, no de instalación.
- `srv01` está expresamente pausado por el Sponsor. Issue #97 sigue abierto. No instalar, desplegar manualmente, ejecutar smoke en servidor, regresión física ni UAT hasta que el Sponsor levante la pausa. No promover a `main`.
- Issues #190 y #191 permanecen pendientes de definiciones contables/de tarifas; no inventar montos, moneda, conversiones ni tratamiento de anticipos. PR #182 (revisión de menús del Sistema Logial) está abierta con base antigua `14019d2`; no fusionar su rama obsoleta. Comparar su contenido con `dev` y Drive antes de decidir si rebase, sustituir o cerrar.
- La mejora de ficha #199 se autorizó de forma explícita y ya está integrada. Esa aprobación no autoriza automáticamente otro incremento funcional; si Issue #97 sigue bloqueante, se requiere alcance definido por el Product Owner o decisión vigente para la siguiente función.

### Protocolo obligatorio de continuidad

1. Consulta GitHub en vivo: ramas `dev` y `main`, árbol, PRs/issues abiertos, checks y Pages. En un clone ejecuta `git fetch origin dev main`; crea toda rama nueva desde el HEAD vivo de `dev`, nunca desde esta fotografía.
2. Lee en ese mismo HEAD: `START-HERE.md`, `AGENTS.md`, `README.md`, Estado Maestro (`docs/PMGM-BASE-001-estado-maestro.md`), `docs/PMGM-NEXT-001-siguiente-corte-tecnico.md`, GOV-001/GOV-002, ADR, QA, pruebas y workflows pertinentes.
3. Consulta en Drive la Línea Base Maestra y los documentos oficiales vigentes de Proyecto Centenario. Verifica versión/fecha y reconcilia discrepancias. Para normas, cargos, atribuciones, firmas, permisos y aprobaciones, verifica Constitución, Reglamento, protocolos y formularios aplicables.
4. Separa claramente estados: requerido/aprobado, documentado, implementado en rama, integrado en `dev`, publicado en Pages, artefacto QA generado, instalado en servidor, regresión física aprobada, UAT aceptada y promovido a `main`. Un check verde o paquete nunca prueba una etapa posterior.
5. No regresión: inspecciona implementación, modelos, permisos, auditoría, migraciones, pruebas y demo existentes; extiende el producto real, no lo reemplaces con mocks, esqueletos ni sistemas paralelos. Conserva terminología institucional: “Cuadro del Taller/Orden”; “Padrón” se reserva a electores de Gran Asamblea.
6. Para alcance funcional autorizado: rama `feature/*` desde `dev`; cambios mínimos y pruebas necesarias; PR a `dev`; valida CI sobre el SHA exacto; integra sólo si está autorizado y GitHub lo permite. No solicites aprobación redundante del Product Owner para un alcance ya autorizado ni inventes revisores obligatorios. `main` requiere promoción explícita separada.
7. Mantén en sincronía código, Demo GitHub Pages con datos ficticios y paquete instalable QA provenientes del mismo SHA cuando el cambio funcional lo requiera. Cumple la pausa de `srv01`.
8. Al terminar, actualiza documentación de GitHub y Drive; registra SHAs de inicio/cierre, rama/PR, archivos, cambios, pruebas, runs, Pages, checksum del instalable, riesgos, pendientes y decisión requerida. Verifica lectura de retorno y deja handoff apto para otra IA.

### Prompt universal para copiar en un nuevo chat

> Continúa el Proyecto Centenario / Modernización Gran Logia Mixta de Chile como parte del mismo proyecto, sin reiniciar ni reconstruirlo desde conversaciones. Tu primera tarea es verificar en vivo el HEAD de `dev` y `main` en `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`, los PRs/issues/checks recientes, la Demo Pages y el artefacto QA. Lee `START-HERE.md`, `AGENTS.md`, `README.md`, Estado Maestro, NEXT, GOV-001/GOV-002, ADR y documentación relacionada en el HEAD actual de `dev`; consulta además en Google Drive la Línea Base Maestra y fuentes institucionales aplicables. Las fuentes persistentes y sus versiones prevalecen sobre memoria o chats. Conserva todo lo aprobado y sigue el flujo `feature/* → pruebas/documentación → PR → CI exact-head → dev`; no cambies `main` sin promoción expresa. `srv01` permanece pausado hasta instrucción del Sponsor: no instales, despliegues ni declares QA/UAT aceptada por CI, Pages o paquetes. No inicies alcance funcional ajeno a autorizaciones vigentes; reconcilia los pendientes de Issue #97, #190, #191 y PR #182 con las decisiones/documentos actuales antes de elegir el siguiente paso. Mantén terminología y reglas institucionales, datos de demo ficticios, no regresión y código/demo/QA trazables al mismo SHA. Actualiza GitHub y Drive al cerrar y deja un handoff verificable. Si no puedes acceder a una fuente necesaria, dilo y no supongas su contenido.

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

## 7. Registro obligatorio al terminar cada tarea

Al terminar **cada tarea del Proyecto Centenario**, actualiza ambos registros persistentes, aunque la tarea no cambie código:

1. **GitHub:** deja el resultado en el Issue/PR pertinente y actualiza documentación versionada cuando cambie el estado, decisión, código, validación o próximo paso. Registra fecha, HEAD exacto de `dev` y `main`, rama/commit/PR, archivos afectados, pruebas/gates/artefactos, límites operacionales y pendientes. Si no hubo cambios técnicos, indícalo expresamente.
2. **Google Drive:** actualiza la Línea Base Maestra con el mismo estado y enlaces/evidencias. Si no cambió el estado maestro, deja constancia breve de la revisión y del próximo paso.
3. **Handoff transferible:** deja instrucciones suficientes para que cualquier chat/IA continúe desde GitHub y Drive, indicando fuentes a consultar y qué no se debe asumir desde conversaciones.

La intervención se cierra sólo después de verificar la lectura de retorno de ambos registros. Luego deja, como mínimo:

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
