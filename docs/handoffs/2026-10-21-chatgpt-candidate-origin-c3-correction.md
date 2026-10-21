# Issue #613 C3 — corrección auditada del origen del candidato (21-10-2026)

Agente ChatGPT. Base inicial `dev@f27ee46905b9562902ab5216dcb85c4f1022b6bf` después de la integración C2, PR #622.

## Objetivo
Permitir corrección privada del origen únicamente a Secretaría de su Taller, exigiendo motivo, validación nuevamente de recomendante activo por fuente servidor, traza inmutable antes/después y protección de la independencia de entrevistadores. API y persistencia aditiva; UI/impresión formal quedan en coordinación con Claude.

## Controles y reservas
- Reutilizar `CandidateOriginPolicy.ValidateNew`; clientes legacy con origen NULL continúan sin forzar datos ficticios.
- Bloquear corrección de expedientes cerrados o si el nuevo recomendante estuvo designado entrevistador del expediente.
- Serializar con el mismo advisory lock PostgreSQL usado en asignación de entrevistadores; revalidar dentro de la transacción.
- Guardar las versiones anterior/nueva y el motivo en registro privado append-only con actor y fecha UTC. El evento de auditoría general solo guardará metadatos mínimos, sin identidad personal del recomendante ni motivo libre.
- Seguridad por Taller y sesión, sin exponer a listados públicos ni autorización implícita.
- Pruebas C3 HTTP/PostgreSQL, además de regresión C2; CI/Showcase/QA instalable exact-head obligatorios.
- No publicar en main, no desplegar a QA físico ni introducir datos reales.

## Estado
Reservada rama C3 para avance. Completar con SHA, archivos y gates. Si CI falla, NO fusionar.
