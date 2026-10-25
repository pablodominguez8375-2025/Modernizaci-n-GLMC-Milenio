# #694 fase D — Vista previa offline de 3 CSV (sin datos reales)

Agente: ChatGPT · 24-10-2026 · rama `feature/initial-load-offline-preview-694-20261024-gpt` desde `dev@3958637dcb32bdfa5e8a237204b7a552780255b5`.

## Dependencia dura
- Fase A PR #698: contrato de tres plantillas CSV de encabezados.
- Fase B PR #700: `InitialLoadPreflightPolicy.Inspect` de filas estructuradas (MERGED).
- Fase C PR #702: `InitialLoadCsvShapePolicy.Inspect` del texto CSV (DRAFT al inicio). **No fusionar esta fase D antes de integrar #702** y reconstruir la rama D sobre el HEAD vivo de dev, con nuevas pruebas exact-head.
- El validador forma+Criterios funciona exclusivamente con cadenas en memoria suministradas por pruebas sintéticas; NO conectarlo a APIs ni almacenamiento todavía.

## Alcance
Nuevo `InitialLoadCsvPreviewPolicy`: primero ejecutar comprobaciones de forma/cabecera sin exponer datos; si pasan, interpretar columnas/filas con comillas escapadas y comparar relaciones vía preflight #700. Devolver solo nombre canónico de hoja, fila, campo, código de error y conteo, jamás nombres/RUT/correos/identidad/filas. Cero escrituras y `CanApply=false` incondicional. Límites heredados de fase C. Pruebas con strings ficticios y regresiones de privacidad/errores; sin rutas de subida, despliegue, BD ni secretos.

## No incluido
Carga de nómina real, acta de consentimiento, estado abierto/cerrado, importador, transacción, cuenta, perfil, auditoría con datos identificables, ni reversión. Requiere aprobación Jurídico Ley 21.719 y control de acceso del usuario Máster antes de habilitar cualquier recepción de datos personales en QA, aunque el PO haya definido que allí irá la carga futura. En fase D los archivos reales continúan prohibidos.

## Coordinación
UI Claude por Issue #694 en etapa posterior; no alterar frontend/hot files, migraciones, AGENTS o main. PR draft, CI + Showcase + QA Installable sobre HEAD exacto 3/3 SUCCESS, revisión de diff y behind=0 antes de merge a dev. Registro GitHub + Drive.
