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
Debe existir un portal específico para visualizar las publicaciones vigentes de insinuados.

El portal deberá permitir, según permisos y política institucional:
- consultar insinuados actualmente publicados;
- identificar el Taller que presenta al insinuado;
- consultar fecha de inicio y fecha prevista de cumplimiento del plazo;
- consultar estado de la publicación;
- registrar observaciones/antecedentes por canales autorizados si la Gran Logia habilita esta función;
- conservar el histórico de publicación aunque el registro deje de estar visible en el portal vigente.

## 5. Privacidad y minimización
El portal no deberá publicar automáticamente todos los datos almacenados de la persona.

Los campos visibles deberán ser definidos por configuración y política institucional. Como principio:
- mostrar sólo información necesaria para la finalidad de la publicación;
- no mostrar RUT, domicilio, teléfonos, correo privado u otros datos administrativos sensibles salvo decisión formal explícita y jurídicamente habilitada;
- separar la ficha administrativa completa de la vista de publicación;
- auditar cambios de contenido visible y fechas de publicación.

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
- campos visibles en el portal;
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
7. El portal expone sólo los datos autorizados para publicación.
8. Gran Secretaría visualiza una matriz única de requisitos y bloqueos antes de emitir la plancha.
9. Toda excepción queda asociada a autoridad, fundamento, fecha y auditoría.
10. La autorización emitida conserva evidencia de todas las validaciones utilizadas.
