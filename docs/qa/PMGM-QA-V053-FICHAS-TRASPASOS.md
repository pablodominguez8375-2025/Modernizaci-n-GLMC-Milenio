# PMGM QA v0.53 — Fichas, historial y traspasos

## Objetivo

Consolidar la ficha institucional del hermano como registro único y mantener la continuidad cuando cambia de Taller, sin perder el historial del Taller de origen.

## Criterios de aceptación

- El padrón permite filtrar por Taller, nombre, número institucional y estado.
- La ficha muestra identidad, grado, cargos, regularidad y eventos institucionales según el ámbito autorizado.
- Un traslado ejecutado cierra la pertenencia de origen y crea la pertenencia destino con fecha efectiva, resolución y referencia de respaldo.
- El historial anterior permanece consultable y no se duplica la identidad personal.
- Los datos de contacto se minimizan por rol; una consulta de padrón no expone información innecesaria.
- Una transferencia pendiente o aprobada no puede duplicarse para el mismo hermano.
- Las operaciones de solicitud, aprobación y ejecución quedan auditadas.

## Evidencia técnica

- Frontend: `MemberDirectoryPage` y `membershipApi`.
- API: `MembershipEndpoints` y `TransferEndpoints`.
- Pruebas: `LodgeManagementHttpWorkflowTests`, `MemberSelfServicePostgreSqlTests` y pruebas de traspaso.

## Próximo paso UAT

Ejecutar dos casos con datos ficticios: un hermano activo sin traslado y un hermano trasladado, verificando que ambos recorridos conservan el historial y respetan la minimización de datos.
