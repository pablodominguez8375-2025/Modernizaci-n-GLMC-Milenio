# PMGM-REQ-025 — Elegibilidad de ceremonias: regularidad institucional y publicación de insinuados

**Estado:** Aprobado v0.1  
**Prioridad:** P1 / MVP  
**Áreas:** Régimen Interior, Gran Tesorería, Gran Hospitalaria, Gran Secretaría, Talleres

## Objetivo
Asegurar que una ceremonia sólo pueda ser autorizada cuando se hayan cumplido las validaciones institucionales obligatorias y que dichas validaciones sean trazables, configurables y verificables.

## Regla general de autorización
Antes de emitir una autorización definitiva para una ceremonia, el sistema debe comprobar como mínimo:

1. aprobación de Régimen Interior respecto del hermano o candidato y sus antecedentes;
2. regularidad del Taller ante Gran Tesorería;
3. regularidad del Taller respecto de reposiciones u obligaciones ante Gran Hospitalaria;
4. cualquier requisito adicional configurado para el tipo de ceremonia;
5. para iniciaciones, cumplimiento del plazo mínimo de publicación del insinuado.

Gran Secretaría consume estas validaciones y emite el documento oficial; no debe recrear ni modificar la información fuente de cada área.

## Regularidad del Taller — Gran Tesorería
- La fuente responsable es Gran Tesorería.
- El estado debe poder consultarse a una fecha de corte.
- Estados iniciales sugeridos: `al día`, `moroso`, `pendiente`, `exento`.
- El sistema conservará evidencia y fecha de la validación utilizada para autorizar la ceremonia.
- No se duplicarán movimientos contables ni saldos dentro de Régimen Interior o Gran Secretaría.
- Una ceremonia queda bloqueada cuando el Taller no esté al día, salvo excepción formal configurada y debidamente autorizada/auditada.

## Regularidad del Taller — Gran Hospitalaria
- La fuente responsable es Gran Hospitalaria.
- Debe registrar si el Taller se encuentra al día en reposiciones u obligaciones hospitalarias requeridas.
- El estado se conservará históricamente por fecha de corte y evidencia.
- Una ceremonia queda bloqueada si existen reposiciones u obligaciones vencidas que la normativa institucional considere impeditivas.
- Las excepciones, si la normativa las permite, requieren resolución formal y auditoría.

## Portal de insinuados para iniciación
Para toda solicitud de iniciación debe existir una publicación vigente del insinuado en un portal institucional destinado a este proceso.

### Reglas mínimas
- El plazo mínimo inicial será de **20 días corridos**.
- El plazo no debe quedar codificado de forma fija: será configurable por la autoridad institucional.
- La configuración debe tener vigencia temporal para poder reconstruir qué regla se aplicó históricamente.
- El cómputo se realizará desde la fecha/hora efectiva de publicación hasta la fecha/hora en que se evalúa el requisito según la regla vigente.
- La autorización de iniciación queda bloqueada si el plazo mínimo no se cumple.
- Toda suspensión, retiro, corrección sustantiva o republicación debe quedar registrada para determinar correctamente el período válido de exposición.

### Registro de publicación
Cada publicación debe conservar como mínimo:
- identificador técnico del insinuado/candidato;
- Taller patrocinante/solicitante;
- fecha y hora de publicación;
- fecha y hora de cierre o retiro, si existe;
- estado de publicación;
- versión del contenido mostrado;
- regla de plazo aplicada;
- actor que publicó, modificó, suspendió o cerró;
- observaciones/evidencia cuando corresponda.

### Observaciones
El portal debe quedar preparado para recibir observaciones institucionales sobre una publicación cuando la normativa lo requiera. La visibilidad del portal, datos mostrados, destinatarios habilitados y tratamiento de observaciones serán configurables según política institucional y protección de datos personales.

## Motor de elegibilidad
El resultado de evaluación de una ceremonia debe ser explicable y utilizar estados:
- **Cumple**;
- **Observado**;
- **No cumple**.

Debe mostrar, como mínimo:
- aprobación de Régimen Interior;
- regularidad de Gran Tesorería;
- regularidad de Gran Hospitalaria;
- para iniciación: días exigidos, días válidos cumplidos y estado de publicación;
- requisitos adicionales configurables;
- razones concretas de cualquier bloqueo u observación;
- fecha de evaluación y fuentes utilizadas.

## Congelamiento de evidencia
Cuando Gran Secretaría emita la autorización, debe conservar una referencia inmutable a las validaciones y reglas usadas en ese momento. Cambios posteriores en Tesorería, Hospitalaria o configuración no deben alterar retrospectivamente el fundamento de una autorización ya emitida.

## Criterios de aceptación
1. Una solicitud no puede pasar a autorización definitiva si el Taller no está regular en Gran Tesorería.
2. Una solicitud no puede pasar a autorización definitiva si el Taller no está regular en Gran Hospitalaria cuando dicha obligación sea impeditiva.
3. Una iniciación no puede autorizarse si el insinuado no cumple el plazo de publicación configurado.
4. El plazo por defecto puede configurarse inicialmente en 20 días y cambiarse sin modificar código.
5. El sistema permite reconstruir qué regla, estados y evidencias fueron utilizados en una autorización histórica.
6. Gran Secretaría consume las validaciones de las áreas responsables sin duplicar sus datos maestros.
7. Toda excepción queda justificada, autorizada y auditada.
