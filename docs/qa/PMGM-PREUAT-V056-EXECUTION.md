# PMGM Pre-UAT — preparación operacional QA v0.56

**Estado:** listo para ejecución institucional, no aprobado  
**Fecha de corte:** 2026-09-13  
**Rama:** `dev`  
**SHA verificado:** `d83d37a22998f2750dce434e10dd728794fdc6f2`  
**Interfaz:** `UI QA v0.56`

## 1. Propósito

Este documento fija el candidato Pre-UAT posterior a la integración del PR #70. No sustituye la UAT de 20 casos ni la aprobación del Sponsor/Product Owner. Su objetivo es impedir que se pruebe accidentalmente el SHA anterior o que se confunda CI verde con aceptación institucional.

## 2. Evidencia técnica cerrada sobre el SHA

| Control | Ejecución | Resultado |
| --- | ---: | --- |
| PMGM CI | #1069 | `success` |
| PMGM CI | #1070 | `success` |
| Showcase GitHub Pages | #305 | `success` |
| Paquete instalable Pre-UAT | #265 | `success` |
| Pruebas frontend | 119/119 | `pass` |
| Lint y build frontend | local + CI | `pass` |
| Backend, infraestructura y servicios | CI | `pass` |
| Privacidad Ley 21.719, clasificación, migraciones y release | gates CI | `pass` |

Estos resultados prueban reproducibilidad técnica. Los 20 casos de aceptación siguen en estado institucional `pending` hasta ejecutarse en `pilot-operational`.

## 3. Datos de prueba obligatorios

- usar únicamente datos ficticios o expresamente autorizados;
- simular 20 Talleres;
- cada Taller debe mostrar 12 Maestros, 5 Compañeros, 5 Aprendices y 2 PAS activos;
- total esperado: 480 registros activos, distribuidos en 240 Maestros, 100 Compañeros, 100 Aprendices y 40 PAS;
- no subir datos, fotografías ni documentos personales reales a la demo pública.

## 4. Prueba rápida previa a la UAT formal

1. abrir el showcase e identificar `UI QA v0.56`;
2. comprobar el total Orden de 480 y el detalle de 24 registros en al menos dos Talleres;
3. cargar una fotografía ficticia JPG o PNG, máximo 10 MB y mínimo 500 × 500 píxeles;
4. cargar antecedentes ficticios `.docx` y PDF, máximo 50 MB;
5. confirmar que las cargas existen durante la sesión y desaparecen al recargar;
6. recorrer el expediente desde ficha hasta solicitud de Iniciación;
7. comprobar un bloqueo por requisito pendiente o rechazo en Cámara del Medio;
8. comprobar que perfiles institucionales distintos ven y ejecutan sólo sus acciones;
9. verificar que el reporte de Régimen Interior conserva los totales acordados;
10. iniciar el paquete Pre-UAT y ejecutar sus smokes antes de comenzar la Ola A.

## 5. Ejecución institucional

La UAT formal conserva los 20 casos definidos en `release/PMGM-UAT-1.0.0-rc1.template.json`, organizados en las cinco olas del plan operativo. Para comenzar una ejecución nueva se debe:

1. congelar un nuevo SHA candidato desde `dev`;
2. desplegar exactamente ese SHA en `pilot-operational` con HTTPS/OIDC;
3. copiar la plantilla de evidencia fuera del repositorio cuando vaya a contener referencias sensibles;
4. registrar URL, timestamp, rol, resultado y referencia de evidencia por caso;
5. abrir defectos P0/P1 separados y repetir la regresión sobre un nuevo SHA cuando corresponda;
6. mantener `main` y la RC1 anterior sin cambios hasta obtener 20/20 `pass` y aprobación expresa.

## 6. Gate de salida

El candidato sólo puede avanzar a una nueva RC cuando coincidan:

- CI verde sobre el SHA congelado;
- paquete instalable reproducible sobre el mismo SHA;
- UAT 20/20 `pass`;
- backup, restore y smoke post-recuperación aprobados;
- revisión de privacidad/legal antes de datos reales;
- aprobación expresa del Sponsor/Product Owner.
