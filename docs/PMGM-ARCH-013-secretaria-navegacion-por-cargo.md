# PMGM-ARCH-013 — Navegación operativa por cargo de Secretaría

**Estado:** implementado en rama candidata  
**Fecha:** 2026-09-22  
**Base:** `dev` `822fd25b88caafdf5a01d89c27c53b6cd000f183`

## Decisión

La navegación lateral expone una sola entrada principal para cada responsabilidad secretarial:

- **Secretaría** para Secretaría del Taller;
- **Gran Secretaría** para el cargo institucional de Gran Secretaría.

Las tareas se agrupan dentro de cada espacio mediante navegación interna. La reorganización no crea permisos ni traslada atribuciones entre Taller y Orden.

## Secretaría del Taller

El espacio reúne:

1. Tenidas y actas;
2. Insinuados, incluido **Nuevo insinuado**;
3. Circuito de iniciación;
4. Cuadro del Taller;
5. Ficha del Taller;
6. documentos y respaldos.

Se conservan los contratos existentes de expediente único, privacidad del borrador, entrevistas, publicación, balotaje, solicitud de Plancha, cierre documental, correspondencia, agenda y pendientes.

## Gran Secretaría

El espacio reúne:

1. bandeja institucional;
2. revisión de insinuados;
3. circuito de iniciación;
4. ceremonias;
5. consulta mínima del Cuadro General de la Orden;
6. documentos oficiales.

Los documentos oficiales —incluidas Planchas y Decretos— no se generan ni firman en el sistema. Se registra su descripción y trazabilidad, y el original firmado físicamente debe cargarse en PDF. La navegación dirige la carga/consulta al Gestor Documental existente; el enlace específico entre cada PDF y su acto institucional continúa como brecha funcional separada y no se declara cerrado por esta reorganización.

## Permisos y no regresión

- Secretaría del Taller no administra Gran Secretaría, Tesorería, Hospitalaria ni Docencia.
- Gran Secretaría no obtiene acceso al detalle privado del Taller fuera de los mínimos ya autorizados.
- `Padrón` se reserva al **Padrón de la Gran Asamblea**.
- Biblioteca Virtual permanece como servicio transversal y no se duplica dentro del cargo.
- `main` no se modifica.

## Validación

- prueba de componente para ambos espacios operativos;
- regresión completa del frontend;
- compilación productiva;
- QA-030 debe comprobar menús y recorridos en 390×844 y escritorio para ambos perfiles.
