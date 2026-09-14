# PMGM-QA-V028 — Cola de corroboración

## Objetivo

Validar el vertical slice v0.28 de Régimen Interior desde la detección read-only v0.27 hasta el cierre documentado de un caso, preservando la fuente de verdad institucional.

## Escenario funcional principal

1. Ingresar con rol autorizado de Régimen Interior.
2. Abrir `Calidad de datos`.
3. Seleccionar una observación vigente y usar `Abrir caso de corroboración`.
4. Confirmar que la cola presenta un único caso `open`.
5. Repetir la apertura del mismo hallazgo y confirmar que no se crea un duplicado activo.
6. Abrir `Cola de corroboración` y tomar el caso.
7. Confirmar cambio a `under_review`, nombre visible del responsable y condición `assignedToCurrentUser`.
8. Intentar tomar el mismo caso con otro actor y confirmar conflicto.
9. Registrar resolución con mínimo 10 caracteres y una referencia de respaldo.
10. Confirmar estado `resolved_confirmed` o `dismissed` y tres movimientos en historial: apertura, toma y cierre.
11. Verificar que el caso cerrado no puede ser tomado nuevamente.
12. Volver a abrir un caso para el mismo hallazgo sólo después de que el caso anterior está cerrado.

## Privacidad y seguridad

Validar que:

- la respuesta API no expone `AssignedToSubject`, `CreatedBySubject`, `ResolvedBySubject` ni `ActorSubject`;
- la interfaz utiliza únicamente nombres visibles autorizados y `assignedToCurrentUser`;
- `ResolutionSummary` y `EvidenceReference` no aparecen en logs;
- el respaldo se guarda como referencia y no como contenido documental;
- no existe exportación masiva por defecto;
- respuestas con datos usan `Cache-Control: private, no-store`;
- cliente usa Bearer token, `credentials: omit`, `cache: no-store`, `redirect: error` y mismo origen.

## Integridad / concurrencia

- El índice parcial debe impedir más de un caso activo por fingerprint.
- `claim` debe ejecutar una transición condicional: sólo un consumidor puede cambiar `open → under_review`.
- `resolve` debe ejecutar una transición condicional desde `under_review` y respetar al responsable asignado.
- El evento de workflow y el cambio de estado deben quedar dentro de la misma transacción PostgreSQL.

## No regresión

CI debe cubrir además:

- compilación backend .NET 10;
- unitarias y PostgreSQL existentes;
- S3/MinIO;
- ClamAV;
- frontend lint/build/tests;
- frontend container;
- showcase container;
- configuración Docker;
- gate Ley 21.719;
- gate de clasificación, incluyendo el catálogo de casos;
- migration safety gate.

## Regla de merge

El PR sólo puede pasar de draft a ready y fusionarse a `dev` cuando todos los jobs anteriores estén verdes sobre el mismo SHA de head.
