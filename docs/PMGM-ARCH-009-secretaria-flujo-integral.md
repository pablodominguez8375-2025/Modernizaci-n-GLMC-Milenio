# PMGM-ARCH-009 — Flujo integral de Secretaría de Taller y Gran Secretaría

## Estado

Integrado en `dev` mediante PR #105. Merge SHA: `88e6fd70493b972467759fddc1d6180eb07927d1`. PMGM CI, Showcase y QA srv01 Installable quedaron verdes antes del merge. El despliegue físico en `srv01` continúa pendiente y se traza en Issue #97.

## 1. Propósito

Completar el ciclo operativo de Secretaría para la puesta en marcha real del Proyecto Centenario, respetando que la Gran Logia Mixta de Chile posee historia institucional previa al sistema y que los Talleres conservan un ámbito interno propio.

## 2. Fuentes institucionales contrastadas

- Constitución y Reglamento de la Gran Logia Mixta de Chile:
  - Art. 22: son activos quienes forman parte del **Cuadro de una Logia** con fuerza y vigor, junto a los demás casos definidos institucionalmente.
  - Art. 31: las Logias son libres, autónomas y soberanas en su régimen interior, subordinadas a Constitución, Reglamentos, acuerdos e instrucciones aplicables.
- Formato oficial **EXTRACTO DE ACTA**:
  - identifica Taller, Tenida, grado, fecha, asistencia, apertura, acta anterior, excusas, correspondencia, Decretos, proposiciones, balotajes, trabajo del/de la Q∴H∴ y su título, aportes, bien general, beneficencia, clausura, firmas y resumen del Cuadro.

La plancha del hermano no se confunde con el Extracto de Acta: el formato oficial registra el trabajo y su título, sin exigir que la obra completa viaje con el extracto.

## 3. Puesta en marcha — Cuadro del Taller

El sistema diferencia:

- **carga histórica / regularización inicial**: para hermanos que ya pertenecen a la Orden antes de la operación real de Centenario;
- **flujo operacional futuro**: para altas posteriores originadas por los procesos institucionales de insinuación, afiliación y ceremonias.

### 3.1 Operación de Secretaría del Taller

Secretaría puede:

1. ingresar un hermano individualmente;
2. importar una plantilla XLSX con hojas `Hermanos` y `Cargos`;
3. registrar RUT, número institucional, grado actual, pertenencia, fechas masónicas conocidas, cargos y fuente/evidencia;
4. dejar en blanco fechas históricas que no puedan acreditarse;
5. corregir borradores u observaciones;
6. enviar la propuesta a Régimen Interior.

La carga no crea ni modifica información oficial hasta la aprobación de Régimen Interior.

### 3.2 Validación de Régimen Interior

Régimen Interior puede:

- aprobar;
- observar;
- rechazar.

Sólo la aprobación materializa la información en:

- Persona/Hermano;
- pertenencia vigente al Taller;
- grado actual;
- eventos de grado cuando exista fecha exacta;
- cargos;
- Cuadro del Taller y proyecciones institucionales.

RUT y número institucional se utilizan para identificar al hermano de manera transversal y evitar duplicarlo al aparecer en más de un antecedente histórico.

## 4. Tenidas

### 4.1 Estado

- `scheduled` — **Programada**
- `held` — **Realizada**
- `closed` — sólo compatibilidad histórica de lectura, interpretado como Realizada
- `cancelled` — Cancelada

### 4.2 Modalidad

Una Tenida se registra como:

- **Presencial**: requiere referencia de templo, sala o lugar;
- **Virtual**: requiere referencia de acceso restringida.

No se define modalidad híbrida en esta versión.

La referencia de acceso virtual es información operacional restringida y no forma parte de la proyección de Gran Secretaría.

### 4.3 Plancha de trabajo del hermano

En una Tenida no ceremonial:

- la plancha es el trabajo o tema principal;
- es **opcional**;
- puede cargarse antes o después de marcar la Tenida como Realizada;
- admite PDF o DOCX;
- debe vincularse al hermano autor, integrante activo del Cuadro del Taller;
- permanece privada dentro del Taller.

No se bloquea la realización de una Tenida por ausencia de plancha adjunta.

### 4.4 Tenidas ceremoniales

Las Tenidas de:

- iniciación;
- aumento de salario;
- exaltación

no llevan plancha de trabajo del hermano.

La autorización formal de estas ceremonias se acredita con una **Plancha de Autorización de Ceremonia emitida por Gran Secretaría**, que es una figura documental distinta.

## 5. Extracto y acta completa

Para una Tenida:

- **Extracto de Acta PDF**: documento remitible a Gran Secretaría; debe estar disponible antes de remitir;
- **Acta completa**: adjunto opcional PDF/DOCX, privado del Taller;
- **Plancha de trabajo**: adjunto opcional PDF/DOCX, privado del Taller.

Una Tenida sólo puede remitirse a Gran Secretaría después de estar Realizada y tener Extracto PDF válido.

## 6. Reuniones y Consejo de Administración

Las Reuniones administrativas y el Consejo de Administración:

- pertenecen exclusivamente al ámbito interno del Taller;
- pueden tener registros documentales internos;
- pueden adjuntar extracto/acta para archivo del Taller;
- **no se remiten a Gran Secretaría**;
- no aparecen en la bandeja de Gran Secretaría.

Las Reuniones usan estados Programada/Realizada.

El Consejo conserva su entidad y workflow ya existentes; Secretaría sólo relaciona documentación, sin duplicar la sesión.

## 7. Frontera de acceso de Gran Secretaría

Gran Secretaría no reutiliza la vista interna de Secretaría del Taller.

Su proyección específica de Tenidas contiene únicamente:

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

Quedan excluidos:

- plancha de trabajo;
- autor de la plancha;
- acta completa;
- asistencia detallada;
- votaciones;
- intervenciones;
- enlace/acceso virtual;
- Reuniones;
- Consejos;
- documentación privada adicional.

La descarga del Extracto se audita.

## 8. Planchas de Gran Secretaría y Decretos

Se establecen dos categorías documentales independientes:

### Decreto

Documento formal de naturaleza `decree`, con numeración/código propio.

### Plancha

Documento formal de Gran Secretaría, sin carácter de Decreto.

Naturalezas vigentes:

- `formal_communication`: comunicado formal;
- `ceremony_authorization`: autorización formal de ceremonia.

La **Plancha de Autorización de Ceremonia**:

- sólo puede emitirse desde una solicitud ya autorizada por el flujo institucional;
- puede vincular la reserva de templo/sala;
- se identifica como Plancha;
- su contenido deja explícito que no constituye Decreto.

Se mantiene compatibilidad de lectura con códigos históricos `communication`, `ceremony_authorization` y `ceremony_authorization_plancha`, pero las nuevas emisiones usan `plancha` + `planchaKind`.

## 9. Gestión documental

Plancha del hermano, Extracto y acta completa reutilizan Gestión Documental:

- almacenamiento privado S3/MinIO;
- hash/integridad;
- análisis antimalware;
- versión documental;
- organización propietaria;
- acceso `management_only`.

No se crea un segundo repositorio de archivos.

## 10. Auditoría y privacidad

Se conservan trazas de:

- carga/importación histórica;
- envío a Régimen Interior;
- aprobación de RI;
- creación/realización de Tenidas;
- remisión de extracto;
- recepción/observación por Gran Secretaría;
- descarga de extracto;
- emisión de Plancha/Decreto.

El modelo aplica minimización y separación de funciones compatible con Ley 21.719 y con el principio de que Gran Secretaría no requiere acceso al contenido interno del Taller para recibir el Extracto institucional.

## 11. Migraciones

La implementación detectó un defecto histórico: algunas migraciones manuales compilaban pero carecían de metadata EF Core `[DbContext]` + `[Migration]`, por lo que podían no ser descubiertas.

Se corrigieron las migraciones afectadas y se reforzó `tests/migration_gate.py` para impedir futuras migraciones invisibles.

## 12. No regresión

Este incremento:

- no reemplaza Tenidas existentes;
- no reemplaza Consejo;
- no sustituye Gestión Documental;
- no expone información interna a Gran Secretaría;
- no activa todavía el flujo de nuevos iniciados como requisito de puesta en marcha;
- no declara `srv01` operacional: Issue #97 continúa siendo el hito de despliegue físico y regresión.

## 13. Regla aprobada de cierre documental de Tenidas

Por decisión expresa del Sponsor / Product Owner, se separan funcionalmente los estados **Realizada** y **Cerrada**.

Flujo objetivo:

`Programada → Realizada → Cerrada`

- **Realizada**: la Tenida efectivamente se efectuó y puede continuar completando su documentación.
- **Cerrada**: cierre documental definitivo de la Tenida, sólo permitido cuando se cumplen los documentos obligatorios según su naturaleza.
- **Cancelada**: no puede pasar a Realizada ni Cerrada.

### 13.1 Tenida regular o no ceremonial

Para cerrar una Tenida regular/no ceremonial, el único documento obligatorio es:

- **Extracto de Acta en PDF**.

No condicionan el cierre:

- la Plancha de trabajo del hermano, que continúa siendo opcional;
- el Acta completa, que continúa siendo opcional y privada del Taller.

### 13.2 Tenida ceremonial

Para cerrar una Tenida ceremonial de:

- Iniciación;
- Aumento de Salario;
- Exaltación;

deben estar asociados obligatoriamente a la misma Tenida:

1. **Extracto de Acta en PDF**; y
2. **Plancha de Autorización de Ceremonia emitida por Gran Secretaría**.

La Plancha de Autorización:

- es el documento formal que acredita la autorización institucional de la ceremonia;
- no es una Plancha de trabajo del hermano;
- no constituye Decreto;
- debe corresponder al mismo Taller y a la ceremonia autorizada;
- debe quedar vinculada/adjunta al expediente de la Tenida ceremonial;
- forma parte de la trazabilidad institucional de la ceremonia.

El sistema debe bloquear el paso a **Cerrada** si falta cualquiera de los documentos obligatorios.

### 13.3 Trazabilidad objetivo

Para una ceremonia, la continuidad documental queda:

`solicitud → aprobaciones institucionales → Plancha de Autorización de Gran Secretaría → Tenida Realizada → Extracto de Acta → Tenida Cerrada`

### 13.4 Estado de implementación

Esta regla queda **aprobada como requisito funcional vigente y documentada para continuidad**. Al momento de esta decisión, el código de `dev` todavía trata la operación histórica `closed` como equivalente de lectura a Realizada y no fuerza estas validaciones documentales al marcar una Tenida como realizada/cerrada.

Por tanto, el siguiente ajuste funcional deberá:

- incorporar el estado Cerrada con semántica documental propia;
- mantener compatibilidad con datos históricos;
- vincular la Plancha de Autorización a la Tenida ceremonial;
- validar requisitos en backend, no sólo en interfaz;
- reflejar el estado de documentos obligatorios en Secretaría del Taller;
- mantener paridad en Demo GitHub Pages e instalable QA;
- agregar pruebas de no regresión para Tenidas regulares y ceremoniales.

