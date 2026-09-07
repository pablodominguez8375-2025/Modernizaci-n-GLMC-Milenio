# PMGM-REQ-024 — Localización institucional Español (Chile)

**Estado:** Aprobado funcionalmente  
**Prioridad:** P0 transversal  
**Ámbito:** Todo el sistema  
**Fuente:** Definición del Product Owner

## 1. Objetivo
Todo el sistema Proyecto Milenio deberá utilizar **Español (Chile)** como idioma y configuración regional institucional por defecto para toda interacción humana.

El requisito aplica a portal, intranet, módulos administrativos, reportes, dashboards, formularios, notificaciones, documentos oficiales, mensajes de error, ayudas, correos generados y cualquier otra salida destinada a usuarios.

## 2. Configuración regional base
- Cultura de interfaz: `es-CL`.
- Idioma predeterminado: Español (Chile).
- Zona horaria institucional predeterminada para presentación: `America/Santiago`.
- Moneda predeterminada: peso chileno, código `CLP`.
- Fechas para presentación humana: día/mes/año según convención chilena.
- Horas para presentación humana: formato de 24 horas salvo necesidad institucional distinta.

## 3. Separación entre presentación y persistencia
La localización no debe contaminar la estructura técnica de los datos.

### Reglas
- Fechas y horas técnicas se almacenan en UTC cuando correspondan y se convierten a `America/Santiago` al presentar información institucional.
- Fechas civiles que no representan instante horario, por ejemplo fecha de iniciación o fecha de nacimiento, se modelan como fecha sin zona horaria.
- Valores numéricos y monetarios se almacenan como tipos numéricos, nunca como textos ya formateados.
- La API utilizará formatos técnicos estables e interoperables, por ejemplo fechas ISO 8601.
- Los códigos internos de estados, grados, trámites y reglas serán estables e independientes del idioma.
- La interfaz traducirá esos códigos a terminología institucional en Español (Chile).

## 4. Terminología institucional
Todo texto visible debe utilizar la nomenclatura oficial de la Gran Logia Mixta de Chile.

Ejemplos:
- Taller / Logia.
- Hermano / Hermana o la denominación institucional que se determine para cada contexto.
- Iniciación.
- Aumento de Salario.
- Exaltación.
- Retiro voluntario.
- Retiro forzoso.
- Reintegro.
- Oriente Eterno.
- Régimen Interior.
- Gran Secretaría.
- Gran Tesorería.

La terminología debe ser parametrizable cuando exista una decisión institucional pendiente o una denominación que pueda evolucionar.

## 5. RUT y otros identificadores
- El RUT no será clave primaria técnica.
- Cuando deba mostrarse, podrá presentarse con formato chileno para lectura humana.
- La validación y comparación debe realizarse sobre una representación normalizada.
- El formato visual no debe utilizarse como valor canónico de persistencia.

## 6. Moneda
Las cantidades monetarias institucionales se presentarán por defecto en pesos chilenos (`CLP`) usando convenciones de Español (Chile).

El almacenamiento deberá conservar el valor numérico y la moneda de forma explícita cuando el modelo pueda admitir más de una moneda en el futuro.

## 7. Reportes y documentos oficiales
Todo reporte y documento generado para uso institucional deberá:
- emitirse en Español (Chile);
- usar fechas y horas convertidas a la zona institucional cuando corresponda;
- utilizar terminología masónica/institucional oficial;
- formatear importes según convención chilena;
- mantener consistencia entre pantalla, PDF, XLSX, CSV y documentos oficiales.

En exportaciones técnicas como CSV, el formato deberá documentarse para evitar ambigüedad en fechas, separadores decimales y codificación.

## 8. Accesibilidad y calidad de redacción
- Mensajes claros y comprensibles para usuarios no técnicos.
- Ortografía y gramática en español formal de Chile.
- Evitar anglicismos visibles cuando exista una expresión institucional o técnica adecuada en español.
- Los mensajes de validación deben explicar qué debe corregirse y no exponer detalles internos del sistema.

## 9. Preparación para internacionalización
Aunque `es-CL` sea el único idioma requerido para el MVP, los textos de interfaz no deben quedar dispersos o incrustados de forma que impidan incorporar otra cultura en el futuro.

La arquitectura deberá permitir recursos de localización y traducciones futuras sin modificar el modelo de datos institucional.

## 10. Criterios de aceptación
El requisito se considera cumplido cuando:
1. la cultura predeterminada del backend y frontend es `es-CL`;
2. toda interfaz visible del MVP está en Español (Chile);
3. fechas, horas y montos se presentan según configuración chilena;
4. la zona horaria institucional es `America/Santiago` para visualización;
5. los datos persistidos permanecen independientes del formato de presentación;
6. reportes y documentos oficiales utilizan español y convenciones chilenas;
7. RUT y otros identificadores se formatean solo en la capa de presentación;
8. códigos internos no dependen de textos traducidos;
9. existe una estrategia de recursos/localización que permita evolución futura.