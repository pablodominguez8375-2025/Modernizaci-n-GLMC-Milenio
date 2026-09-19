# PMGM-NEXT-001 — Siguiente corte técnico

## 1. Hito operacional bloqueante vigente: QA srv01

Issue #97 continúa abierto. El despliegue físico en `srv01` sigue siendo obligatorio antes de UAT/promoción a `main`.

Corte funcional de referencia al cerrar PR #112:

- merge funcional: `f2679b7cccd652d7cbb9fcd7688eee4d0c3a4bc3`;
- Pages + ZIP QA del mismo SHA: publicados;
- ZIP QA: `Proyecto-Centenario-QA-srv01-f2679b7cccd6.zip`;
- SHA-256: `cae2bae623554ec0b42daedc78dda3703dd1042a63d47b059d480485b45ba159`;
- matriz de regresión: **QA-001..QA-024**.

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

El Sponsor / Product Owner instruyó continuar desarrollo, pruebas y documentación mientras Issue #97 siga abierto. Esta autorización **no reemplaza QA/UAT** ni permite declarar operacional un SHA no desplegado físicamente.

## 3. Funciones ya integradas que no son trabajo futuro

No reabrir como “siguiente incremento”:

- Secretaría integral: PR #105;
- flujo reglamentario de insinuaciones: PR #106;
- continuidad Secretaría: PR #108;
- regla documental de cierre de Tenidas: PR #109;
- implementación Programada → Realizada → Cerrada: PR #110;
- continuidad post-merge de Tenidas: PR #111;
- Cuadro Mensual de Tesorería y segregación Taller / Gran Tesorería: PR #112.

## 4. Último incremento integrado — PR #112

PR #112 `feat(tesoreria): segregar Cuadro Mensual Taller y Gran Tesorería` quedó integrado y validado.

Reglas vigentes:

1. Tesorero del Taller crea/prepara el Cuadro sólo para su Taller;
2. la nómina deriva de membresías vigentes y conserva grado/cargo al corte;
3. rebajas/exenciones requieren referencia autorizante;
4. transferencias/depósitos se registran antes del envío;
5. pagador y referencia/comprobante son obligatorios;
6. Diferencia se calcula automáticamente;
7. envío bloqueado si Diferencia != 0;
8. envío bloqueado si hay identidades pendientes;
9. después del envío no se agregan pagos silenciosamente;
10. Gran Tesorería lista/revisa Cuadros enviados;
11. sólo Gran Tesorería concilia institucionalmente;
12. la conciliación emite la regularidad consumida por Ceremonias/reportes;
13. demo Pages y backend mantienen el mismo contrato;
14. QA-024 protege el flujo.

Fuentes: Constitución/Reglamento art. 12.12, `CUADRO PAGO GRAN TESORERÍA.xlsx`, Matriz Funcional Normativa de Cargos de Taller y Matriz Perfiles/Vistas/Firmas.

## 5. Incremento funcional siguiente — Hospitalaria del Taller + Gran Hospitalaria

Antes de programar, consultar nuevamente HEAD vivo de `dev`, Estado Maestro, PRs recientes, Issue #97 y fuentes actuales de Drive.

Objetivo: completar Hospitalaria reutilizando el módulo existente y manteniendo el **Tronco de Beneficencia independiente de Tesorería**.

Fuentes mínimas a contrastar:

- Art. 12.13 del Reglamento;
- formularios/planillas vigentes de Hospitalaria en Drive;
- reglas de reposiciones que participan en elegibilidad de ceremonias;
- matriz de perfiles/vistas/firmas;
- Línea Base Maestra vigente.

Primera entrega funcional esperada:

1. caja/estado mensual de Hospitalaria del Taller;
2. ingresos/aportes del Tronco de Beneficencia;
3. egresos/socorros con motivo, beneficiario minimizado y trazabilidad;
4. aprobación del Venerable Maestro para egresos;
5. balance/estado mensual disponible para Consejo de Administración;
6. reposiciones/deudas institucionales hacia Gran Hospitalaria;
7. validación/revisión de Gran Hospitalaria;
8. snapshot de regularidad institucional reutilizable por Ceremonias;
9. auditoría de altas, cambios, aprobaciones y conciliaciones;
10. demo funcional + instalable QA + nuevo control de regresión.

No crear un segundo módulo de Hospitalaria ni mezclar fondos de Hospitalaria con Tesorería.

## 6. QA vigente

La línea funcional de PR #112 deja **QA-001..QA-024**. Issue #97 y la Línea Base Maestra ya registran el corte funcional.

Cualquier PR documental posterior cambia el HEAD vivo de `dev`; por regla de frescura, el despliegue físico debe usar el HEAD que exista al momento de ejecutar Issue #97 y verificar que Pages/`qa-current.json`/ZIP correspondan al mismo SHA.
