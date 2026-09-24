# PMGM-REQ-025 — Elegibilidad de ceremonias y publicación de insinuados

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Módulos:** Régimen Interior / Gran Tesorería / Gran Hospitalaria / Gran Secretaría / Portal de Insinuados  
**Fuente:** Definición del Product Owner

## 1. Objetivo
Definir las condiciones obligatorias y configurables que deben cumplirse antes de autorizar ceremonias de iniciación, aumento de salario y exaltación.

La autorización definitiva no depende de una sola área. El sistema deberá consolidar validaciones provenientes de las áreas responsables y entregar a Gran Secretaría una evaluación trazable y explicable.

## 2. Condiciones obligatorias del Taller
Para autorizar cualquier ceremonia, el Taller solicitante deberá cumplir al menos con:

1. **Régimen Interior:** antecedentes institucionales y masónicos conformes para la ceremonia solicitada.
2. **Gran Tesorería:** Taller al día con sus obligaciones y pagos exigibles a la Gran Logia, o con excepción formal vigente.
3. **Gran Hospitalaria:** Taller al día con las reposiciones u obligaciones hospitalarias exigibles, o con excepción formal vigente.
4. **Otros requisitos configurables:** la Gran Logia podrá incorporar nuevas validaciones sin modificar el flujo base.

La evaluación deberá registrar la fecha de corte de cada validación. Una autorización no podrá depender silenciosamente de un estado antiguo o sin fecha.

## 3. Regla especial para iniciaciones
Las solicitudes de iniciación deberán incluir además una publicación previa del insinuado en un portal institucional.

### 3.1 Plazo mínimo
- valor inicial recomendado/configurado: **20 días**;
- el plazo real deberá provenir de una configuración institucional vigente;
- el sistema no deberá codificar permanentemente 20 días como constante de negocio;
- la regla podrá cambiar por período o resolución institucional conservando histórico de vigencia.

### 3.2 Cómputo
El sistema deberá registrar:
- fecha/hora de inicio de publicación;
- fecha/hora de término, retiro o suspensión;
- días computables;
- regla/plazo mínimo aplicado;
- estado de cumplimiento;
- interrupciones o anulaciones si corresponden.

Una iniciación no podrá quedar como `Cumple` si la publicación válida no alcanza el plazo mínimo exigido, salvo excepción formal autorizada y auditada.

## 4. Portal institucional de insinuados
Debe existir una **vista general institucional de insinuados publicados para todos los hermanos autenticados de la Orden**. Esta vista no corresponde a una publicación abierta en Internet y deberá requerir autenticación institucional.

La lista general deberá mostrar, para cada insinuado vigente:
- **fotografía** destinada expresamente a la publicación institucional;
- **nombres y apellidos completos**;
- **Taller que presenta al insinuado**.

La interfaz podrá mostrar además metadatos operativos no sensibles del proceso de publicación, tales como estado de publicación, días transcurridos o fecha de cumplimiento, cuando sean necesarios para comprender el período vigente.

La vista general deberá:
- incluir insinuados publicados por cualquier Taller de la Orden, sin limitar la lista al Taller de pertenencia del hermano que consulta;
- permitir búsqueda por nombre/apellidos o Taller;
- mostrar únicamente publicaciones actualmente vigentes;
- conservar el histórico de publicación aunque el registro deje de estar visible en la lista vigente;
- registrar observaciones/antecedentes sólo por canales y roles autorizados si la Gran Logia habilita esta función.

La ficha administrativa completa del insinuado seguirá siendo una vista separada y restringida a los cargos con atribuciones para gestionar el proceso.

## 5. Privacidad y minimización
La vista general para los hermanos no deberá publicar automáticamente todos los datos almacenados de la persona.

Como decisión funcional vigente, los **datos personales visibles en la lista general** serán exclusivamente:
- fotografía de publicación;
- nombres y apellidos completos;
- Taller que presenta al insinuado.

No se mostrarán en esa lista general RUT, fecha de nacimiento, domicilio, teléfonos, correo privado, profesión u oficio, estado civil, patrocinantes/presentantes, documentos, entrevistas, observaciones internas ni otros antecedentes del expediente.

La fotografía utilizada en la lista deberá formar parte del expediente autorizado para publicación y quedar vinculada de manera trazable a la publicación vigente. El sistema no deberá exponer directamente rutas internas de almacenamiento, credenciales, URLs permanentes de objetos privados ni metadatos técnicos del archivo.

Los cambios de contenido visible y las fechas de publicación deberán quedar auditados. La habilitación institucional de estos campos deberá mantenerse documentada dentro de la política de tratamiento correspondiente conforme al diseño de privacidad de PMGM y a la Ley 21.719.

El insinuado deberá existir como `Person` o entidad de candidato vinculada a `Person`, pero **no como `Member` activo antes de su iniciación**.

## 6. Evaluación consolidada de ceremonia
Cada solicitud deberá presentar una matriz de validaciones como mínimo:

| Validación | Responsable | Resultado esperado |
|---|---|---|
| Antecedentes masónicos/institucionales | Régimen Interior | aprobado/cumple |
| Obligaciones de Gran Logia | Gran Tesorería | al día/exento/excepción |
| Reposiciones hospitalarias | Gran Hospitalaria | al día/exento/excepción |
| Publicación de insinuado | Portal/Régimen Interior | cumple plazo, sólo iniciación |
| Disponibilidad del espacio | Gran Secretaría | disponible/reservado cuando aplique |

Estados normalizados sugeridos:
- `pending`;
- `approved`;
- `observed`;
- `rejected`;
- `not_applicable`;
- `exception_approved`.

## 7. Regla de autorización
Gran Secretaría sólo podrá generar la autorización definitiva cuando todas las validaciones obligatorias aplicables se encuentren en un estado habilitante.

Para iniciación se requiere adicionalmente:
- publicación válida;
- plazo mínimo cumplido;
- ausencia de bloqueo formal vigente derivado del proceso de publicación, si dicha función está habilitada.

El sistema deberá mostrar claramente qué requisito impide autorizar y qué área es responsable de resolverlo.

## 8. Configuración institucional
Se deberá crear configuración versionada para, al menos:
- días mínimos de publicación de insinuados;
- tipos de ceremonia a los que aplica cada validación;
- vigencia de la regla;
- estados que habilitan autorización;
- necesidad o no de reserva de espacio;
- fotografía, nombres y apellidos completos y Taller como campos habilitados para la vista general de hermanos;
- mecanismo de observaciones/oposiciones, si se habilita;
- autoridad que puede aprobar excepciones.

Las modificaciones deben aplicar hacia adelante y conservar la regla usada en trámites históricos.

## 9. Evidencia y auditoría
La autorización debe conservar un snapshot de:
- Taller solicitante;
- ceremonia;
- persona/miembro involucrado;
- validación de Régimen Interior;
- validación de Gran Tesorería;
- validación de Gran Hospitalaria;
- publicación y plazo aplicado cuando sea iniciación;
- referencia de la fotografía autorizada para la publicación;
- excepciones;
- fecha de corte de cada antecedente;
- regla/configuración utilizada;
- autoridad que resolvió;
- documento final emitido.

## 10. Criterios de aceptación
1. Una ceremonia no puede autorizarse si el Taller está moroso/no conforme en Gran Tesorería sin excepción formal.
2. Una ceremonia no puede autorizarse si el Taller mantiene reposiciones hospitalarias exigibles pendientes sin excepción formal.
3. Una iniciación no puede autorizarse si el insinuado no cumplió el plazo mínimo de publicación configurado.
4. El plazo de publicación puede modificarse desde configuración sin recompilar el sistema.
5. El sistema conserva qué plazo y regla se aplicaron a cada solicitud histórica.
6. El insinuado puede publicarse sin crear prematuramente una membresía activa.
7. Todo hermano autenticado puede consultar la lista general de insinuados vigentes de todos los Talleres de la Orden.
8. Cada tarjeta de la lista general muestra fotografía, nombres y apellidos completos y Taller presentante.
9. La lista general no muestra RUT, contacto, domicilio, edad, profesión, patrocinantes, documentos, entrevistas ni observaciones del expediente.
10. La fotografía se entrega mediante un mecanismo autorizado y protegido, sin exponer directamente el almacenamiento privado.
11. Gran Secretaría visualiza una matriz única de requisitos y bloqueos antes de emitir la plancha.
12. Toda excepción queda asociada a autoridad, fundamento, fecha y auditoría.
13. La autorización emitida conserva evidencia de todas las validaciones utilizadas.
