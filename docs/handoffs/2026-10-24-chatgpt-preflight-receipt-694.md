# PMGM #694 — fase O — comprobante de preflight ligado a los CSV (sin datos personales)

Corte 24-10-2026 (Chile), agente ChatGPT. Base `dev@923e8c7b3a91678578d69c8689b05f727053e2c0`; rama `feature/initial-load-preflight-receipt-694-20261024-gpt`.

## Objetivo
Implementar un componente **puro, interno y no registrado** para ligar tres CSV de carga inicial a un comprobante firmado HMAC-SHA256 con clave **suministrada por el servidor**, vigente por una ventana breve. Protege contra reemplazo, alteración o intercambio de archivos entre preflight y futura aplicación. Revalida forma y reglas del preflight. No es consentimiento ni autorización jurídica, **no habilita apply**.

## Archivos reclamados
- `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadPreflightReceiptPolicy.cs` nuevo.
- `backend/tests/PMGM.Api.Tests/InitialLoadPreflightReceiptPolicyTests.cs` nuevo.
- `docs/handoffs/2026-10-24-chatgpt-preflight-receipt-694.md` este handoff.

No se tocan archivos calientes, migraciones, frontend, main ni glmdch. PR draft y 3 gates exact-head.

## Premisas y límites
Sólo datos sintéticos en tests. Material de clave real es externo al código; método requiere mínimo 32 bytes, falla cerrado sin secreto. Token no es de un solo uso: un futuro apply necesitará persistir consumo de nonce y hash del lote junto a transacción y legal/audit. No cargar datos reales sin aprobación Jurídico Ley 21.719. En ningún resultado se devuelven valores de CSV, correos, RUT, nombres ni razones jurídicas. La huella de lote es HMAC con secreto, no SHA público predecible.

## Continuidad
Verificar código/CI en PR, squash a dev sólo con 3/3 gates exact SHA y behind0; actualizar Drive e Issue #694. Pendientes fase posterior: evidencia Jurídico server-side, autenticación y endpoint Master, apply/reversal transaccional idempotente y QA/UAT, diagnóstico #681.

## Implementación y pruebas (fase O)
Componente puro `InitialLoadPreflightReceiptPolicy` sin DI/HTTP. `Issue`: acepta únicamente exactamente las tres hojas del contrato CSV v1 si pasa `InitialLoadCsvPreviewPolicy.Inspect`, contiene al menos un Taller y un Hermano y se dispone de clave de servidor (>=32 bytes). Genera huella **HMAC-SHA256** encuadrada con nombres/longitud y orden canónico de las hojas; nonce CSPRNG y ticket HMAC con fecha Unix UTC. Rehúsa strings Unicode con UTF-8 inválido. La prueba solo vale durante 15 minutos, pero **no es de un solo uso**. El ticket no incluye celdas ni datos personales, y su `CanApply=false`. La huella liga exactamente los textos CSV decodificados y re-serializados con UTF-8 estricto, NO los bytes originales del archivo si el llamador transcodifica.

`Verify` revalida preflight, UTC, plazo, firma fija en tiempo, secreto y contenido; retorna códigos canónicos sin eco de campos. Cinco pruebas unitarias sintéticas: no permitir ticket sin inputs/clave; tokens falsos; fixture válido sin apply; alteración y clave incorrecta; expiración/futuro; hojas vacías/inesperadas. No hay migración ni integración PostgreSQL en este cambio: la fase N del PR #716 ya cubrió esa base de datos y el paso idempotente real sigue bloqueado.

PR #718 DRAFT en `feature/initial-load-preflight-receipt-694-20261024-gpt`; enlaces verificados por GitHub. Primer HEAD completo de código y pruebas `9f22534a09e09ae216ed30f1ae6df3d6b771492c`; corrección de caducidad `d0b1801cab8ff9a983d74c280be52375752d3f99`. Requerir Gates exact-head nuevos y comparar dev vivo antes de merge.
