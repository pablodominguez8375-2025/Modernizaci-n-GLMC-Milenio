# PMGM-REQ-026 — Cumplimiento de Ley 21.719 sobre protección de datos personales

**Estado:** Aprobado funcionalmente  
**Prioridad:** P0 transversal  
**Áreas:** Todo el ecosistema PMGM  
**Fuente:** Definición del Product Owner + Ley 21.719 (Chile)

## 1. Objetivo
Diseñar e implementar Proyecto Milenio para cumplir, desde su arquitectura y operación, las obligaciones aplicables de la Ley N° 21.719 que modifica la Ley N° 19.628 sobre protección de datos personales y crea la Agencia de Protección de Datos Personales.

La ley fue publicada el 13-12-2024 y su vigencia general está diferida al 01-12-2026. Mientras no entre en vigencia, el proyecto deberá cumplir la normativa vigente y quedar preparado para operar bajo el nuevo estándar desde su entrada en vigencia.

## 2. Clasificación especial del dato masónico
La pertenencia, trayectoria, grado, Taller, cargos, reconocimientos, ritualidad y otros antecedentes masónicos pueden revelar convicciones filosóficas o ideológicas y, por tanto, deben tratarse como datos de sensibilidad elevada y, cuando jurídicamente corresponda, como datos personales sensibles.

La plataforma no debe asumir que todo dato masónico puede circular libremente entre miembros o Talleres.

## 3. Registro de finalidad y base de licitud
Cada categoría de tratamiento deberá tener registrada, como mínimo:
- finalidad específica;
- categoría de titulares;
- categorías de datos;
- base de licitud aplicable;
- área responsable;
- destinatarios autorizados;
- plazo o criterio de conservación;
- posibilidad de cesión o transferencia internacional;
- nivel de sensibilidad;
- controles de acceso y seguridad;
- fecha de vigencia de la política o regla.

No se permitirá crear nuevos campos o tratamientos sensibles sin asociarlos a una finalidad definida.

## 4. Minimización y privacidad por diseño y por defecto
El sistema deberá:
- recolectar sólo datos estrictamente necesarios;
- limitar por defecto la visibilidad al mínimo requerido;
- separar información pública, interna, restringida y sensible;
- evitar duplicación de datos maestros;
- utilizar UUID u otros identificadores técnicos no significativos;
- no usar RUT como clave primaria ni identificador público;
- ocultar datos sensibles en logs, errores, URLs y telemetría;
- usar seudonimización o anonimización cuando la finalidad permita prescindir de la identidad.

## 5. Derechos de los titulares
Debe existir un flujo institucional para gestionar solicitudes de:
- acceso;
- rectificación;
- supresión;
- oposición;
- portabilidad cuando corresponda;
- bloqueo temporal del tratamiento.

El módulo deberá registrar:
- fecha de ingreso;
- identidad del solicitante y medio de verificación;
- derecho ejercido;
- datos o tratamiento involucrado;
- responsable de la gestión;
- respuesta;
- fundamento de aceptación o rechazo;
- fecha de respuesta;
- evidencia de entrega/notificación;
- plazo y eventuales prórrogas.

## 6. Transparencia
La plataforma deberá soportar publicación y versionado de una Política de Tratamiento de Datos Personales que informe, según corresponda:
- responsable del tratamiento;
- medios de contacto;
- categorías de datos y titulares;
- finalidades y bases de legitimidad;
- destinatarios o cesionarios previstos;
- medidas y política de seguridad en nivel apropiado de divulgación;
- derechos de los titulares y cómo ejercerlos;
- plazos de conservación;
- fuentes de los datos;
- transferencias internacionales;
- decisiones automatizadas relevantes;
- posibilidad de retirar consentimiento cuando ésta sea la base utilizada.

## 7. Conservación, retiro y archivo histórico
La conservación no será un único plazo global.

Se definen tres capas conceptuales:
1. **Expediente operativo identificable:** necesario mientras exista una relación institucional activa o una finalidad jurídica vigente.
2. **Historial institucional mínimo:** conservará sólo los datos cuya retención identificable tenga base jurídica documentada y plazo definido.
3. **Archivo histórico/estadístico:** deberá utilizar anonimización o seudonimización reforzada cuando no sea necesario mantener identificación directa y deberá cumplir las reglas especiales aplicables a fines históricos/estadísticos.

Cuando la base para tratar datos sensibles sea la regla especial aplicable a personas jurídicas sin fines de lucro de finalidad filosófica respecto de sus miembros, el sistema deberá estar preparado para anonimizar o suprimir datos cuando cese la pertenencia, salvo que exista otra base jurídica aplicable y documentada.

Por esta razón, el requisito funcional de conservar historial entre Talleres no autoriza por sí solo una conservación indefinida de todos los datos personales identificables.

## 8. Transferencias entre Talleres
Una transferencia de Taller no crea una nueva persona ni duplica el expediente.

El Taller de origen conservará únicamente los registros que pueda mantener conforme a la finalidad y política de conservación aplicable. El Taller de destino recibirá acceso sólo a la información necesaria para la nueva pertenencia y para las finalidades institucionales autorizadas.

El sistema deberá registrar qué información se hizo accesible al Taller de destino y bajo qué finalidad.

## 9. Portal de insinuados
El Portal de Insinuados deberá aplicar privacidad por defecto:
- no mostrar RUT;
- no mostrar domicilio particular;
- no mostrar teléfono ni correo privado salvo habilitación jurídica y funcional expresa;
- no exponer documentos internos;
- publicar sólo los campos mínimos definidos en una política versionada;
- registrar fecha de publicación, retiro y modificaciones;
- separar la ficha administrativa del candidato de la vista publicada;
- conservar evidencia de la regla y contenido que estuvo vigente.

La publicación de un insinuado requerirá una base de licitud y deber de información definidos antes de su uso productivo.

## 10. Tesorería y Hospitalaria
Los datos de morosidad, pagos, obligaciones, reposiciones y regularidad tendrán acceso restringido por finalidad.

Régimen Interior y Gran Secretaría consumirán estados de regularidad necesarios para decidir trámites, evitando exponer saldos, movimientos o detalles financieros cuando no sean necesarios.

Ejemplo: para autorizar una ceremonia, Gran Secretaría podrá conocer `al día / no al día / excepción`, pero no necesita visualizar toda la cuenta corriente del Taller.

## 11. Datos sensibles y biométricos
Proyecto Milenio no incorporará biometría por defecto.

Si en el futuro se propone utilizar huella, rostro, palma, voz u otra biometría para identificación/autenticación, deberá existir previamente:
- análisis jurídico específico;
- evaluación de necesidad y proporcionalidad;
- información al titular sobre sistema, finalidad, período de uso y ejercicio de derechos;
- evaluación de impacto cuando corresponda;
- controles reforzados de seguridad;
- ADR y requisito aprobados antes del desarrollo.

## 12. Evaluación de impacto en protección de datos
El proyecto deberá soportar y documentar una Evaluación de Impacto en Protección de Datos (EIPD/DPIA) antes de tratamientos que puedan producir alto riesgo.

Como criterio de proyecto, se realizará una EIPD antes de producción para los módulos que involucren tratamiento masivo, datos sensibles de alto riesgo, decisiones automatizadas significativas o nuevas tecnologías que eleven el riesgo.

La EIPD deberá registrar:
- tratamiento y finalidad;
- necesidad y proporcionalidad;
- categorías de datos/titulares;
- riesgos para las personas;
- medidas de mitigación;
- riesgo residual;
- responsables y aprobación.

## 13. Seguridad
Los controles deberán ser proporcionales al riesgo e incluir, según corresponda:
- cifrado en tránsito;
- cifrado en reposo y/o por campo cuando el riesgo lo justifique;
- seudonimización;
- MFA para perfiles privilegiados;
- RBAC + alcance de Orden/Taller;
- mínimo privilegio;
- segregación de funciones;
- backups protegidos;
- restauración probada;
- gestión de vulnerabilidades;
- revisión periódica de controles;
- logs de seguridad sin datos personales innecesarios.

## 14. Incidentes de seguridad
Debe existir un registro de incidentes y flujo de respuesta que permita:
- detectar y clasificar una vulneración;
- determinar datos y titulares afectados;
- estimar riesgo para derechos y libertades;
- registrar efectos y medidas adoptadas;
- escalar al responsable institucional;
- preparar reporte a la Agencia cuando corresponda;
- preparar comunicación a titulares cuando corresponda;
- conservar evidencia completa del incidente y respuesta.

No se codificará un plazo fijo de notificación distinto del que establezca la ley/regulación aplicable; el criterio será actuar sin dilaciones indebidas y conforme a las instrucciones vigentes de la Agencia.

## 15. Encargados y proveedores
Todo proveedor que trate datos por cuenta de la institución deberá registrarse como encargado/tercero mandatario y asociarse a:
- contrato;
- finalidad;
- duración;
- categorías de datos;
- categorías de titulares;
- instrucciones de tratamiento;
- subencargados autorizados;
- ubicación de procesamiento/hosting;
- medidas de seguridad;
- devolución o supresión al terminar el servicio;
- obligaciones ante incidentes.

## 16. Transferencias internacionales
Toda transferencia o alojamiento que implique acceso desde otro país deberá quedar inventariado y sujeto a revisión jurídica previa.

La arquitectura debe poder registrar:
- país/territorio;
- receptor;
- categorías de datos;
- finalidad;
- mecanismo jurídico utilizado;
- garantías aplicadas;
- vigencia.

No se aprobará un proveedor cloud sólo por criterios técnicos o de costo sin revisar esta dimensión.

## 17. Auditoría y accountability
Las operaciones de alto riesgo deberán dejar evidencia de:
- actor;
- rol y alcance;
- finalidad/acción;
- entidad o dato afectado;
- fecha/hora;
- resultado;
- justificación/excepción;
- correlation ID;
- fuente o resolución cuando corresponda.

Deben auditarse especialmente:
- consultas administrativas sensibles;
- exportaciones;
- cambios de roles;
- cambios de estado institucional;
- datos financieros/hospitalarios usados en decisiones;
- autorizaciones y rechazos de ceremonias;
- modificaciones del Portal de Insinuados;
- solicitudes de derechos de titulares;
- incidentes de seguridad.

## 18. Programa de cumplimiento
La solución deberá estar preparada para soportar un programa institucional de cumplimiento y, si la Gran Logia lo adopta, la función de delegado/encargado de protección de datos, con independencia funcional definida por la institución.

La adopción del modelo de prevención/delegado se tratará como decisión institucional y jurídica; el software no asumirá que su designación sea obligatoria en todos los casos.

## 19. Criterios de aceptación
1. Todo módulo identifica las categorías de datos personales que trata.
2. Cada tratamiento relevante tiene finalidad y base jurídica documentables.
3. Los permisos respetan finalidad, rol y alcance institucional.
4. El Portal de Insinuados aplica minimización por defecto.
5. Morosidad y Hospitalaria se exponen como estados mínimos a áreas que no requieren detalle.
6. Existe un registro de solicitudes de derechos del titular.
7. Existe política de conservación diferenciada y ejecutable.
8. Existen mecanismos de anonimización/supresión.
9. Existe inventario de proveedores/encargados y transferencias internacionales.
10. Existe flujo de incidentes de seguridad y evidencia de notificación.
11. Existe auditoría de operaciones de riesgo.
12. Los tratamientos de alto riesgo pueden ser sometidos a EIPD antes de producción.
13. Ningún módulo que trate datos personales sensibles pasa a producción sin revisión de privacidad.
14. El cumplimiento se valida nuevamente antes del 01-12-2026 y antes de cada puesta en producción relevante.

## 20. Nota jurídica
Este requisito traduce obligaciones legales en capacidades de diseño y control del sistema. No reemplaza la revisión de un abogado ni las instrucciones que emita la Agencia de Protección de Datos Personales. Toda base de licitud, excepción, plazo de conservación o transferencia internacional relevante deberá validarse jurídicamente antes de producción.
