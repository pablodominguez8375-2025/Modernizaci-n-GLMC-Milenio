# 2026-09-18 — Secretaría integral de Taller y Gran Secretaría

## Alcance

Se completa el flujo funcional de Secretaría para la puesta en marcha del Proyecto Centenario, distinguiendo claramente:

- ámbito privado del Taller;
- validación transversal de Régimen Interior;
- información mínima remitida a Gran Secretaría;
- documentos formales de Gran Secretaría.

## Cuadro del Taller

Secretaría del Taller puede:

- cargar hermanos activos individualmente;
- descargar/importar plantilla XLSX con hojas `Hermanos` y `Cargos`;
- registrar RUT, número institucional, grado actual, fechas conocidas, cargos y evidencia;
- dejar fechas históricas desconocidas sin inventarlas;
- proponer actualizaciones sobre hermanos ya existentes;
- enviar la regularización a Régimen Interior.

Régimen Interior puede aprobar, observar o rechazar. Sólo la aprobación modifica la información oficial del hermano y el Cuadro del Taller.

## Tenidas

- estados: Programada / Realizada / Cancelada;
- compatibilidad de lectura de `closed` como Realizada;
- modalidad Presencial o Virtual;
- presencial exige lugar;
- virtual exige referencia de acceso restringida;
- Tenida no ceremonial puede tener Plancha de trabajo del hermano;
- Plancha de trabajo es opcional y puede cargarse posteriormente;
- Plancha de trabajo admite PDF/DOCX y queda vinculada a su hermano autor;
- iniciación, aumento de salario y exaltación no llevan Plancha de trabajo del hermano.

## Documentación del Taller

Para Tenidas, Reuniones y Consejos:

- Extracto: PDF;
- Acta completa: PDF/DOCX opcional;
- Plancha del hermano: PDF/DOCX opcional sólo en Tenidas no ceremoniales.

Reuniones y Consejos son privados del Taller.

El acta completa y la Plancha del hermano son siempre privadas del Taller.

## Gran Secretaría

Gran Secretaría recibe sólo Tenidas Realizadas que hayan sido remitidas con Extracto PDF.

Su vista contiene únicamente:

- Taller;
- fecha;
- tipo;
- grado;
- modalidad;
- título;
- tipo de ceremonia cuando corresponda;
- estado;
- Extracto PDF;
- estado de recepción/observación.

No accede a:

- enlace virtual;
- Plancha del hermano;
- autor de Plancha;
- acta completa;
- asistencia;
- votaciones;
- Reuniones;
- Consejo;
- documentación privada adicional.

## Planchas de Gran Secretaría

Se separan formalmente de los Decretos.

- `decree`: Decreto.
- `plancha` + `formal_communication`: Plancha / comunicado formal.
- `plancha` + `ceremony_authorization`: Plancha / autorización formal de ceremonia.

La Plancha de Autorización de Ceremonia:

- no constituye Decreto;
- sólo se emite para ceremonia ya autorizada por el flujo institucional;
- puede quedar vinculada a reserva de templo/sala;
- se identifica con código `PLA-AUT-CER`.

Se mantiene compatibilidad de lectura de tipos históricos anteriores.

## Seguridad y datos

- documentos del Taller reutilizan Gestión Documental privada;
- MinIO/S3 + integridad SHA + análisis antimalware;
- RUT protegido y no expuesto en proyecciones innecesarias;
- descarga de Extracto por Gran Secretaría auditada;
- separación de funciones compatible con la política de minimización de Ley 21.719.

## Calidad técnica

- migración EF Core explícitamente registrada;
- migration gate reforzado para impedir migraciones invisibles;
- pruebas de regularización histórica;
- pruebas de privacidad de la proyección de Gran Secretaría;
- frontend, demo y paquete QA actualizados.

## Estado

PR #105. Pendiente de gates finales e integración a `dev`.
