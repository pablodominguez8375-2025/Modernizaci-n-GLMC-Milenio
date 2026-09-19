# PMGM-NEXT-001 — Siguiente corte técnico

## 1. Hito operacional bloqueante vigente: QA srv01

Issue #97 continúa abierto. El despliegue físico en `srv01` sigue siendo obligatorio antes de UAT/promoción a `main`.

Orden operacional:

1. tomar HEAD vivo de `dev`;
2. verificar Pages + `qa-current.json` + instalable del mismo SHA;
3. desplegar ese SHA exacto en `srv01`;
4. validar SHA-256, `BUILD-INFO.txt` y `MANIFEST.sha256`;
5. ejecutar smoke autenticado;
6. ejecutar regresión QA completa;
7. corregir P0/P1;
8. congelar candidato UAT desde el código realmente probado;
9. ejecutar UAT institucional;
10. promover a `main` sólo con aprobación expresa.

La automatización está documentada en `docs/installation/QA-SRV01-AUTODEPLOY.md` y el estado operacional dinámico se mantiene en Issue #97.

## 2. Autorización de avance funcional en paralelo

El Sponsor / Product Owner instruyó el 18-09-2026 continuar desarrollo, pruebas y documentación mientras Issue #97 siga abierto. Esta autorización **no reemplaza QA/UAT** ni permite declarar operacional un SHA no desplegado físicamente.

## 3. Funciones ya integradas que no son trabajo futuro

No deben reabrirse como “siguiente incremento”:

- Secretaría integral: PR #105;
- flujo reglamentario de insinuaciones: PR #106;
- continuidad Secretaría: PR #108;
- regla documental de cierre de Tenidas: PR #109;
- implementación Programada → Realizada → Cerrada: PR #110;
- cierre documental/continuidad post-merge: PR #111.

## 4. Incremento funcional activo — PR #112

PR #112: `feat(tesoreria): segregar Cuadro Mensual Taller y Gran Tesorería`.

Objetivo: completar el Cuadro de Pago oficial 2026 reutilizando el dominio Treasury existente y respetando las responsabilidades del Taller y de Gran Tesorería.

Criterios del corte:

1. Tesorero del Taller crea el Cuadro sólo para su Taller;
2. la nómina se deriva del Cuadro/membresías vigentes y conserva grado/cargo al corte;
3. rebajas/exenciones requieren referencia autorizante;
4. transferencias/depósitos se registran antes del envío;
5. Diferencia se calcula automáticamente;
6. envío bloqueado si Diferencia != 0;
7. envío bloqueado si hay identidades pendientes;
8. después del envío no se agregan pagos silenciosamente;
9. Gran Tesorería lista/revisa Cuadros enviados;
10. sólo Gran Tesorería concilia institucionalmente;
11. la conciliación emite la regularidad consumida por Ceremonias;
12. demo Pages y backend mantienen el mismo contrato;
13. QA-024 protege el flujo; matriz pasa a 24 controles.

Fuentes: Constitución/Reglamento art. 12.12, `CUADRO PAGO GRAN TESORERÍA.xlsx`, Matriz Funcional Normativa de Cargos de Taller y Matriz Perfiles/Vistas/Firmas 2026.

## 5. Siguiente incremento funcional después de PR #112

Continuar **Hospitalaria del Taller + Gran Hospitalaria**, reutilizando el módulo existente y contrastando antes de programar:

- Art. 12.13 del Reglamento;
- formularios/planillas vigentes de Hospitalaria en Drive;
- obligaciones/reposiciones que participan en elegibilidad de ceremonias;
- independencia del Tronco de Beneficencia respecto de Tesorería;
- aprobación del Venerable Maestro para egresos del Taller;
- estado mensual/caja y trazabilidad hacia el Consejo de Administración;
- fuente de verdad de Gran Hospitalaria para regularidad institucional.

No crear un segundo módulo de Hospitalaria ni mezclar sus fondos con Tesorería.

## 6. QA vigente del corte

En la rama de PR #112 el kit queda en **QA-001..QA-024**. QA-024 valida Cuadro Mensual y segregación Tesorero del Taller / Gran Tesorería.

Después de integrar PR #112, Issue #97 y Línea Base Maestra deben actualizarse al nuevo HEAD vivo y al instalable/Pages del mismo SHA.
