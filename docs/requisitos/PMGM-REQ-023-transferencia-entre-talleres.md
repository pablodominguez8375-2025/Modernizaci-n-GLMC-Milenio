# PMGM-REQ-023 — Transferencia de miembros entre Talleres

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Módulo:** Miembros / Régimen Interior / Gestión Logial  
**Fuente:** Definición del Product Owner

## 1. Objetivo
Permitir que un hermano cambie de Taller conservando una identidad institucional única y toda su historia masónica, sin borrar ni trasladar destructivamente los registros que pertenecen al Taller de origen.

La transferencia debe cerrar una etapa de pertenencia y abrir una nueva etapa en el Taller receptor.

## 2. Principio de identidad única
El hermano conserva el mismo `MemberId` institucional durante toda su trayectoria dentro de la Orden.

No se crea una nueva Persona ni un nuevo Miembro por cambiar de Taller.

## 3. Información institucional que acompaña al hermano
Debe continuar disponible como parte de su expediente institucional, según permisos:
- identidad y datos maestros autorizados;
- número institucional cuando corresponda;
- estado institucional e historial de estados;
- iniciación;
- aumento de salario;
- exaltación;
- grado e historial de grados;
- retiros y reintegros;
- reconocimientos y distinciones;
- antecedentes generales de Régimen Interior;
- documentos institucionales de alcance general;
- otras referencias que la Gran Logia determine como parte del expediente institucional.

## 4. Información que permanece en el Taller de origen
Los antecedentes generados en el Taller de origen permanecen vinculados a ese Taller y a su período histórico, incluyendo según corresponda:
- pertenencia y fechas de ingreso/término;
- cargos locales y períodos;
- asistencia y excusas;
- Tenidas en las que participó;
- actas, planchas y documentos del Taller sujetos a permisos;
- movimientos o antecedentes administrativos propios del período;
- observaciones y evidencias históricas autorizadas.

El Taller de origen no pierde su historia cuando el hermano se transfiere.

## 5. Nueva información en el Taller receptor
Al hacerse efectiva la transferencia se crea una nueva pertenencia para el mismo Miembro:
- Taller receptor;
- fecha efectiva de incorporación;
- tipo de pertenencia;
- estado;
- documento o acto que autoriza la transferencia;
- observaciones cuando correspondan.

Desde ese momento, los nuevos registros de actividad local se escriben asociados al Taller receptor.

## 6. Entidad Transferencia de Miembro
La transferencia debe registrar como mínimo:
- UUID de transferencia;
- Miembro;
- pertenencia de origen;
- Taller de origen;
- Taller receptor;
- fecha de solicitud;
- fecha efectiva propuesta;
- fecha efectiva aprobada;
- estado: solicitada, en revisión, aprobada, rechazada, cancelada, ejecutada u otro configurable;
- motivo;
- autoridad/responsable solicitante;
- validaciones requeridas;
- resolución;
- documento de autorización;
- usuario que ejecuta el cambio;
- fecha/hora de ejecución.

## 7. Ejecución de la transferencia
Una transferencia aprobada debe ejecutarse como una única operación transaccional:
1. cerrar la pertenencia vigente del Taller de origen con su fecha y motivo;
2. conservar intactos todos sus registros históricos;
3. crear la nueva pertenencia en el Taller receptor;
4. registrar el hito institucional correspondiente;
5. conservar referencia entre ambas pertenencias y la transferencia;
6. auditar actor, fecha, resolución y datos afectados.

Si una parte falla, no debe quedar una transferencia parcialmente aplicada.

## 8. Acceso a historia anterior
El nuevo Taller podrá visualizar antecedentes históricos anteriores solo conforme a roles, permisos, clasificación documental y reglas de privacidad institucional.

La transferencia de pertenencia no implica acceso automático e irrestricto a todos los documentos internos del Taller de origen.

Régimen Interior de Gran Logia podrá disponer de visión transversal cuando su rol lo autorice.

## 9. Reportes
Debe ser posible consultar:
- miembros transferidos por período;
- transferencias desde/hacia cada Taller;
- historial completo de Talleres de un hermano;
- fecha de cada cambio de pertenencia;
- transferencias pendientes, aprobadas, rechazadas y ejecutadas;
- discrepancias entre pertenencia actual y transferencias registradas.

## 10. Reglas de consistencia
- Un cambio de Taller nunca crea un Miembro duplicado.
- Una pertenencia histórica no se sobrescribe.
- La pertenencia de origen debe conservar fecha de término y motivo.
- La nueva pertenencia debe comenzar en la fecha efectiva autorizada.
- El sistema debe impedir períodos de pertenencia incompatibles cuando la normativa institucional no los permita.
- Si la afiliación simultánea a más de un Taller está permitida en determinados casos, debe modelarse mediante tipos de pertenencia y reglas parametrizables, no mediante excepciones manuales ocultas.
- Los hitos de grado no deben copiarse como nuevos hitos al Taller receptor; siguen formando parte del historial único del hermano.

## 11. Criterios de aceptación
El requisito se considera implementado cuando:
1. un hermano puede cambiar de Taller sin crear una segunda ficha de miembro;
2. el Taller de origen conserva su historia completa;
3. el Taller receptor obtiene una nueva pertenencia del mismo hermano;
4. la historia institucional y de grados sigue siendo única y continua;
5. la información local nueva queda asociada al Taller receptor;
6. el traslado queda respaldado por resolución/documento y auditoría;
7. puede reconstruirse cronológicamente en qué Taller estuvo el hermano en cada período;
8. los permisos evitan revelar automáticamente documentos internos del Taller anterior.