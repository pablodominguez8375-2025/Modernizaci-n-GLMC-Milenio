# QA525 — reconciliación del operador único post-DB — 2026-10-30

## Fuente y causa

- PO aprobó propuesta #525/6527378960 y recordó definición de primera implementación.
- [Decisión registrada #525/6528024401](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6528024401).
- [Antecedente #525/6512522202](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6512522202): Sponsor ejecuta migración #801 supervisada y revisión automatizada ChatGPT, sin presentarla como DBA humano.
- Primera implementación v0.30 describe QA operable desde launcher sin requisito de DBA externo; no regula por sí sola el update de glmdch.
- Entry point posterior conservó exigencia exclusiva de DBA independiente. ChatGPT no reconcilió el antecedente al tratarlo como bloqueo nuevo; se corrige con aprobación actual del PO.

## Rama y alcance

- PR #806, base dev `ceaca327cacb983850a66a9dbdb41a8d4a3c4316`.
- Rama `fix/qa525-single-operator-postdb-20261030-gpt`.
- main `6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin promoción.
- Entry point existente + sus tests + dos contratos QA + este handoff.
- No migraciones, aplicación/UI, workflows, ni cambios en servidor.

Modo nuevo solo con `--single-operator-qa` tras comando prepare/apply, acta root
separada y reconocimiento explícito de revisión IA sin humano independiente.
No fallback si falla ruta DBA. Conserva ruta anterior y verifica SHA/digest,
blobs únicos PR801, live99/schema, G6 post-DB fresco, flags runtime desactivados,
aprobación temporal, locks, Compose/identidad y rollback dual. Recibo preparado
ligado al modo de revisión. No se generan actas ni se afirman revisiones ajenas.

## Verificación

- Local: 77 pruebas PASS (entrypoint43, transacción dual13, schema/probe21).
- Casos nuevos: opt-in, rechazo de registro cruzado/DBA ausente sin fallback,
  atribución falsa, aprobación ausente, scope/host incorrectos, SHA/digest/blob
  distintos, vencimiento, G6 inválido, schema vivo alterado y flags runtime.
- CI del HEAD final y publicación de nuevo corte: consultar PR806; no asumir PASS.

## QA físico conservado

En glmdch: staging firmado ceaca327 PASS; EF99 y forma física exacta PASS;
G6 postDB Drive/restore ya registrados PASS. API+WEB nuevos **no desplegados**;
prepared.json y tres registros locales ausentes según salida del PO.
Migraciones de startup false; flags de cuentas ausentes en entorno. Revisar
otras fuentes de configuración antes de afirmar el estado efectivo.

## Continuidad

1. CI exact-head y revisión PR806; integración permitida del correctivo QA bajo autorización PO.
2. Nuevo corte firmado y verificación en host. El paquete ceaca327 no contiene este modo.
3. Completar revisión técnica del corte y decisión real del operador; registrar
   actas privadas vigentes con identidad/tiempos/hashes auténticos. No compartirlas.
4. Preparar imágenes sin recrear servicios, aplicar supervisado, comprobar salud/
   versión/OIDC/SMTP/roles y UAT sintética; preservar rollback dual.
5. Reutilizar EF99 y G6 mientras sigan válidos; no repetir por cambio de chat.

PR804/805 separados del corte; main/producción/srv01 excluidos. Actualizar #525
y Estado Ejecutivo Drive con commit, CI y evidencias físicas nuevas. Un PR o CI
verde no acredita deploy, UAT ni revisión humana independiente.
