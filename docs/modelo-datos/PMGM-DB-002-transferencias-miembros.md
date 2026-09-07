# PMGM-DB-002 — Modelo de transferencia de miembros entre Talleres

**Estado:** Aprobado funcionalmente  
**Versión:** 0.1

## Principio
Una transferencia entre Talleres no mueve ni duplica la ficha del hermano. El `MemberId` permanece invariable y representa al mismo miembro durante toda su trayectoria institucional.

El modelo distingue entre:
- datos institucionales globales del hermano;
- relaciones históricas de pertenencia a Talleres;
- datos locales generados dentro de cada Taller.

## Entidad `MemberTransfer`
Atributos conceptuales:
- `Id` UUID;
- `MemberId`;
- `SourceMembershipId`;
- `SourceOrganizationId`;
- `TargetOrganizationId`;
- fecha de solicitud;
- fecha efectiva propuesta;
- fecha efectiva aprobada;
- estado;
- motivo;
- resolución;
- documento/evidencia de autorización;
- usuario/autoridad responsable;
- fecha/hora de ejecución;
- `TargetMembershipId` una vez ejecutada.

## Ejecución transaccional
Al ejecutar una transferencia aprobada:
1. se cierra la pertenencia de origen con fecha y motivo;
2. no se modifica ni elimina ningún registro histórico del Taller de origen;
3. se crea una nueva pertenencia para el mismo `MemberId` en el Taller receptor;
4. se vinculan la pertenencia anterior y la nueva mediante la transferencia;
5. se registra auditoría completa;
6. la operación completa debe confirmarse o revertirse de forma atómica.

## Datos institucionales globales
Permanecen vinculados al mismo miembro y no se copian como nuevos registros por el traslado:
- Persona;
- identidad institucional;
- historial de estados;
- iniciación;
- aumento de salario;
- exaltación;
- grados;
- reconocimientos y distinciones;
- retiros/reintegros institucionales cuando corresponda;
- documentos de alcance Gran Logia autorizados.

## Datos locales por Taller
Permanecen vinculados al Taller donde ocurrieron:
- pertenencias;
- asistencias;
- Tenidas;
- cargos locales;
- documentos internos;
- actas y planchas;
- observaciones locales;
- movimientos administrativos propios del Taller.

Los nuevos registros locales posteriores a la transferencia se asocian al Taller receptor.

## Seguridad
El Taller receptor no obtiene acceso irrestricto a los documentos internos históricos del Taller de origen. El acceso depende de clasificación documental, rol, propósito y alcance institucional.

Régimen Interior de Gran Logia puede consultar la trayectoria transversal cuando los permisos lo autoricen.

## Integridad
- no crear un nuevo Miembro por transferencia;
- no sobrescribir pertenencias anteriores;
- no duplicar hitos de grado al cambiar de Taller;
- conservar fechas de inicio/término de cada pertenencia;
- detectar solapamientos incompatibles;
- permitir afiliaciones simultáneas únicamente si la normativa las admite y el tipo de pertenencia lo permite;
- una transferencia ejecutada debe referenciar la pertenencia de origen y la nueva pertenencia de destino.

## Consultas derivadas
El modelo debe permitir responder:
- ¿en qué Taller estaba un hermano en una fecha determinada?;
- ¿cuántas veces cambió de Taller?;
- ¿desde qué Taller llegó al actual?;
- ¿qué cargos/asistencias/actividad tuvo en cada Taller?;
- ¿qué transferencias están pendientes o fueron rechazadas?;
- ¿qué miembros ingresaron o salieron de un Taller en un período?.