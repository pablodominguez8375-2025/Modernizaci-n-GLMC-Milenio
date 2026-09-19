# PMGM-NEXT-001 — Siguiente corte técnico

## 1. Hito operacional bloqueante vigente: QA srv01

Issue #97 continúa abierto. El despliegue físico en `srv01` sigue siendo obligatorio antes de UAT/promoción a `main`.

El corte operativo debe usar siempre el HEAD vivo de `dev` y verificar paridad:

`HEAD dev = Pages = qa-current.json = BUILD-INFO del ZIP QA`.

Orden operacional:

1. tomar HEAD vivo de `dev`;
2. verificar Pages + `qa-current.json` + instalable;
3. desplegar el SHA exacto en `srv01`;
4. validar SHA-256, BUILD-INFO y MANIFEST;
5. ejecutar smoke autenticado;
6. ejecutar regresión QA completa;
7. corregir P0/P1;
8. congelar candidato UAT;
9. ejecutar UAT institucional;
10. promover a `main` sólo con aprobación expresa.

## 2. Avance funcional en paralelo

El Sponsor / Product Owner autorizó continuar desarrollo, pruebas y documentación aunque Issue #97 continúe abierto.

Esto no reemplaza QA/UAT ni permite declarar operacional un SHA no desplegado físicamente.

## 3. Funciones integradas que no son trabajo futuro

- Secretaría integral: PR #105.
- Insinuaciones: PR #106.
- Cierre documental de Tenidas: PR #110.
- Cuadro Mensual de Tesorería: PR #112.
- Continuidad Tesorería: PR #113.

## 4. Incremento funcional activo — PR #114

PR #114: `feat(hospitalaria): flujo integral Taller y Gran Hospitalaria`.

Objetivo: completar Hospitalaria conforme al art. 12.13, sin mezclar el Tronco de Beneficencia con Tesorería y sin exponer información sensible a Gran Hospitalaria.

Criterios mínimos:

1. Hospitalario gestiona exclusivamente Hospitalaria de su Taller;
2. Secretaría no administra Hospitalaria;
3. Venerable lee/inspecciona y aprueba egresos sin editar movimientos;
4. socorros pueden autorizarse por Venerable o por acuerdo auditable del Consejo;
5. acuerdo del Consejo debe ser `benevolence_aid_proposal`, aprobado, mismo Taller y monto exacto;
6. aportes/Tronco y socorros permanecen separados de Tesorería;
7. estado mensual se revisa en Consejo;
8. rendición mensual bloqueada mientras existan egresos pendientes;
9. reposición pagada requiere referencia/comprobante;
10. Gran Hospitalaria recibe sólo cifras agregadas y referencias institucionales;
11. la proyección superior no contiene beneficiario, destino ni observaciones privadas;
12. Gran Hospitalaria puede observar o conciliar;
13. conciliación genera regularidad institucional consumida por Ceremonias;
14. demo reproduce Hospitalario/Venerable/Gran Hospitalaria;
15. QA-025 protege flujo y privacidad;
16. matriz srv01 queda en 25 controles.

Fuente técnica: `docs/PMGM-ARCH-011-hospitalaria-flujo-integral.md`.

## 5. Después de PR #114

No fijar un nuevo incremento por conversación antigua.

Después de integrar Hospitalaria:

1. reconsultar HEAD vivo de `dev`;
2. revisar Estado Maestro, ADRs, PRs, Issue #97 y Línea Base Maestra;
3. revisar backlog vigente y documentos oficiales de Drive;
4. seleccionar el siguiente bloque sin reabrir funciones integradas ni inventar alcance.

## 6. QA vigente en esta rama

La rama de PR #114 eleva el kit a **QA-001..QA-025**.

QA-025 valida Hospitalaria integral:

- independencia del Tronco;
- autorización Venerable/Consejo;
- revisión mensual Consejo;
- privacidad;
- rendición agregada;
- reposición;
- conciliación;
- regularidad institucional.

Después del merge, Issue #97 y Línea Base Maestra deben actualizarse al nuevo HEAD vivo y a los artefactos post-merge del mismo SHA.
