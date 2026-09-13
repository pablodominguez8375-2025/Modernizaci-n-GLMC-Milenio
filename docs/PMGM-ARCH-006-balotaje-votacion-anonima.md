# PMGM ARCH 006 Balotaje votación y sufragio anónimo

## 1 Decisión institucional

En Proyecto Centenario, todo balotaje, votación o sufragio es anónimo. El sistema puede identificar a las personas asistentes y determinar cuáles estaban habilitadas para participar, pero no puede registrar, inferir ni reconstruir cómo votó una persona determinada.

La asistencia y el resultado son registros separados. El padrón o lista de asistencia acredita presencia y habilitación. El resultado conserva únicamente cantidades agregadas y la evidencia institucional del acto.

## 2 Terminología

- **Balotaje:** votación mediante balotas blancas y negras. Una balota blanca representa aprobación y una balota negra representa rechazo.
- **Votación o sufragio en Asamblea:** puede registrar votos positivos y negativos sobre una proposición, o votos positivos asociados a candidatos determinados, según el asunto sometido a decisión.
- **Asistencia:** nómina de personas presentes en una Tenida, Asamblea o actividad institucional. No contiene la preferencia de voto.
- **Habilitación:** condición calculada que determina quién podía participar en una votación específica. No revela si la persona efectivamente emitió un voto ni su contenido.

## 3 Datos permitidos

Cada acto de votación debe conservar como mínimo:

- identificador y tipo del acto institucional;
- Tenida, Asamblea o instancia a la que pertenece;
- fecha y hora;
- asunto, candidato o decisión sometida a votación;
- número total de asistentes;
- número total de habilitados para votar;
- número total de balotas o votos contabilizados;
- resultado agregado;
- referencia del acta y responsables que certifican el escrutinio;
- observaciones sobre diferencias de recuento, si existieran.

Para balotaje, el resultado agregado contiene `balotas_blancas` y `balotas_negras`. Para una votación positiva o negativa contiene `votos_positivos` y `votos_negativos`. Para elección de candidatos contiene el total agregado obtenido por cada candidatura, sin identidad de los votantes.

## 4 Datos prohibidos

El sistema no debe crear ni conservar:

- relación entre una persona y su balota o voto;
- selección individual del votante;
- secuencia que permita correlacionar identidad, hora y preferencia;
- auditoría, historial técnico o notificación que revele el sentido del voto individual;
- reportes que permitan deducir una preferencia personal mediante cruces de datos.

La auditoría registra apertura, cierre, responsables, cantidades y correcciones del escrutinio, pero nunca el voto individual.

## 5 Reglas de consistencia

1. El número de personas habilitadas no puede superar el número de asistentes registrados.
2. El total contabilizado debe corresponder a la suma de las categorías agregadas aplicables.
3. El sistema compara asistentes, habilitados y votos o balotas contabilizados.
4. Una diferencia de recuento no se corrige asignando votos a personas. Debe quedar observada y explicada en el acta.
5. Toda corrección del resultado genera una nueva versión trazable del escrutinio agregado; la versión anterior se conserva.
6. El cierre congela el resultado y el padrón de habilitación como evidencias separadas e inmutables.

## 6 Aplicación por proceso

### Insinuaciones e Iniciación

El balotaje de primer grado registra asistentes, habilitados y recuento de balotas blancas y negras. La regla institucional determina si el resultado es aprobatorio. El expediente de la persona insinuada recibe solamente la decisión final y la referencia del acta, no información sobre votantes individuales.

### Tenidas

Cada Tenida conserva su asistencia. Cuando exista balotaje o votación, se agrega un acto de escrutinio independiente asociado al acta.

### Asamblea General

El padrón inmutable determina quién podía asistir y sufragar. La asistencia confirma quién estuvo presente. El resultado electoral conserva totales positivos y negativos o totales por candidatura, según el tipo de elección, sin vincularlos a integrantes del padrón.

## 7 Criterios de aceptación

- Es posible auditar cuántas personas asistieron y cuántas estaban habilitadas.
- Es posible verificar el recuento agregado y su coherencia matemática.
- No existe consulta, tabla, evento de auditoría ni exportación capaz de responder cómo votó una persona.
- El Extracto de Acta se completa con asistencia y resultados agregados desde fuentes separadas.
- Los permisos de una autoridad permiten certificar o consultar resultados conforme a su ámbito, pero nunca acceder a votos individuales porque esos datos no existen.
