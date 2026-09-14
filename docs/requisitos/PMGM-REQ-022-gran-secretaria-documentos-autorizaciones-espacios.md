# PMGM-REQ-022 — Gran Secretaría: decretos, comunicados, autorizaciones y gestión de espacios

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Módulo:** Gran Secretaría  
**Fuente:** Definición del Product Owner

## 1. Objetivo
Gran Secretaría debe funcionar como el módulo institucional responsable de redactar, emitir, registrar, distribuir y archivar documentos oficiales de la Gran Logia, además de gestionar las autorizaciones formales que dependan de validaciones previas de Régimen Interior y Gran Tesorería.

También debe administrar la disponibilidad y autorización de uso de templos, salas de Secretaría y otros espacios institucionales para reuniones y ceremonias especiales.

El sistema debe evitar correos, planillas o documentos aislados como fuente maestra del trámite. Cada documento y autorización debe quedar vinculado al expediente, solicitud, miembro, Taller y aprobaciones que lo originaron.

## 2. Responsabilidades funcionales de Gran Secretaría
Gran Secretaría deberá poder gestionar, al menos:
- decretos;
- comunicados oficiales;
- circulares u otros documentos institucionales configurables;
- planchas/documentos de autorización para ceremonias;
- autorizaciones de uso de templos y salas;
- registro y numeración correlativa de documentos oficiales;
- plantillas institucionales;
- distribución y notificación a destinatarios autorizados;
- archivo histórico y consulta de documentos emitidos;
- control de versiones y anulaciones/reemplazos;
- trazabilidad de firma, emisión y recepción cuando corresponda.

La nomenclatura documental debe ser parametrizable para adaptarse a las denominaciones oficiales de la Gran Logia.

## 3. Decretos y comunicados
Gran Secretaría podrá crear documentos desde cero o a partir de plantillas institucionales.

Cada documento deberá registrar como mínimo:
- tipo de documento;
- número/correlativo oficial;
- período/año;
- fecha de emisión;
- asunto/título;
- cuerpo del documento;
- autoridad emisora;
- responsable de redacción;
- destinatarios;
- clasificación/confidencialidad;
- estado: borrador, en revisión, aprobado, emitido, anulado, reemplazado u otros configurables;
- documentos anexos;
- versión;
- firmas o validaciones asociadas;
- fecha y medio de distribución;
- evidencia de entrega/lectura cuando la política lo requiera.

## 4. Autorización de ceremonias
Las ceremonias de iniciación, aumento de salario y exaltación deberán generar un flujo institucional coordinado.

### 4.1 Precondiciones
Antes de que Gran Secretaría pueda emitir la autorización formal, la solicitud deberá contar con:
1. validación/aprobación de Régimen Interior;
2. validación/aprobación de Gran Tesorería respecto de la regularidad financiera cuando corresponda;
3. demás aprobaciones institucionales configuradas para ese tipo de ceremonia.

El sistema no debe permitir emitir la autorización definitiva si alguna aprobación obligatoria está pendiente o rechazada, salvo que exista un mecanismo formal de excepción con autoridad, fundamento y auditoría.

### 4.2 Antecedentes visibles para Gran Secretaría
Gran Secretaría deberá visualizar, sin duplicar las fuentes maestras:
- miembro beneficiario;
- Taller solicitante;
- tipo de ceremonia;
- grado actual y grado resultante cuando corresponda;
- fechas relevantes;
- evaluación de Régimen Interior;
- estado de Gran Tesorería;
- observaciones y condiciones;
- fecha propuesta;
- lugar propuesto;
- documentos exigidos;
- historial del trámite.

### 4.3 Generación de plancha/documento de autorización
Una vez cumplidas las condiciones, el sistema debe permitir generar automáticamente la plancha o documento oficial de autorización usando una plantilla institucional.

El documento deberá incorporar, según corresponda:
- correlativo oficial;
- fecha;
- Taller autorizado;
- miembro(s) involucrado(s);
- ceremonia autorizada;
- grado;
- fecha y lugar autorizados;
- referencias a aprobaciones previas;
- condiciones u observaciones;
- autoridad que autoriza;
- firma/sello institucional cuando exista soporte para ello;
- código o referencia verificable del trámite.

La emisión debe congelar una copia de los antecedentes y aprobaciones que sustentaron la autorización para conservar evidencia histórica.

## 5. Integración con Régimen Interior
Gran Secretaría no recalcula elegibilidad masónica.

Régimen Interior es responsable de entregar la evaluación institucional y antecedentes relacionados con:
- estado del miembro;
- pertenencia vigente;
- grado;
- fechas masónicas;
- historial relevante;
- inconsistencias;
- requisitos de ceremonia;
- observaciones o impedimentos.

Gran Secretaría consume esa resolución/validación para confeccionar y emitir el documento formal.

## 6. Integración con Gran Tesorería
Gran Secretaría no mantiene saldos financieros paralelos.

Gran Tesorería deberá entregar un resultado de regularidad aplicable al trámite, por ejemplo:
- aprobado / al día;
- observado;
- no aprobado / moroso;
- exento/no aplica;
- excepción autorizada.

Debe quedar registrada la fecha de corte y la referencia a la validación financiera utilizada para autorizar la ceremonia.

## 7. Gestión y autorización de espacios
Gran Secretaría deberá administrar la disponibilidad institucional de:
- templos;
- salas de Secretaría;
- salas de reuniones;
- otros espacios configurables.

### 7.1 Catálogo de espacios
Cada espacio deberá registrar:
- nombre;
- tipo;
- ubicación;
- capacidad;
- condiciones de uso;
- equipamiento relevante;
- horarios habilitados;
- estado: disponible, mantenimiento, bloqueado, fuera de servicio u otros configurables;
- responsable/administrador.

### 7.2 Solicitud de reserva
Una solicitud deberá incluir:
- Taller o unidad solicitante;
- responsable;
- tipo de actividad;
- fecha;
- hora de inicio y término;
- cantidad estimada de asistentes;
- ceremonia/reunión asociada cuando corresponda;
- requerimientos especiales;
- observaciones.

### 7.3 Control de disponibilidad
El sistema deberá detectar automáticamente:
- solapamientos de horario;
- bloqueos administrativos;
- períodos de mantenimiento;
- capacidad insuficiente cuando aplique;
- restricciones específicas del espacio.

No deberá existir doble reserva confirmada para el mismo espacio y franja horaria.

### 7.4 Autorización
Gran Secretaría podrá aprobar, observar, rechazar, cancelar o reprogramar una reserva.

Toda resolución deberá conservar:
- responsable que decide;
- fecha/hora;
- motivo u observación;
- período autorizado;
- espacio;
- actividad;
- historial de modificaciones.

## 8. Calendario institucional de espacios
El módulo debe disponer de una vista de calendario que permita consultar:
- disponibilidad por día/semana/mes;
- reservas confirmadas;
- solicitudes pendientes;
- bloqueos/mantenimiento;
- ceremonias especiales;
- reuniones institucionales.

Los detalles visibles dependerán de los permisos del usuario y de la confidencialidad del evento.

## 9. Flujo objetivo de ceremonia
Flujo base esperado:

1. Taller genera solicitud de ceremonia.
2. Régimen Interior revisa requisitos y antecedentes.
3. Gran Tesorería valida regularidad financiera cuando corresponda.
4. Otras áreas requeridas emiten su validación.
5. Gran Secretaría verifica que todas las aprobaciones obligatorias estén completas.
6. Se valida o reserva templo/sala si corresponde.
7. Gran Secretaría genera la plancha/documento oficial de autorización.
8. Autoridad competente firma/aprueba la emisión.
9. Documento queda numerado, emitido, notificado y archivado.
10. La ceremonia realizada actualiza el expediente y los hitos históricos correspondientes.

## 10. Numeración y registro oficial
Los documentos oficiales deberán utilizar correlativos controlados.

El sistema debe soportar reglas como:
- secuencia por tipo de documento;
- secuencia por año/período;
- prefijos/sufijos institucionales;
- reserva de número al momento definido por política;
- imposibilidad de reutilizar números anulados;
- registro del motivo de anulación o reemplazo.

La regla exacta debe ser parametrizable.

## 11. Plantillas institucionales
Gran Secretaría debe poder administrar plantillas para:
- decretos;
- comunicados;
- autorizaciones de iniciación;
- autorizaciones de aumento de salario;
- autorizaciones de exaltación;
- autorizaciones de uso de espacios;
- otros documentos oficiales.

Las plantillas deberán admitir campos automáticos provenientes de la base maestra, evitando volver a escribir datos que ya existen.

## 12. Seguridad y permisos
Como mínimo deberán distinguirse permisos para:
- crear borradores;
- editar borradores propios/ajenos;
- revisar;
- aprobar contenido;
- asignar numeración;
- emitir;
- anular/reemplazar;
- reservar espacios;
- aprobar reservas;
- consultar documentos confidenciales;
- descargar/exportar;
- administrar plantillas y correlativos.

Las funciones de emisión y anulación deben estar restringidas a roles autorizados.

## 13. Auditoría
Se deberá auditar como mínimo:
- creación y edición de documentos;
- cambios de estado;
- aprobaciones/rechazos;
- asignación de correlativos;
- emisión;
- firma/validación;
- anulación y reemplazo;
- distribución;
- generación de autorizaciones de ceremonia;
- validaciones consideradas;
- reservas, reprogramaciones y cancelaciones de espacios;
- cambios de plantillas y reglas de numeración.

## 14. Reportes de Gran Secretaría
El módulo deberá permitir reportar al menos:
- documentos emitidos por período y tipo;
- decretos vigentes/anulados/reemplazados;
- comunicados emitidos;
- ceremonias autorizadas, rechazadas y pendientes;
- autorizaciones por Taller;
- tiempo promedio de tramitación;
- solicitudes pendientes por área responsable;
- uso de templos y salas;
- porcentaje de ocupación por espacio;
- cancelaciones y reprogramaciones;
- documentos pendientes de firma/emisión.

## 15. Criterios de aceptación
El requisito se considera implementado cuando un usuario autorizado de Gran Secretaría puede:
1. redactar y versionar decretos y comunicados;
2. asignar correlativos oficiales bajo reglas institucionales;
3. emitir documentos y conservar su historial;
4. visualizar las aprobaciones de Régimen Interior y Gran Tesorería para una ceremonia;
5. impedir la emisión definitiva cuando falte una aprobación obligatoria;
6. generar automáticamente una plancha/documento de autorización desde plantilla;
7. vincular la autorización al miembro, Taller, ceremonia y aprobaciones que la sustentan;
8. consultar disponibilidad de templos y salas;
9. impedir doble reserva confirmada;
10. aprobar, rechazar, cancelar o reprogramar reservas;
11. visualizar un calendario institucional de ocupación;
12. mantener auditoría verificable de documentos, autorizaciones y reservas.