# Registro de salida — fecha de Carta de Retiro Voluntario

Issue #275 / PR #276, rama `feature/admissions-residual-275`. Base de este incremento: `450171df4a4abde7a23a83039a08373072aa7aab`; dev observado inicialmente `68bef4f4559fdfa0ea6ea617718a65e3573bd3a0`, posteriormente `b070dbc71d25f811f0b70999dab8f378a5343cc8`. Modelo implementado en rama, no schema instalado.

## Alcance y fuentes

Registro parcial del campo CRV y su cálculo; no diccionario exhaustivo de Admisiones. Complementa [DB-004](../PMGM-DB-004-diccionario-y-estructuras.md). Fuentes técnicas: AdmissionEntities.cs, AdmissionsDbContext.cs, AdmissionEndpoints.cs, migración 20261003113000 y contratos frontend. Fuente funcional: decisión del Sponsor registrada en #276, Constitución Art. 21 y Reglamento Art. 2.1. Se conserva evidencia manuscrita y revisión documental; una fecha declarada no las sustituye.

| Campo | Significado | Tipo / nulabilidad / default | Regla / clasificación |
|---|---|---|---|
| core.admission_cases.WithdrawalLetterGrantedDate | Fecha civil de otorgamiento de CRV, no fecha de carga | DateOnly? / PostgreSQL date; nullable, sin default | Obligatoria para nuevas afiliaciones, no futura en America/Santiago; dato institucional protegido por controles existentes de Admisiones |
| withdrawalLetterGrantedDate | Campo API de entrada y proyección backend | Fecha ISO YYYY-MM-DD o null | Contrato request opcional para compatibilidad de deserialización; la API exige valor en afiliación. El DTO frontend de respuesta aún no lo declara: pendiente |
| AffiliationMode / affiliationMode | Modalidad persistida y enviada al crear expediente | varchar(40) nullable; catálogo simple/activation | Hasta fecha CRV + 3 meses calendario inclusive: simple; después: activation. Frontend calcula, backend rechaza contradicción. No equivale a reintegro |

## Estructuras y antes/después

No cambia PK (`Id`), relaciones, índices ni borrados. El campo se añade a la tabla existente; no nueva entidad/FK. Se preservan índices de OrganizationId/AdmissionType/Status, PersonId y MemberId. La fecha no tiene índice ni CHECK SQL nuevo: validación de ingreso en API. Evidencias y decisiones mantienen FK AdmissionCaseId y borrado cascade existentes.

Antes no existía fecha CRV en el expediente; la modalidad era informada por el cliente. Ahora el campo se conserva y la API valida antigüedad al crear. El cálculo frontend anterior usaba setMonth y hora del navegador: podía desbordar fin de mes o cortar el último día al mediodía. Ahora ajusta al último día válido y compara fechas civiles de Chile, sin hora ni equivalencia de 90 días.

Migración: `20261003113000_AddWithdrawalLetterGrantedDate`, AddColumn nullable, sin backfill. Los registros antiguos conservan null; no se inventan fechas ni se reclasifican. Down elimina la columna y perdería sus valores: requiere respaldo y revisión antes de cualquier reversión. No ejecutada en srv01.

Sin nuevas tarifas, permisos, retención ni reportes. Se mantienen autorización y auditoría del expediente. No incluye datos personales ni documentos reales. La fecha declarada requiere confrontación con evidencia; no valida por sí sola retiro, firma o autorización.

## Pruebas y pendientes

Pruebas frontend ejecutables: límite inclusivo/día posterior, fin de mes, año bisiesto, cruce de año, fecha futura/inválida y fecha civil de Chile. El recibo GitHub identifica SHA exacto y resultado final; CI de otros SHA no certifica este cambio.

Pendientes explícitos: pruebas HTTP PostgreSQL dedicadas a fecha/modalidad y migración; paridad de validación del adaptador demo; DTO frontend de respuesta; coherencia de fecha con documento revisado; regla temporal en evaluaciones posteriores (actualmente modalidad congelada al crear); búsqueda de hermanos retirados/de otros Talleres y personas de otra Obediencia. No afirmar circuito completo ni aptitud operacional.

Main congelado, srv01 pausado, despliegue QA pendiente; sin instalación, QA física ni UAT. START-HERE no se modifica antes del merge.
