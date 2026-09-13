# PMGM QA v0.49 — Balotaje definitivo

## Objetivo

Hacer operativa la etapa de balotaje definitivo del circuito de Iniciación, respetando la estructura del `EXTRACTO-DE-ACTA-2026.docx` y usando solamente datos ficticios en la demostración pública.

## Reglas implementadas

- El balotaje sólo puede realizarse después de una revisión favorable de tercer grado y del plazo de publicación configurado.
- Se admite entre uno y tres trámites, identificados como primero, segundo y tercero.
- Cada número de trámite puede aparecer una sola vez.
- Cada trámite registra personas habilitadas, balotas blancas y balotas negras.
- La suma de balotas blancas y negras debe coincidir con las personas habilitadas.
- El acuerdo final se consigna como favorable o desfavorable; no se inventa un umbral reglamentario no definido por la fuente.
- La referencia al extracto de acta es obligatoria y admite hasta 240 caracteres.
- Se conservan sólo los escrutinios agregados. No existe relación entre una persona y su balota.

## Contrato técnico

`POST /api/insinuados/solicitudes/{requestId}/balotaje`

El cuerpo contiene `ballotDate`, `ballots`, `ballotApproved` y `sourceReference`. Cada elemento de `ballots` contiene `procedureNumber`, `eligibleVoters`, `whiteBallots` y `blackBallots`.

El backend verifica permisos del Taller, estado del expediente, fecha, publicación, aprobación de tercer grado, plazo mínimo, referencia documental y consistencia del escrutinio. La auditoría registra sólo cantidades agregadas.

## Prueba manual

1. Completar las cinco etapas anteriores del circuito.
2. Abrir “Balotaje definitivo”.
3. Registrar fecha, referencia y el primer escrutinio.
4. Agregar, si corresponde, segundo y tercer trámite.
5. Comprobar que el sistema impide avanzar si las balotas no coinciden con las personas habilitadas.
6. Registrar resultado favorable para continuar o desfavorable para detener el expediente.

## Verificación

- Pruebas frontend para escrutinio válido, inválido y contrato HTTP.
- Pruebas backend para cómputos, trámites únicos y aprobación.
- Versión visual: `UI QA v0.49`.
