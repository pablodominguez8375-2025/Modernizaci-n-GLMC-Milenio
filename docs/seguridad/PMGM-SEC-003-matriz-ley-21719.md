# PMGM-SEC-003 — Matriz técnica de cumplimiento Ley 21.719

**Estado:** Baseline de cumplimiento  
**Prioridad:** P0 transversal  
**Relacionado:** PMGM-REQ-026

## 1. Objetivo
Traducir las obligaciones relevantes de la Ley 21.719 en controles de arquitectura, aplicación, datos, seguridad y operación del Proyecto Milenio.

La Ley 21.719 fue publicada el 13-12-2024 y su vigencia general está diferida al 01-12-2026. Esta matriz se utiliza desde ahora como diseño objetivo, sin perjuicio de la normativa actualmente vigente y de los reglamentos e instrucciones que emita la autoridad competente.

## 2. Matriz de cumplimiento

| Tema | Riesgo PMGM | Control requerido | Evidencia técnica esperada |
|---|---|---|---|
| Licitud/finalidad | tratar datos sin finalidad/base definida | catálogo de tratamientos y finalidades | tabla/configuración versionada + documentación |
| Minimización | recolectar o mostrar datos innecesarios | privacidad por defecto + campos mínimos | DTOs por finalidad, masking, pruebas |
| Transparencia | titular desconoce uso de sus datos | política versionada y registro de avisos | versión, vigencia, hash y aceptación/entrega cuando corresponda |
| Acceso | acceso excesivo entre Talleres | RBAC + scope de organización | claims/policies + tests de autorización |
| Datos sensibles | exposición de pertenencia/convicciones | clasificación sensible + acceso reforzado | etiquetas/clasificación + permisos + auditoría |
| Derechos ARSOPB | no poder responder solicitudes | módulo de solicitudes de titulares | workflow, plazos, estado, evidencia de respuesta |
| Calidad/rectificación | datos erróneos o contradictorios | versionado/historial de correcciones | old/new value, actor, motivo, evidencia |
| Supresión | retención innecesaria | motor de retención y supresión | política + job + log de ejecución |
| Anonimización | conservar identidad sin necesidad | servicio de anonimización/pseudonimización | estrategia, job, evidencia irreversible cuando corresponda |
| Portal de insinuados | exposición excesiva | vista mínima separada del expediente | DTO de publicación + pruebas de no exposición |
| Tesorería/Hospitalaria | revelar detalle financiero innecesario | proyección de estado mínimo | `al_dia/no_al_dia/excepcion` sin movimientos detallados |
| Seguridad | filtración, pérdida o alteración | cifrado, MFA, RBAC, backups, monitoreo | configuración, pruebas, inventario de controles |
| Incidentes | no detectar o reportar vulneración | registro y workflow de incidentes | evento, riesgo, categorías, titulares, medidas, notificaciones |
| Proveedores | tratamiento fuera de instrucciones | registro de encargados y contratos | proveedor, contrato, datos, finalidad, subencargados |
| Transferencia internacional | cloud/soporte extranjero no controlado | inventario de transferencias | país, receptor, mecanismo jurídico, garantías |
| EIPD/DPIA | tratamiento de alto riesgo sin evaluación | gate de privacidad antes de producción | evaluación, riesgo residual, aprobación |
| Auditoría | imposibilidad de demostrar cumplimiento | auditoría inmutable o protegida | actor, acción, alcance, resultado, correlation ID |
| Decisiones automatizadas | bloqueo/resultado opaco | decisión explicable + intervención humana | razones, regla/version, decisión humana final |
| Biometría | identificación sensible futura | prohibida por defecto hasta aprobación | ADR + análisis jurídico + EIPD + configuración específica |
| Conservación histórica | conflicto entre archivo e intimidad | retención por categoría/base jurídica | política por tipo de dato + anonimización al vencer |

## 3. Clasificación de datos PMGM

### Nivel A — Público/institucional publicable
Ejemplos:
- información institucional expresamente aprobada para publicación;
- nombre de Taller y número cuando sea público;
- comunicados oficiales públicos.

Control mínimo: integridad, autorización editorial y trazabilidad.

### Nivel B — Interno
Ejemplos:
- datos básicos de contacto institucional;
- agenda interna;
- información administrativa no sensible.

Control: usuario autenticado + necesidad funcional.

### Nivel C — Restringido
Ejemplos:
- RUT;
- domicilio;
- teléfono/correo privado;
- morosidad o detalle financiero;
- antecedentes administrativos internos;
- documentos personales.

Control: rol + scope + finalidad + auditoría según riesgo.

### Nivel D — Sensible/alto impacto
Ejemplos:
- pertenencia y trayectoria masónica cuando revele convicciones filosóficas/ideológicas;
- antecedentes disciplinarios si se incorporan;
- salud/hospitalaria cuando contenga información de salud personal;
- datos biométricos si alguna vez se incorporan;
- combinaciones de datos que permitan inferir categorías sensibles.

Control: acceso explícito, minimización estricta, auditoría, cifrado reforzado cuando corresponda, EIPD según riesgo y exclusión de logs.

## 4. Regla de exposición por finalidad
La aplicación no debe devolver una entidad completa sólo porque el usuario tenga acceso general al módulo.

Se utilizarán vistas/DTO distintos por finalidad:
- `MemberPublicSummary`;
- `MemberLodgeOperationalView`;
- `MemberInternalAffairsView`;
- `TreasuryRegularityView`;
- `HospitalariaRegularityView`;
- `CandidatePublicationView`;
- `GrandSecretariatCeremonyView`.

Cada vista sólo incluirá los datos requeridos para la tarea.

## 5. Portal de insinuados
Control específico:
- la publicación será una proyección separada;
- no se serializará directamente `Person`;
- los campos publicados deberán provenir de una plantilla/política de visibilidad versionada;
- cada publicación conservará snapshot del contenido visible;
- los datos no autorizados deben permanecer inaccesibles incluso inspeccionando la API;
- toda consulta al expediente completo seguirá requiriendo autorización interna.

## 6. Transferencias entre Talleres
El cambio de Taller no equivale a copiar una ficha completa.

Se aplicará:
- identidad única;
- nueva pertenencia en destino;
- historial del origen protegido;
- acceso del destino sólo a datos necesarios;
- evaluación de conservación del histórico identificable;
- anonimización/supresión cuando corresponda por política/base jurídica;
- auditoría de la información puesta a disposición del nuevo Taller.

## 7. Retención
Debe existir una tabla/configuración `DataRetentionPolicy` o equivalente con:
- categoría de dato/tratamiento;
- finalidad;
- base jurídica;
- período o evento de expiración;
- acción: conservar, revisar, anonimizar, suprimir;
- excepción/hold legal;
- versión/vigencia.

La eliminación física no deberá ejecutarse silenciosamente: se dejará evidencia de la política aplicada sin conservar el dato eliminado.

## 8. Solicitudes de titulares
Entidad sugerida `DataSubjectRequest`:
- Id UUID;
- titular/persona;
- tipo de derecho;
- fecha de ingreso;
- canal;
- identidad verificada;
- estado;
- fecha límite;
- prórroga;
- responsable;
- resolución;
- fundamentos;
- evidencia de respuesta;
- fecha de cierre.

## 9. Registro de tratamientos
Entidad sugerida `DataProcessingActivity`:
- código;
- nombre;
- módulo;
- finalidad;
- base jurídica;
- categorías de datos;
- categorías de titulares;
- datos sensibles sí/no;
- destinatarios;
- encargados;
- transferencia internacional;
- política de conservación;
- controles principales;
- propietario/responsable interno;
- vigencia/versionado.

## 10. Encargados/proveedores
Entidad sugerida `DataProcessor` + `ProcessorAgreement`:
- proveedor;
- servicio;
- país(es) de tratamiento;
- finalidad;
- datos/categorías;
- titulares;
- subencargados;
- contrato y vigencia;
- devolución/supresión;
- obligaciones de incidente;
- controles y certificaciones declaradas;
- revisión de transferencia internacional.

## 11. Incidentes
Entidad sugerida `PrivacySecurityIncident`:
- fecha detección;
- origen;
- naturaleza;
- categorías de datos;
- sensibilidad;
- número estimado de titulares;
- impacto/riesgo;
- medidas inmediatas;
- estado;
- decisión de notificación;
- fecha y referencia de reporte a Agencia;
- comunicación a titulares cuando corresponda;
- lecciones/acciones correctivas.

## 12. EIPD/DPIA
El pipeline de producto deberá incluir un gate de privacidad:

`nuevo tratamiento → clasificación → evaluación de riesgo → ¿alto riesgo? → EIPD → mitigaciones → aprobación → desarrollo/producción`

Ninguna decisión de alto riesgo debe quedar sólo en una conversación o correo; la evaluación se almacenará como artefacto versionado.

## 13. Pruebas mínimas
- usuario de Taller A no accede a datos restringidos de Taller B;
- el portal de insinuados no devuelve RUT/email/teléfono/domicilio salvo política explícita y jurídicamente validada;
- Gran Secretaría ve estado de regularidad, no movimientos de cuenta;
- cambios de política de retención quedan versionados;
- solicitudes de rectificación conservan auditoría sin impedir corrección efectiva;
- anonimización elimina identificadores directos definidos;
- exportaciones sensibles requieren permiso y generan auditoría;
- un tratamiento marcado como alto riesgo no puede pasar el gate de producción sin EIPD aprobada.

## 14. Gate de salida a producción
Además de seguridad técnica, cada release con tratamiento personal relevante deberá responder:
1. ¿Qué datos personales trata?
2. ¿Cuál es la finalidad?
3. ¿Cuál es la base jurídica validada?
4. ¿Son sensibles?
5. ¿Quién puede acceder?
6. ¿Qué se muestra por defecto?
7. ¿Cuánto tiempo se conserva?
8. ¿Cómo se atienden derechos?
9. ¿Qué proveedores/países intervienen?
10. ¿Requiere EIPD?
11. ¿Existe auditoría suficiente?
12. ¿Existe plan de incidentes?

Un `NO DEFINIDO` en finalidad, base jurídica, conservación o acceso bloquea la promoción a producción.

## 15. Referencias funcionales de la Ley 21.719
El diseño toma en cuenta, entre otras, las siguientes materias:
- derechos de acceso, rectificación, supresión, oposición, portabilidad y bloqueo;
- deber de información/transparencia;
- protección desde el diseño y por defecto;
- seguridad proporcional al riesgo;
- reporte de vulneraciones cuando exista riesgo razonable;
- tratamiento de datos sensibles;
- regla especial para personas jurídicas sin fines de lucro de finalidad filosófica respecto de sus miembros;
- evaluación de impacto para tratamientos de alto riesgo;
- encargados/terceros mandatarios;
- transferencias internacionales.

## 16. Revisión jurídica
Esta matriz establece controles técnicos de soporte al cumplimiento. La interpretación jurídica definitiva y las políticas institucionales deberán validarse antes de producción y actualizarse frente a reglamentos, instrucciones generales y criterios que emita la Agencia de Protección de Datos Personales.
