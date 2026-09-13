# PMGM QA v0.46 — Publicación institucional del insinuado

## Resultado

La tercera etapa del circuito de Iniciación registra la aprobación de Gran Secretaría, publica al insinuado en el portal institucional, genera las notificaciones internas y controla el plazo reglamentario antes de continuar.

## Controles obligatorios

- La ficha privada debe estar completa.
- La deliberación inicial debe constar como aprobada.
- Sólo Gran Secretaría o la administración institucional con alcance de Orden puede publicar.
- El plazo se obtiene de la regla institucional vigente; el valor por defecto es 20 días corridos.
- Una publicación existente se devuelve de forma idempotente y no se duplica.
- Mientras falten días, el expediente queda observado y no avanza.
- La demo permite cambiar la fecha de control QA sin alterar la fecha real de publicación.
- La versión instalada utiliza exclusivamente la fecha actual del servidor institucional.

## Escenarios de aceptación

| Escenario | Resultado esperado |
|---|---|
| Sin deliberación inicial aprobada | HTTP 409; no publica |
| Ficha incompleta | HTTP 409 con requisitos faltantes |
| Usuario sin competencia institucional | HTTP 403 |
| Publicación válida | Estado `published`, plazo y regla congelados |
| Control antes de 20 días | Observado; no avanza |
| Control al día 20 | Etapa completada |
| Reintento sobre publicación vigente | Devuelve la publicación existente |

## Prueba manual de la demo

1. Reiniciar el Circuito de Iniciación.
2. Completar ingreso y deliberación inicial con unanimidad.
3. Cambiar al perfil Gran Secretaría.
4. En Publicación institucional, usar una fecha de control anterior al cumplimiento para comprobar el bloqueo.
5. Usar 14-09-2026 o una fecha posterior y volver a verificar para continuar.

La demostración contiene exclusivamente nombres, documentos e identificadores ficticios.
