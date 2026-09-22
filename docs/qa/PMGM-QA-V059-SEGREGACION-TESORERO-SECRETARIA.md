# PMGM QA v0.59 — Segregación Tesorero / Secretaría

## Objetivo

Impedir que el perfil **Tesorero del Taller** herede menús, vistas o acciones propias de **Secretaría del Taller**.

## QA-031

Con el perfil `lodgeTreasurer`:

- se muestra el menú operativo **Tesorería**;
- no se muestra **Secretaría** ni **Gestión Logial**;
- no se muestran Carga/Revisión de insinuados ni Circuito de Iniciación;
- no se muestran Fichas de miembros, Cuadro del Taller ni Ficha del Taller;
- no se muestra Gestor Documental general;
- no puede abrir Tenidas, asistencia, actas, correspondencia, pendientes, retiros ni expedientes secretariales;
- conserva Mi ficha, Mi calendario, Notificaciones, Insinuados publicados y Biblioteca Virtual como vistas transversales que no entregan administración secretarial;
- conserva exclusivamente Resumen, Cuotas y cobranza, Egresos y Cuadro mensual dentro de Tesorería.

## Autoridad backend

La API ya mantiene la segregación:

- `CanManageLodgeOperations` sólo habilita Taller Admin/Secretaría con ámbito de Taller;
- `CanReadLodgeSecretariat` no incluye Tesorería;
- `CanManageLodgeSecretariat` no incluye Tesorería;
- `CanManageDocuments` del Taller no incluye Tesorería;
- `CanManageLodgeTreasury` continúa habilitando exclusivamente la operación financiera correspondiente.

La corrección elimina la exposición incorrecta del frontend y alinea la Demo con esos permisos reales.

## Evidencia local inicial

- frontend: 35 archivos / 153 pruebas aprobadas;
- build productivo TypeScript/Vite aprobado;
- `git diff --check` aprobado.

## Control automatizado de Demo

El flujo Showcase de GitHub Actions valida QA-031 en las capturas de escritorio y móvil:

- el Tesorero conserva Mi ficha, Mi calendario, Notificaciones, Insinuados publicados, Biblioteca Virtual y Tesorería;
- el menú del Tesorero contiene Resumen, Cuotas y cobranza, Egresos y Cuadro mensual;
- no aparecen los accesos de Secretaría, Gestión Logial, Circuito de Iniciación, fichas, Cuadro del Taller ni Gestor Documental;
- el Venerable recibe únicamente la pestaña Egresos por autorizar dentro del menú Tesorería; no recibe Resumen, cobranza ni Cuadro mensual.

La comprobación falla el workflow Showcase si falta un acceso permitido o aparece uno restringido. Las capturas se generan después de esta validación.
