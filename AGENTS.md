# AGENTS.md — Proyecto Centenario

Estas instrucciones son obligatorias para cualquier IA, agente de código, desarrollador o colaborador que continúe el **Proyecto Centenario / Modernización Gran Logia Mixta de Chile**.

## 1. Identidad y ramas

- Existe **un solo Proyecto Centenario y un solo desarrollo continuo**.
- Repositorio oficial: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- Rama de desarrollo e integración: **`dev`**.
- Rama estable/promoción: **`main`**.
- Las ramas `feature/*` nacen desde el HEAD vigente de `dev` y regresan por Pull Request con CI.
- Las ramas `release/*` se usan para cortes RC/UAT cuando el flujo vigente lo determine.
- **No programar desde `main` si `dev` contiene avances posteriores.**
- **No fusionar o sobreescribir `dev` con `main` por conveniencia.** Toda divergencia se reconcilia de forma explícita y revisada.

Checkpoint conocido al redactar esta instrucción: `dev` = `12b37f465220078e3d7472b3c019965c7cce85ae`, merge del PR #87. Este SHA es histórico: **siempre consultar el HEAD vivo de `dev` antes de trabajar**.

## 2. Arranque obligatorio desde cualquier chat o IA

Antes de analizar, programar, corregir, publicar la demo o desplegar QA:

1. Obtener el HEAD actual de `dev` y el estado actual de `main`.
2. Inspeccionar el árbol y el código realmente existente en `dev`.
3. Leer `README.md`.
4. Leer `docs/PMGM-BASE-001-estado-maestro.md`.
5. Leer `docs/PMGM-GOV-001-instrucciones-decisiones-consolidadas.md` y cualquier documento `PMGM-GOV-*` posterior.
6. Leer los ADR, migraciones, pruebas, workflows y documentación específica relacionada con la tarea.
7. Consultar la Línea Base Maestra y los documentos oficiales vigentes en Google Drive que afecten el cambio.
8. Verificar la Constitución/Reglamento y protocolos/formularios oficiales cuando la tarea afecte responsabilidades, firmas, aprobaciones o flujos normativos.
9. Comparar GitHub y Drive. Si hay contradicción material, resolverla antes de tocar el código afectado.
10. Confirmar el estado real de Demo GitHub Pages y QA/instalable; no inferirlo desde una conversación.

**Nunca reconstruir el proyecto desde un chat, resumen, prompt antiguo o memoria de una IA cuando existe estado posterior en GitHub/Drive.**

## 3. Continuidad multichat y multi-IA

La continuidad del proyecto **no depende de un chat maestro único**. Un chat puede servir como interfaz de dirección, pero el estado persistente y transferible está en GitHub y Google Drive.

Cualquier ChatGPT, Codex, Claude, Cursor, Copilot, agente u otro desarrollador puede continuar el trabajo si sigue este protocolo y tiene acceso suficiente a las fuentes.

Si una IA no puede acceder a GitHub `dev`, no debe reconstruir código desde conversaciones. Si no puede acceder a Drive, debe declarar que no pudo verificar las fuentes oficiales/normativas antes de hacer cambios de ese tipo.

## 4. Orden de prevalencia

Ante antecedentes incompatibles:

1. Normativa institucional vigente y documentos oficiales aplicables.
2. Línea Base Maestra y cambios formalmente aprobados.
3. Código vigente de `dev`, Estado Maestro, ADR y documentación técnica asociada.
4. `main` como rama estable/promoción y otras fuentes oficiales vigentes de Drive.
5. Conversaciones, chats, resúmenes y memoria de IA, solo como antecedente histórico.

Una norma nueva puede exigir modificar código o línea base, pero el cambio debe quedar registrado y versionado.

## 5. No regresión

No eliminar, degradar, sobrescribir ni reemplazar funcionalidades, modelos, perfiles, permisos, flujos, migraciones, pruebas, documentación o código ya vigentes sin decisión explícita y trazable.

Antes de modificar una pieza existente:

- leer su implementación actual en `dev`;
- revisar sus pruebas y dependencias;
- preservar todo comportamiento no afectado por el requisito;
- agregar/actualizar pruebas;
- registrar cualquier reemplazo de una decisión anterior.

Está prohibido reemplazar código real por un mock, ejemplo, esqueleto o módulo paralelo solo porque el agente no haya inspeccionado el repositorio actual.

## 6. Arquitectura vigente

Antes de cambiar tecnología, buscar un ADR posterior. La base actual es:

- backend ASP.NET Core / .NET 10;
- PostgreSQL 17 + EF Core/Npgsql;
- frontend React + TypeScript + Vite;
- identidad OIDC/JWT, con Keycloak/PKCE en los entornos actuales;
- monolito modular con límites de dominio explícitos;
- Docker / Docker Compose;
- almacenamiento S3-compatible/MinIO privado;
- ClamAV para archivos;
- GitHub Actions;
- cultura `es-CL`, zona `America/Santiago`, moneda CLP.

No crear una segunda API, un frontend alternativo o un servicio paralelo si el requisito puede extender los módulos existentes.

## 7. Una sola línea con triple salida obligatoria

Cada incremento funcional relevante debe mantener el flujo:

`Requisito aprobado → feature/* desde dev → implementación + pruebas → PR/CI exact-head → dev → Demo funcional GitHub Pages → instalable QA del mismo SHA → despliegue/smoke QA en srv01 → QA/UAT → promoción controlada a main cuando corresponda`

### Definition of Done permanente

Un incremento funcional no se considera terminado por estar fusionado a `dev` ni por tener CI verde. Debe dejar **tres salidas sincronizadas del mismo desarrollo**:

1. **Código real:** implementación, migraciones, permisos, auditoría, pruebas y documentación que correspondan.
2. **Demo GitHub Pages funcional:** datos exclusivamente ficticios/sintéticos, mismos formularios, validaciones, recorridos, estados y permisos visuales que el producto, mediante adaptadores mock compatibles con los contratos reales. No se acepta una maqueta estática sustitutiva.
3. **Instalable QA para `srv01`:** artefacto reproducible desde el mismo SHA, con aplicación e infraestructura necesarias para levantar el stack operacional de QA, incluyendo scripts/runbook, migraciones, manifiesto/checksums y configuración segura de ejemplo.

Si por falta de acceso no se puede desplegar en `srv01`, el artefacto instalable debe quedar igualmente generado/actualizado y el handoff debe decir explícitamente **“despliegue QA pendiente”**. Nunca declarar operacional algo que solo está integrado, empaquetado o visible en Pages.

### Demo GitHub Pages

- Es la superficie demostrativa del mismo producto.
- Usa datos ficticios/sintéticos.
- Debe reflejar las mismas reglas, perfiles, vistas y flujos.
- Toda función demostrada debe conservar el comportamiento funcional mediante datos ficticios/adaptadores mock; si existe una simulación parcial, debe identificarse claramente.
- Visible en Pages **no significa operacional**.

### QA

- La versión operacional se valida en el ambiente QA acordado (`srv01` cuando corresponda al corte vigente).
- Debe ser reproducible desde los artefactos/configuración versionados.
- El instalable QA debe salir del mismo SHA que la demo/código del corte. QA valida backend, PostgreSQL, OIDC, permisos reales, persistencia, auditoría, archivos, correo e integraciones según el avance.

## 8. Perfiles y normativa de Taller

Las responsabilidades de cargos del Taller se derivan de la Constitución y Reglamento General vigente, protocolos y formularios institucionales.

Principio: un permiso técnico **no crea una atribución masónica nueva**.

Cargos institucionales base del Taller:

- Venerable Maestro;
- Inmediato Ex-Venerable Maestro;
- Primer Vigilante;
- Segundo Vigilante;
- Orador/a;
- Secretario/a;
- Tesorero/a;
- Hospitalario/a.

Docencia base: Segundo Vigilante → Aprendices; Primer Vigilante → Compañeros; Inmediato Ex-Venerable → Maestros.

Tesorería y Hospitalaria son ámbitos separados. El Consejo de Administración y las subrogaciones deben conservar trazabilidad.

Leer siempre la documentación normativa vigente antes de modificar permisos o flujos.

## 8.1. Terminología institucional obligatoria

- **Cuadro del Taller**: conjunto oficial de hermanos pertenecientes a un Taller.
- **Cuadro General de la Orden**: vista consolidada institucional de los hermanos de la Orden.
- **Padrón de la Gran Asamblea**: conjunto de electores vigentes habilitados para participar en la Gran Asamblea.
- No usar **padrón** como sinónimo de miembros, membresía, listado general, Cuadro del Taller, Cuadro General de la Orden, asistencia o una nómina genérica de habilitados.
- En procesos de votación distintos de la Gran Asamblea, usar **lista de asistencia**, **nómina de habilitados** o la denominación institucional específica que corresponda.
- Si una fuente normativa usa literalmente otra expresión, conservarla solo como cita o antecedente y no extender ese término a otras vistas del sistema.

## 9. Procedimiento de implementación

Para cada cambio:

1. identificar requisito y fuente;
2. verificar implementación existente en `dev`;
3. determinar impacto en datos, backend, frontend, permisos, auditoría, demo, QA, pruebas y documentación;
4. crear/usar rama `feature/*` desde HEAD de `dev`;
5. implementar extendiendo lo existente;
6. agregar/actualizar migraciones cuando corresponda;
7. agregar/actualizar pruebas;
8. actualizar demo cuando la función sea visible/demostrable;
9. actualizar instalación/artefactos QA cuando corresponda;
10. actualizar documentación y changelog;
11. abrir PR hacia `dev`;
12. exigir CI del **HEAD exacto** del PR antes de merge;
13. no promover a `main` sin el flujo de estabilidad/aprobación vigente.

## 10. Definición de estado

Distinguir siempre:

- definido/documentado;
- implementado en rama/PR;
- integrado en `dev`;
- visible en Demo Pages;
- empaquetado/desplegado en QA;
- probado en QA/UAT;
- promovido a `main`/release estable.

No declarar algo “operacional” por existir solo en código o en la demo.

## 11. Handoff obligatorio

Al terminar una intervención, dejar hechos verificables:

- HEAD/commit de la rama trabajada;
- PR asociado y estado;
- HEAD de `dev` observado al iniciar/cerrar;
- archivos modificados;
- funcionalidades implementadas;
- migraciones;
- pruebas/CI y resultado;
- estado Demo GitHub Pages;
- estado QA/artefacto instalable;
- documentación GitHub actualizada;
- documentación Drive actualizada;
- pendientes concretos;
- riesgos o discrepancias abiertas.

## 12. Regla final

**Continuar siempre el Proyecto Centenario existente. Trabajar desde `dev`, preservar lo ya programado, usar PR/CI, mantener Demo + QA, verificar Drive y normativa, y no reconstruir ni bifurcar el sistema desde memoria conversacional.**
