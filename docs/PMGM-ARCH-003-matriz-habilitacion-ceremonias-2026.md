# PMGM-ARCH-003 — Matriz de habilitación de ceremonias 2026

**Estado:** diseño funcional  
**Rama:** `dev`

## Objetivo

Reemplazar una validación global simple por una matriz de requisitos evaluables y trazables por solicitud.

## Requisitos comunes

Toda solicitud debe poder registrar como mínimo:
- validación de Régimen Interior;
- regularidad de Gran Tesorería;
- regularidad de Gran Hospitalaria;
- visto bueno de Gran Maestría cuando corresponda;
- identidad y Taller solicitante;
- evidencia documental asociada;
- resultado agregado derivado de los requisitos, nunca ingresado manualmente.

## Iniciación

Además de los requisitos comunes:
- expediente de insinuación habilitado;
- publicación mínima cumplida;
- votaciones previas registradas;
- derecho de ceremonia;
- Fondo de Defunción cuando corresponda;
- demás antecedentes exigidos por el protocolo.

## Afiliación

Además de los requisitos comunes:
- tipo simple o con activación;
- Carta de Retiro Voluntario verificada;
- historial masónico requerido según grado;
- nueva presentación/rechazo anterior cuando aplique;
- derecho y obligaciones financieras aplicables.

## Aumento de salario

Además de los requisitos comunes:
- grado de origen;
- antigüedad y actividad exigidas por regla vigente;
- asistencias/instrucciones/planchas requeridas por la fuente institucional aplicable;
- dispensa registrada cuando corresponda.

## Exaltación

Además de los requisitos comunes:
- antecedentes de iniciación y aumento de salario;
- actividad de 2.º grado;
- tenidas e instrucciones/cámaras exigidas por regla vigente;
- planchas requeridas;
- dispensa registrada cuando corresponda.

## Incorporación

Además de los requisitos comunes:
- Obediencia de origen;
- estado de Pacto de Paz y Amistad;
- antecedentes masónicos legalizados;
- Carta de Retiro Voluntario;
- aprobación de Gran Maestría si no existe pacto;
- demás requisitos reglamentarios aplicables.

## Estados de requisito

- `pending`
- `approved`
- `observed`
- `rejected`
- `not_applicable`
- `exception_approved`

## Estado agregado

- si algún requisito obligatorio está `rejected`: no autorizable;
- si existe requisito obligatorio `pending` u `observed`: no autorizable;
- sólo cuando todos los obligatorios están `approved`, `exception_approved` o `not_applicable` según corresponda: autorizable.

## Regla de emisión

Gran Secretaría puede emitir plancha únicamente desde una solicitud autorizable. La plancha debe quedar vinculada al snapshot de requisitos que justificó la autorización para impedir que una validación posterior altere retroactivamente la evidencia del acto administrativo.
