# 2026-09-18 — Secretaría integral, Cuadro histórico y Gran Secretaría

## Alcance

Incremento funcional para completar las funciones de Secretaría del Taller y Gran Secretaría antes de la puesta en marcha institucional real.

## Secretaría del Taller

- carga individual de hermanos que ya integran la Orden;
- plantilla/importación XLSX para carga masiva;
- borrador → envío a Régimen Interior → aprobación/observación/rechazo;
- fechas históricas pueden quedar desconocidas;
- grado actual puede quedar validado aunque falten hitos exactos;
- cargos con fechas históricas opcionales;
- actualización de hermano existente por identidad institucional;
- Reuniones privadas Programada/Realizada;
- registros documentales de Tenidas/Reuniones/Consejos.

## Tenidas

- Programada / Realizada;
- Presencial / Virtual;
- ubicación para presencial;
- referencia restringida de acceso para virtual;
- plancha del hermano opcional y cargable posteriormente;
- plancha PDF/DOCX + hermano autor;
- iniciación, aumento de salario y exaltación no llevan plancha del hermano;
- extracto obligatorio en PDF antes de remitir a Gran Secretaría;
- acta completa opcional y privada.

## Gran Secretaría

- recibe exclusivamente datos básicos de Tenidas + Extracto PDF;
- no recibe Reuniones ni Consejos;
- no recibe plancha del hermano ni acta completa;
- descarga de extractos auditada;
- recepción/observación de extractos.

## Documentos formales

- Decreto permanece como categoría independiente;
- Plancha de Gran Secretaría = comunicado formal o autorización formal;
- Plancha de Autorización de Ceremonia no tiene carácter de Decreto;
- compatibilidad con códigos documentales históricos.

## Calidad técnica

- ClosedXML para plantilla/importación XLSX;
- reutilización de Gestión Documental segura;
- migración `20260918190000_AddSecretariatFullWorkflow`;
- corrección de metadata EF en migraciones manuales históricas afectadas;
- migration gate reforzado;
- tests unitarios e integración de carga histórica y privacidad.

## Estado

PR #105. No considerar integrado hasta que PMGM CI, Showcase e instalable QA estén verdes sobre el HEAD final y el PR sea fusionado a `dev`.
