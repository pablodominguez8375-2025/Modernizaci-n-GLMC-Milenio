# PMGM-ARCH-005 — Asambleístas, habilitación de sufragio y Tribunal de Honor

**Estado:** requisito funcional incorporado al diseño 2026  
**Ámbito:** Asamblea General de la Orden, Régimen Interior, Gran Tesorería, Tribunal de Honor, identidad y derechos masónicos.

## 1. Objetivo

Modelar en Proyecto Milenio la calidad de asambleísta, la habilitación para asistir y sufragar en la Asamblea General de la Orden, las inhabilidades asociadas al Taller y al Hermano, y la intervención del Tribunal de Honor cuando existan medidas o sanciones que afecten derechos masónicos.

## 2. Categorías que otorgan calidad de asambleísta

Una persona podrá tener calidad de **asambleísta** cuando se encuentre en una de las siguientes condiciones institucionales:

1. **Ex Venerable Maestro habilitado:** Hermano/a que cumplió su período de Venerable Maestro en su Taller y, por ello, adquiere derecho a participar como asambleísta, sujeto a las reglas de habilitación vigentes.
2. **Representante:** Hermano/a que se encuentra cumpliendo su **primer período de Venerable Maestro** en su Taller y actúa como representante de dicho Taller ante la Asamblea General de la Orden.

La calidad histórica de asambleísta no debe confundirse con la habilitación efectiva para una Asamblea específica. El sistema deberá determinar ambas por separado.

## 3. Control de habilitación

El **Departamento de Régimen Interior** es el órgano encargado de revisar y determinar si un asambleísta se encuentra habilitado para:

- asistir a una Asamblea General;
- ejercer derecho a sufragio;
- figurar en el padrón oficial de asambleístas habilitados.

La habilitación deberá calcularse para cada Asamblea y quedar registrada como una decisión trazable.

## 4. Causales de inhabilidad

Como mínimo, el sistema deberá considerar las siguientes causales:

### 4.1. Morosidad del Taller ante Gran Tesorería

Si el Taller al que pertenece o representa el asambleísta se encuentra en **morosidad con Gran Tesorería**, el asambleísta no podrá ser habilitado para sufragar mientras dicha condición se mantenga, conforme a la regla institucional vigente.

El estado financiero del Taller deberá obtenerse desde el módulo de Gran Tesorería y no ser ingresado manualmente por Régimen Interior.

### 4.2. Inhabilidad determinada por Régimen Interior

Régimen Interior podrá registrar una inhabilidad individual cuando exista una causa institucional que impida la asistencia o el sufragio.

La resolución deberá contener, como mínimo:

- persona afectada;
- tipo de restricción;
- causal o fundamento;
- fecha de inicio;
- fecha de término, si corresponde;
- acto, resolución o antecedente que la sustenta;
- autoridad o usuario que la registró.

### 4.3. Inhabilidad o sanción proveniente del Tribunal de Honor

Las decisiones del **Tribunal de Honor** que afecten los derechos masónicos del Hermano deberán reflejarse automáticamente en su habilitación electoral e institucional.

## 5. Tribunal de Honor

El Tribunal de Honor tendrá un módulo propio para la gestión reservada de antecedentes disciplinarios.

Su función en el sistema será registrar el tratamiento de situaciones relacionadas con el comportamiento de un Hermano/a, incluyendo investigaciones y sus resoluciones.

El módulo deberá permitir, como mínimo:

- apertura de investigación o expediente;
- identificación del Hermano/a involucrado/a;
- registro reservado de antecedentes y actuaciones;
- estado del procedimiento;
- resolución final;
- sanción administrativa o masónica aplicada;
- vigencia de la sanción;
- impacto sobre derechos institucionales;
- trazabilidad completa y auditoría de accesos.

## 6. Sanciones y efectos

El diseño deberá admitir, al menos, los siguientes resultados:

- advertencia u otra medida sin pérdida total de derechos, si institucionalmente corresponde;
- suspensión temporal de derechos masónicos;
- inhabilidad para asistir a determinadas instancias;
- inhabilidad para sufragar;
- expulsión total de la Orden mediante la figura institucional indicada por la GLMCh como **tratamiento**, con pérdida total de derechos masónicos.

La denominación exacta de cada sanción deberá ser parametrizable para ajustarse a la normativa oficial de la Orden.

## 7. Separación entre estado masónico y derechos efectivos

El modelo de datos deberá separar:

- **estado de pertenencia del Hermano/a**;
- **grado masónico**;
- **calidad histórica de Venerable Maestro / ex Venerable Maestro**;
- **calidad de asambleísta**;
- **rol de representante del Taller**;
- **habilitación para una Asamblea determinada**;
- **derecho de asistencia**;
- **derecho de sufragio**;
- **sanciones o restricciones vigentes**.

Esto evita que una sanción temporal destruya el historial institucional de la persona y permite reconstruir qué derechos tenía en una fecha determinada.

## 8. Propuesta de entidades

### `assembly`
- id
- nombre
- tipo
- fecha
- estado
- fecha_corte_padron

### `assembly_member_status`
- id
- person_id
- lodge_id
- categoria: `EX_VENERABLE_MAESTRO` | `REPRESENTANTE`
- fecha_desde
- fecha_hasta
- fuente_cargo_id
- activo

### `assembly_eligibility`
- id
- assembly_id
- person_id
- lodge_id
- puede_asistir
- puede_sufragar
- estado: `PENDIENTE` | `HABILITADO` | `INHABILITADO`
- causal_principal
- fecha_evaluacion
- evaluado_por
- snapshot_financiero_id
- snapshot_derechos_id

### `institutional_restriction`
- id
- person_id
- origen: `REGIMEN_INTERIOR` | `TRIBUNAL_HONOR` | `TESORERIA` | `OTRO`
- tipo_restriccion
- afecta_asistencia
- afecta_sufragio
- afecta_derechos_masonicos
- fecha_inicio
- fecha_fin
- fundamento
- expediente_referencia
- activo

### `honor_case`
- id
- person_id
- numero_expediente
- fecha_apertura
- estado
- clasificacion
- reservado
- resolucion
- fecha_resolucion

### `honor_sanction`
- id
- honor_case_id
- person_id
- tipo
- fecha_inicio
- fecha_fin
- perdida_total_derechos
- observacion
- activa

## 9. Regla de cálculo propuesta

Para una Asamblea determinada:

`puede_sufragar = es_asambleista AND taller_al_dia_tesoreria AND sin_inhabilidad_regimen_interior AND sin_sancion_tribunal_que_afecte_sufragio AND derechos_masonicos_vigentes`

La asistencia podrá manejarse de forma independiente:

`puede_asistir = es_asambleista AND sin_inhabilidad_asistencia AND derechos_masonicos_vigentes`

De esta forma, si la normativa permitiera en algún caso asistir sin votar, el sistema podrá representarlo sin cambiar el modelo.

## 10. Padrón de Asamblea

Antes de cada Asamblea, Régimen Interior deberá poder generar un padrón con:

- nombre del asambleísta;
- Taller;
- categoría: ex Venerable Maestro o representante;
- calidad de Venerable Maestro actual, cuando corresponda;
- estado financiero del Taller;
- habilitado para asistir: sí/no;
- habilitado para sufragar: sí/no;
- causal de inhabilidad visible solo a perfiles autorizados;
- fecha y hora del último cálculo.

El padrón deberá poder cerrarse en una fecha de corte y conservarse como **snapshot inmutable**, de modo que posteriormente pueda auditarse quién estaba habilitado para sufragar en esa Asamblea y por qué.

## 11. Seguridad y confidencialidad

Los expedientes del Tribunal de Honor deberán manejarse con un nivel de acceso especialmente restringido. El resto del sistema no deberá exponer el contenido de una investigación; únicamente recibirá el efecto institucional necesario, por ejemplo: `inhabilitado para sufragio hasta fecha X` o `sin derechos masónicos vigentes`.

Toda consulta, modificación o descarga de antecedentes disciplinarios deberá quedar auditada.

## 12. Integraciones requeridas

- **Régimen Interior:** determina y valida habilitación.
- **Gran Tesorería:** entrega estado de morosidad del Taller.
- **Tribunal de Honor:** entrega restricciones o pérdida de derechos aplicables.
- **Gestión de Talleres y cargos:** acredita períodos como Venerable Maestro y condición de representante.
- **Identidad / ficha del Hermano:** conserva historial de grados, cargos, derechos y sanciones.
- **Asamblea General:** genera padrón, control de asistencia y control de sufragio.

## 13. Criterios de aceptación iniciales

1. El sistema identifica automáticamente a quienes cumplen la condición de ex Venerable Maestro o representante.
2. Régimen Interior puede revisar y validar el padrón antes de una Asamblea.
3. Un Taller moroso bloquea el derecho a sufragio de sus asambleístas conforme a la regla institucional configurada.
4. Una restricción vigente de Régimen Interior puede bloquear asistencia y/o sufragio según su alcance.
5. Una sanción vigente del Tribunal de Honor puede bloquear automáticamente los derechos correspondientes sin exponer el expediente disciplinario a usuarios no autorizados.
6. La expulsión total deja a la persona sin derechos masónicos vigentes, conservando íntegro su historial institucional y disciplinario.
7. Cada Asamblea conserva un snapshot del padrón y de las causales de habilitación/inhabilitación para auditoría posterior.
8. Toda modificación manual de una habilitación exige motivo, usuario responsable, fecha/hora y queda registrada en auditoría.

## 14. Pendiente normativo para cierre funcional

Antes de congelar la regla definitiva deberán validarse con la normativa oficial de la GLMCh:

- denominación jurídica/institucional exacta de la categoría de asambleísta;
- alcance del derecho de voto de ex Venerables Maestros y representantes;
- si la morosidad del Taller impide solo sufragio o también asistencia;
- causales adicionales de inhabilidad;
- órgano competente para cada tipo de suspensión;
- catálogo oficial de sanciones del Tribunal de Honor;
- denominación exacta y efectos de la figura llamada `tratamiento`;
- reglas de rehabilitación, reincorporación o término de sanciones, cuando correspondan.
