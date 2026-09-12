# PMGM-BLG-045 — Localización Español (Chile)

**Prioridad:** P0 transversal  
**Estado:** EN DESARROLLO  
**Requisito relacionado:** PMGM-REQ-024

## Alcance
- cultura predeterminada `es-CL`;
- interfaz completa en Español (Chile);
- terminología institucional de la Gran Logia Mixta de Chile;
- zona horaria institucional de presentación `America/Santiago`;
- moneda predeterminada `CLP`;
- formatos chilenos para fechas, horas, montos y RUT en presentación;
- mensajes de validación y error orientados al usuario en español;
- documentos, reportes y notificaciones en español;
- recursos de localización separados de códigos internos;
- persistencia y APIs técnicas independientes del formato regional.

## Implementación inicial
- backend configurado con cultura `es-CL`;
- metadatos del sistema declaran cultura, zona horaria y moneda institucional;
- mantener UTC para instantes temporales persistidos;
- mantener `DateOnly` para fechas civiles/institucionales sin hora;
- mantener códigos internos estables y traducirlos en la capa de presentación.

## Criterios de aceptación
1. ninguna pantalla del MVP presenta textos técnicos en inglés al usuario final salvo nombres propios o términos aprobados;
2. fechas, horas y montos se muestran de acuerdo con Español (Chile);
3. documentos oficiales y reportes salen en Español (Chile);
4. la conversión horaria utiliza `America/Santiago`;
5. RUT se normaliza para procesamiento y se formatea sólo al presentar;
6. la localización no altera claves, códigos internos ni tipos de datos de persistencia;
7. pruebas verifican la cultura predeterminada y los formatos institucionales críticos.
