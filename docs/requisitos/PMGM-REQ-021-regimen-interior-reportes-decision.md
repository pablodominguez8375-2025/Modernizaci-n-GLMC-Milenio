# PMGM-REQ-021 — Régimen Interior: reportes, control histórico y apoyo a decisiones

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Módulo:** Régimen Interior  
**Fuente:** Definición del Product Owner

## 1. Objetivo
Régimen Interior debe disponer de una visión institucional consolidada, histórica y verificable de los miembros de la Orden, tanto a nivel global como por cada Taller, con información suficiente para control administrativo, regularidad, análisis y toma de decisiones.

El módulo no debe depender de planillas paralelas ni duplicar datos maestros provenientes de Miembros, Talleres o Tesorería.

## 2. Ámbitos de consulta
Los reportes deben permitir como mínimo consultar:

### 2.1 Estado de miembros
- Total de miembros de la Orden.
- Miembros activos.
- Miembros no activos / inactivos.
- Retiros voluntarios.
- Retiros forzosos.
- Defunciones / paso a Oriente Eterno.
- Reintegros.
- Hermanos Past activos, como categoría institucional configurable según nomenclatura oficial.
- Otros estados institucionales que la autoridad defina en el futuro.

Toda cifra debe poder desglosarse:
- por toda la Orden;
- por Taller;
- por período;
- por grado;
- por estado institucional;
- y por combinaciones de estos filtros cuando corresponda.

## 3. Morosidad y regularidad administrativa
Régimen Interior debe poder visualizar el estado de morosidad de los miembros:
- total de la Orden;
- por Taller;
- por miembro;
- por período;
- con indicadores agregados y detalle autorizado.

### Regla de arquitectura
La fuente maestra de morosidad será Tesorería. Régimen Interior consume el estado calculado y sus antecedentes autorizados, pero no mantiene un saldo financiero paralelo.

Debe distinguirse, al menos:
- al día;
- moroso;
- situación pendiente de regularización;
- no aplica / exento cuando exista esa condición institucional.

La definición exacta de morosidad y sus plazos debe ser parametrizable y no codificarse como una regla fija irreversible.

## 4. Fechas e historial masónico relevante
Régimen Interior debe poder consultar y corroborar las fechas oficiales de cada miembro, incluyendo como mínimo:
- iniciación;
- aumento de salario;
- exaltación;
- retiro;
- tipo de retiro;
- reintegro;
- defunción / Oriente Eterno;
- ingreso o afiliación a un Taller;
- cambios de Taller cuando correspondan;
- cargos y períodos;
- reconocimientos y distinciones;
- ceremonias solicitadas, autorizadas, rechazadas o realizadas.

### Regla de integridad histórica
Una fecha crítica no se sobrescribe sin conservar el valor anterior, el motivo del cambio, quién lo realizó, cuándo se realizó y la evidencia asociada cuando exista.

## 5. Control de inconsistencias
El sistema debe detectar y señalar situaciones que requieran revisión, por ejemplo:
- exaltación anterior al aumento de salario;
- aumento de salario anterior a iniciación;
- reintegro sin retiro previo registrado;
- miembro activo con fecha de defunción;
- miembro retirado figurando como activo sin reintegro;
- pertenencia a Taller sin fechas coherentes;
- datos críticos incompletos;
- duplicidad de hitos incompatibles;
- discrepancias entre ficha maestra y antecedentes documentales.

Estas alertas no deben alterar automáticamente el dato: deben generar una observación para revisión por personal autorizado.

## 6. Reportes mínimos
El módulo debe incorporar al menos los siguientes reportes:

1. Resumen general de miembros de la Orden.
2. Estado de miembros por Taller.
3. Activos e inactivos.
4. Retiros voluntarios y forzosos.
5. Reintegros.
6. Defunciones / Oriente Eterno.
7. Morosidad general de la Orden.
8. Morosidad por Taller.
9. Historial de grados y fechas masónicas.
10. Historial de pertenencias y cambios de Taller.
11. Historial de cargos y períodos.
12. Hermanos Past activos / ex autoridades según clasificación institucional.
13. Miembros con información incompleta o inconsistente.
14. Elegibilidad para ceremonias.
15. Elegibilidad o antecedentes para reconocimientos.
16. Reporte histórico por rango de fechas.

## 7. Filtros y explotación
Los reportes deben permitir filtros por:
- Taller;
- período o rango de fechas;
- estado institucional;
- grado;
- tipo de retiro;
- estado de morosidad;
- cargo;
- condición Past / ex autoridad;
- ceremonia;
- reconocimiento;
- campos de control definidos por Régimen Interior.

Cuando el permiso del usuario lo permita, los resultados deberán poder exportarse a formatos de trabajo institucional tales como PDF, XLSX o CSV.

## 8. Apoyo a decisiones: ceremonias y reconocimientos
Para solicitudes de ceremonia, reconocimientos u otros actos que requieran validación institucional, el sistema debe presentar una vista de antecedentes consolidada.

### 8.1 Semáforo de elegibilidad
El sistema podrá clasificar preliminarmente una solicitud como:
- **Cumple:** no se detectan impedimentos según reglas vigentes.
- **Observado:** existe información faltante, inconsistente o una condición que requiere revisión.
- **No cumple:** existe una regla institucional objetiva incumplida.

La clasificación debe mostrar las razones, por ejemplo:
- grado requerido;
- antigüedad mínima;
- fechas masónicas;
- estado activo;
- pertenencia vigente;
- regularidad administrativa/morosidad cuando corresponda;
- documentación requerida;
- autorizaciones previas;
- antecedentes institucionales pertinentes.

### 8.2 Decisión humana obligatoria
El semáforo es apoyo a la decisión y no reemplaza a la autoridad competente. La autorización, rechazo o excepción debe registrar:
- autoridad que decide;
- fecha y hora;
- resolución;
- fundamento u observación;
- reglas evaluadas;
- antecedentes utilizados;
- documentos asociados;
- historial de cambios.

## 9. Tablero de Régimen Interior
Además de reportes tabulares, el módulo deberá presentar indicadores ejecutivos, como mínimo:
- total de miembros;
- activos e inactivos;
- retiros del período;
- reintegros del período;
- defunciones del período;
- distribución por grados;
- morosidad global y por Taller;
- solicitudes de ceremonias pendientes;
- solicitudes observadas;
- inconsistencias de datos pendientes de corregir.

Los indicadores deben permitir navegar desde el resumen al detalle autorizado.

## 10. Seguridad y privacidad
- Acceso mediante RBAC y alcance institucional.
- Un Taller sólo ve los datos que su rol y alcance permiten.
- Régimen Interior de nivel Gran Logia puede tener visión transversal cuando el rol lo autorice.
- Datos personales y financieros deben mostrarse bajo criterio de mínimo privilegio.
- Toda exportación sensible debe quedar auditada.
- Las consultas de expedientes sensibles deben ser trazables según política institucional.

## 11. Auditoría
Se deben registrar como mínimo:
- altas y cambios de estado;
- correcciones de fechas críticas;
- retiros y reintegros;
- modificaciones de grado;
- cambios de pertenencia;
- autorizaciones y rechazos;
- excepciones a reglas;
- exportaciones de reportes sensibles;
- correcciones derivadas de inconsistencias.

## 12. Criterios de aceptación
El requisito se considera implementado cuando un usuario autorizado de Régimen Interior puede:
1. obtener el estado completo de miembros de la Orden y de cada Taller;
2. separar activos, inactivos, retiros voluntarios, retiros forzosos, reintegros y defunciones;
3. consultar morosidad consolidada desde Tesorería sin duplicar saldos;
4. verificar la historia de iniciación, aumento de salario, exaltación, retiro y reintegro;
5. detectar inconsistencias de fechas y estados;
6. analizar antecedentes de un miembro antes de una ceremonia o reconocimiento;
7. obtener una evaluación explicable de elegibilidad sin reemplazar la decisión humana;
8. filtrar y exportar los reportes autorizados;
9. navegar de indicadores agregados al detalle;
10. dejar evidencia auditable de decisiones y cambios relevantes.
