# #533 C21 — trazabilidad de Venerable Maestro acreditado al asambleísta

Inicio 23-10-2026 · base dev@95c98d8385a20062513b6cc0f6200d9c776269c3 · responsable agente:chatgpt.

## Alcance controlado
- Extender únicamente `AssemblyMasterTermEvidenceAdapter` y sus tests para proporcionar ID y referencia de una **fuente VM verificada**, asociada a la categoría acreditada (representante/permanente).
- Elegir el período acreditado de manera determinista cuando hay reelecciones; sin inferir cumplimiento sólo de `EndDate`.
- En ausencia de certificación íntegra/no calificación, no proporcionar identidad de fuente apta para padrón.
- Registrar contrato y GOV004; sin migraciones, endpoints, permisos, foto/RUT, certificados inventados, apertura de sufragio ni cierre/exportación oficial.

## Dependencias y límites
Claude conserva UI y endpoints de listados/bloqueos #639/#640; no se tocan. Faltan lector real de constancias institucionales VM, certificación histórica de Gran Tesorería, fuentes de Tribunal de Honor y Régimen Interior, materialización auditada de registros, preflight de cierre real y PDF/CSV oficial. **No cerrar #533** con este incremento.

Puertas para integrar sólo a dev: PMGM CI, Showcase e Instalador QA 3/3 SUCCESS exact HEAD, behind dev=0 y control de conflictos. Sin main, QA físico ni datos reales.

## Implementación C21
Se añadió salida `AccreditedSource` sólo cuando la categoría cumple requisitos y la constancia de V.M. pasó el adapter; contiene OfficeAssignmentId, OrganizationId, referencia de acta de certificación y período. Selección determinística: para permanente, período completo más temprano; para representante, período vigente acreditado más reciente. No confundir OrganizationId de fuente histórica con Taller de membresía a la fecha de corte. Categoría no calificada y fallos de cobertura dejan fuente nula. Pruebas unitarias de ambos casos, reelección, duplicidad e invalidez. Sin datos reales. Faltan conectores y materialización institucional; la PR no habilita sufragio ni cierra #533.
