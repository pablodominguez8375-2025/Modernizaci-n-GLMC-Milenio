# PMGM-REQ-030 — Gran Archivero y Archivo Histórico Institucional

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1 institucional  
**Áreas:** Gran Archivo / Gran Secretaría / Biblioteca Virtual / Gestión Documental / Régimen Interior  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-ADR-003

## 1. Objetivo
Incorporar al Proyecto Milenio la gestión institucional del **Gran Archivero**, responsable de custodiar, organizar, describir, preservar y facilitar el acceso controlado al patrimonio documental histórico de la Gran Logia Mixta de Chile.

El proyecto **no incorpora CENDOC**. El dominio de conocimiento institucional se separa en dos módulos distintos:
- **Biblioteca Virtual:** consulta y publicación de libros, trabajos, revistas, material formativo y documentos de lectura autorizados.
- **Gran Archivo / Gran Archivero:** custodia, clasificación, conservación y acceso controlado al archivo histórico institucional.

## 2. Rol institucional del Gran Archivero
El rol `GRAN_ARCHIVERO` deberá disponer de permisos específicos, sujetos a mínimo privilegio y clasificación documental, para:
- recibir transferencias documentales desde Gran Secretaría, Talleres u otras áreas autorizadas;
- registrar ingresos y procedencia;
- clasificar fondos, secciones, series, expedientes y piezas documentales;
- describir documentos y colecciones históricas;
- gestionar metadatos archivísticos;
- gestionar estados de conservación física y digital;
- gestionar digitalización y vincular reproducciones con originales;
- administrar restricciones de acceso y fechas de apertura;
- gestionar consultas internas y préstamos autorizados;
- preservar cadena de custodia;
- generar inventarios, catálogos, índices y reportes históricos;
- preparar copias de consulta o publicaciones históricas para Biblioteca Virtual sin alterar el registro archivístico original.

El rol no otorga acceso irrestricto a todo dato personal o sensible. El acceso final dependerá de clasificación, finalidad, reglas de privacidad y permisos institucionales.

## 3. Separación documento operativo / documento histórico
Estados mínimos:
1. `draft` — borrador.
2. `active` — vigente/operativo.
3. `closed` — cerrado administrativamente.
4. `transfer_pending` — pendiente de transferencia al Gran Archivo.
5. `archived` — recibido formalmente por Gran Archivo.
6. `restricted` — archivado con acceso restringido.
7. `historical_publication` — representación autorizada para consulta histórica.
8. `disposed` — eliminación autorizada, conservando acta y evidencia.

Gran Secretaría y las áreas productoras son responsables de los documentos activos. El Gran Archivero asume custodia histórica cuando se completa la transferencia formal.

## 4. Transferencia documental
Toda transferencia deberá registrar:
- identificador único;
- área o Taller productor;
- responsable de entrega y recepción;
- fecha/hora;
- rango temporal;
- cantidad de expedientes/piezas;
- soporte;
- clasificación de seguridad y privacidad;
- inventario;
- estado de conservación;
- observaciones;
- regla de conservación aplicable;
- hash/checksum para archivos digitales cuando corresponda;
- acta y aprobación de transferencia.

Cuando el documento ya exista en Gestor Documental, la transferencia podrá cambiar custodia lógica y agregar metadata archivística sin duplicar innecesariamente el binario.

## 5. Estructura archivística
El sistema deberá soportar como mínimo:
- Fondo;
- Sección;
- Serie;
- Subserie opcional;
- Expediente;
- Unidad documental o pieza.

Cada nivel podrá mantener código, título, descripción, fechas extremas, productor, alcance/contenido, volumen, soporte, restricciones, notas y relaciones.

El cuadro de clasificación deberá ser configurable sin modificar código.

## 6. Procedencia y contexto
Cada unidad deberá conservar:
- productor original;
- Taller u órgano de origen;
- autoridad o cargo relacionado cuando corresponda;
- fecha de creación;
- fecha de ingreso al archivo;
- relaciones con decretos, actas, ceremonias, tenidas, personas, Talleres, períodos y autoridades;
- historial de custodia;
- historial de clasificación y reclasificación.

Las reclasificaciones no borrarán la clasificación anterior.

## 7. Tipos documentales históricos
El Gran Archivo podrá gestionar, entre otros:
- decretos, circulares y comunicados históricos;
- actas de Gran Logia;
- actas y documentos históricos transferidos por Talleres;
- correspondencia institucional;
- constituciones, reglamentos y estatutos históricos;
- patentes y cartas constitutivas;
- documentos de fundación, fusión, suspensión o cierre de Talleres;
- registros históricos de autoridades y cargos;
- fotografías;
- afiches, invitaciones y programas;
- publicaciones y boletines;
- documentación de congresos, conventos y asambleas;
- material audiovisual;
- planos, diseños, sellos, medallas u objetos con representación documental digital;
- colecciones históricas entregadas institucionalmente;
- documentación de aniversarios, ceremonias e hitos relevantes.

## 8. Digitalización
Por cada reproducción digital se podrá registrar:
- original asociado;
- fecha de digitalización;
- operador;
- equipo/proceso relevante;
- resolución/formato;
- SHA-256;
- control de calidad;
- estado de revisión;
- archivo maestro de preservación;
- derivado de consulta;
- observaciones de legibilidad o daño.

El archivo maestro de preservación no se sobrescribirá.

## 9. Preservación digital
El sistema deberá soportar:
- verificación periódica de integridad;
- detección de archivos faltantes o corruptos;
- respaldos según política;
- formatos de preservación definidos institucionalmente;
- migraciones de formato documentadas;
- registro de acciones de preservación;
- alertas de riesgo de obsolescencia o deterioro;
- estado de conservación de originales físicos.

Se utilizará el Object Storage definido en PMGM-ADR-003 y PMGM-REQ-027.

## 10. Restricciones de acceso
Políticas mínimas:
- público histórico;
- miembros autenticados;
- miembros según condición institucional;
- autoridades de Taller;
- Grandes Oficiales autorizados;
- Gran Archivero;
- restringido por datos personales/sensibles;
- cerrado hasta fecha determinada;
- acceso sólo mediante solicitud aprobada.

La clasificación archivística no podrá reducir automáticamente una restricción de privacidad existente.

## 11. Consultas y solicitudes
El módulo deberá permitir:
- búsqueda por fondo, serie, fecha, productor, Taller, tipo y palabras clave;
- solicitud de acceso a material restringido;
- aprobación/rechazo fundado;
- auditoría de consultas sensibles;
- entrega de copia de consulta cuando corresponda;
- registro de préstamos físicos y devolución.

## 12. Préstamos físicos y cadena de custodia
Registrar:
- unidad;
- ubicación habitual;
- solicitante;
- finalidad;
- autorizador;
- salida;
- ubicación temporal;
- devolución prevista y real;
- estado al salir y retornar;
- incidentes/daños;
- evidencia de entrega/recepción.

## 13. Inventario y ubicación física
Estructura configurable, por ejemplo:
- sede;
- depósito;
- sala;
- estantería;
- módulo;
- caja;
- carpeta;
- unidad.

Las ubicaciones internas no se publicarán cuando impliquen riesgo de seguridad patrimonial.

## 14. Integración con Gran Secretaría
Gran Secretaría podrá iniciar transferencias desde expedientes cerrados. El Gran Archivero podrá aceptar, rechazar con observaciones, pedir antecedentes, completar descripción, asignar clasificación y ubicación definitiva.

Una transferencia aceptada conservará vínculos con expediente y origen, pasando su custodia histórica al Gran Archivo.

## 15. Integración con Talleres
Los Talleres podrán proponer transferencias históricas conforme a política institucional. Se conservarán productor, fechas, autoridades, contexto, inventario y restricciones.

La documentación histórica no modificará ni reemplazará la historia funcional del Taller en otros módulos.

## 16. Integración con Biblioteca Virtual
Biblioteca Virtual y Gran Archivo son módulos separados.

El Gran Archivo podrá autorizar una copia o representación histórica para publicación en Biblioteca Virtual. Esa publicación deberá mantener referencia al registro archivístico de origen y podrá usar:
- imagen derivada;
- transcripción;
- ficha descriptiva;
- copia optimizada para consulta.

El original y el archivo maestro permanecerán bajo custodia archivística.

## 17. Reportes del Gran Archivero
Reportes mínimos:
- inventario total por fondo/serie;
- ingresos y transferencias por período;
- documentos pendientes de clasificar;
- documentos restringidos y fechas de apertura;
- préstamos pendientes o vencidos;
- estado de digitalización;
- estado de conservación;
- verificaciones de integridad;
- documentos sin metadata obligatoria;
- crecimiento del archivo por soporte y período.

## 18. Auditoría
Auditar al menos:
- recepción de transferencias;
- cambios de clasificación;
- cambios de restricción;
- consultas de documentos sensibles;
- préstamos y devoluciones;
- descargas de documentos restringidos;
- digitalizaciones y nuevas versiones;
- acciones de preservación;
- eliminación o disposición documental.

## 19. Privacidad y Ley 21.719
El valor histórico de un documento no autoriza automáticamente conservación identificable ilimitada ni acceso general.

Se aplicará PMGM-REQ-026 respecto de finalidad, base de licitud, minimización, conservación, restricciones, anonimización/seudonimización y ejercicio de derechos cuando corresponda.

## 20. Criterios de aceptación
1. Existe rol `GRAN_ARCHIVERO` con capacidades propias y auditables.
2. Se puede transferir formalmente un expediente cerrado al Gran Archivo.
3. Se conserva procedencia, versiones y cadena de custodia.
4. El archivo soporta Fondo → Sección → Serie → Expediente → Pieza.
5. Las restricciones de acceso se aplican en backend.
6. Se pueden registrar documentos físicos y digitales.
7. Los archivos digitales mantienen hash e integridad verificable.
8. Se pueden registrar digitalizaciones sin sobrescribir maestros.
9. Se pueden registrar préstamos físicos y devoluciones.
10. Biblioteca Virtual sólo recibe copias/representaciones autorizadas; el original permanece en Gran Archivo.
11. CENDOC no aparece como módulo, dependencia ni alcance del Proyecto Milenio.
12. Todas las operaciones sensibles quedan auditadas.
