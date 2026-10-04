# Registro de salida — búsqueda mínima de identidad en Admisiones

Issue #275 / PR draft #276; rama `feature/admissions-residual-275`; base funcional `5f3df076a1bdf1c4c68496f4f4b490a0867bc272`; dev observado `b070dbc71d25f811f0b70999dab8f378a5343cc8`. Continúa [registro anterior CRV](2026-10-03-issue-275-crv-contrato-pruebas.md); SHA entregado en recibo GitHub/Drive. Cobertura parcial, no diccionario de todo el proyecto ni schema instalado.

## Contrato nuevo

GET `/api/admisiones/personas-busqueda`: organizationId UUID requerido, admissionType `affiliation/incorporation`, query texto de 3..80 caracteres luego de trim. Máximo 20 resultados, sin total institucional ni paginación/exportación. Respuesta `returned` entero y `items`:

| Campo | Tipo/nulabilidad | Significado/validación |
|---|---|---|
| personId | UUID no nulo | Identidad maestra existente, nunca se crea desde la búsqueda |
| memberId | UUID nullable | Miembro institucional existente en afiliación; null en incorporación |
| displayName | texto no nulo | FirstNames + LastNames, identificación mínima |
| institutionalNumber | texto nullable | Número existente; coincidencia exacta sin distinguir mayúsculas permite ubicar otro Taller |

Datos institucionales protegidos: se mantienen clasificación/finalidad de identidad y trazabilidad del catálogo vigente. No RUT, contacto, ficha, grado, estados financieros, historial ni documentos en respuesta. UUID técnico no concede acceso a ficha.

## Acceso, estructuras y antes/después

Antes la UI consultaba sólo membresías activas del destino y cargaba el perfil completo para conocer PersonId; no podía reutilizar un retirado/de otro Taller ni Persona externa sin Member. Ahora usa un contrato específico con controles backend de organización y finalidad. Afiliación: Secretaría/gestores ya autorizados buscan nombres en historia del Taller, incluso cerrada; fuera de ese Taller requieren número exacto. Evaluadores centrales conservan alcance institucional existente. Incorporación: sólo Personas sin Member vinculadas a expedientes de incorporación del Taller autorizado (o alcance central de evaluación). No se consultan insinuados ni People global por nombre. El alta de Persona externa nueva permanece pendiente; este endpoint no la simula ni crea membresía anticipada.

Permiso existente CanManageOrganization o CanEvaluateCeremonies; no cambio en Authorization/#267 ni nueva autoridad institucional. Cache-Control private,no-store; auditoría `admission.identity.lookup` sólo tipo y cantidad, sin término buscado ni identidades de resultados.

Sin migración: nuevas proyecciones sobre People/Members/Memberships/AdmissionCases existentes, sin tabla, columna, PK/FK, unicidad, índice, cardinalidad ni borrado nuevos. Person → Member conserva relación existente; los vínculos de expedientes no se reescriben. Sin backfill/cambio de estados. El selector conserva PersonId/MemberId y exige selección explícita; limpia selección al cambiar término/Taller/tipo y descarta respuestas tardías. Deja de llamar a getProfile/getMembers para este flujo.

Demo usa tres identidades sintéticas (retirado local, otro Taller por número y Persona externa con expediente). Reproduce alcance local del selector; la variante central amplia y alta/resolución persistente del circuito siguen pendientes. No afirmar equivalencia integral del circuito.

## Pruebas y control

Siete pruebas frontend de contrato/demo: retirado, búsqueda exacta externa, Persona sin Member sólo contexto autorizado, límites de consulta y transporte/no-cache. Suite completa local 340/340 PASS, lint/build PASS. Prueba HTTP PostgreSQL específica comprueba nombres de retirado local, número ajeno exacto vs parcial, Persona externa visible vs expediente ajeno/insinuado privado, DTO mínimo/no-cache, organización ajena y Tesorero denegados, término corto/tipo inválido y ausencia de membresía anticipada. Sigue contrato ambiental PMGM_TEST_POSTGRES; no atribuir ejecución DB si variable ausente. Backend requiere CI propio: sin SDK local.

Pendientes: alta segura/única de persona externa nueva, UI/circuito integral, contexto de selección en altas por ID directo, concordancia CRV/evidencia, comisión/autoridades, materialización/traslado idempotentes. Main congelado; srv01 pausado; despliegue QA pendiente, sin QA física/UAT. Evidencia de 5f3df07 no certifica este nuevo corte.
