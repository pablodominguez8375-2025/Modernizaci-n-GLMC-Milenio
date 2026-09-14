# PMGM-REQ-038 — Aprobaciones administrativas y matriz de firmantes

**Estado:** pendiente de implementación en demo/UAT  
**Rama objetivo:** `dev`  
**Fecha:** 2026-09-14  
**Origen:** acuerdos funcionales del Proyecto Centenario y revisión de formularios oficiales 2026.

## 1. Aprobación obligatoria del Venerable Maestro

El Venerable Maestro es la máxima autoridad administrativa del Taller. Por ello, todo egreso imputado al Taller debe permanecer en estado `pending_approval` hasta contar con su aprobación explícita.

Aplica como mínimo a:

- egresos de Tesorería del Taller;
- egresos de Hospitalaria del Taller;
- transferencias, pagos o reposiciones que comprometan fondos del Taller;
- solicitudes excepcionales que el parámetro institucional defina como administrativas críticas.

La aprobación debe registrar:

- usuario y cargo aprobador;
- fecha y hora;
- IP o contexto de conexión;
- monto, moneda y concepto;
- beneficiario;
- documento o respaldo asociado;
- decisión: aprobado, observado o rechazado;
- observación de la decisión;
- versión del registro aprobado.

Un usuario no puede aprobar su propio egreso cuando sea quien lo registra, salvo una excepción formal parametrizada y auditada.

## 2. Orador del Taller

El Orador es un firmante institucional del Taller y debe intervenir en los procedimientos que la matriz de documentos defina. Como mínimo, la Carta de Retiro debe contemplar firma o visto bueno del Orador junto con los firmantes correspondientes.

El sistema debe permitir configurar:

- documentos que requieren firma del Orador;
- orden de firma;
- firma conjunta o individual;
- reemplazo temporal por subrogancia;
- vigencia del cargo;
- bloqueo de emisión mientras falte una firma obligatoria.

## 3. Firmantes del Taller

La matriz base debe contemplar, según el trámite:

- Venerable Maestro;
- Secretario/a;
- Orador/a;
- Tesorero/a;
- otros cargos del Taller cuando el procedimiento lo requiera.

Los cargos deben resolverse desde el período vigente del Taller y conservar el histórico de quién firmó cada documento.

## 4. Firmantes y validadores de la Orden

Los procesos de Orden deben poder exigir, según tipo de trámite:

- Gran Tesorero/a;
- Gran Hospitalario/a;
- Gran Secretario/a;
- Gran Maestra/o;
- Régimen Interior como órgano validador;
- otros dignatarios configurados institucionalmente.

Régimen Interior valida datos maestros y condiciones de procedencia; no debe reemplazar la aprobación financiera de Gran Tesorería ni la validación de reposiciones de Gran Hospitalaria.

## 5. Integración con ceremonias y documentos

La emisión de una autorización o documento oficial debe conservar un snapshot de:

- requisitos cumplidos;
- aprobaciones y firmas;
- cargos y períodos vigentes;
- evidencias documentales;
- fecha y hora de cada acción;
- usuario que ejecutó la acción.

Si falta una aprobación o firma obligatoria, el documento no puede pasar a estado emitido.

## 6. Integración con datos oficiales 2026

El formulario de insinuación aporta, entre otros, datos personales, laborales, Taller que presenta, fechas de ingreso/presentación y Secretario/a responsable.

El extracto de acta contempla la participación de Venerable Maestro, Secretario/a y Orador/a, junto con la estructura de cargos y asistencia del Taller.

El cuadro de pagos de Gran Tesorería aporta la nómina por grado, cargo, cuota, rebajas autorizadas, forma de pago y diferencia de conciliación.

Estos documentos son fuentes de diseño y no autorizan cargar datos reales en la demo pública.

## 7. Criterios de aceptación

- [ ] Un egreso nuevo queda pendiente de aprobación del Venerable Maestro.
- [ ] El Venerable Maestro puede aprobar, observar o rechazar.
- [ ] Tesorería y Hospitalaria conservan separación de funciones.
- [ ] La Carta de Retiro bloquea emisión si falta el Orador u otro firmante requerido.
- [ ] La matriz de firmantes es configurable y versionada.
- [ ] Toda decisión genera evento auditable.
- [ ] Un cambio posterior no modifica el snapshot de un documento emitido.
- [ ] El flujo integrado se prueba en QA con identidad, API y PostgreSQL reales.
