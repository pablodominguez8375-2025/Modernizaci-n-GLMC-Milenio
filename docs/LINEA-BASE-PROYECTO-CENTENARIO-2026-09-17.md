# Línea Base Maestra — Proyecto Centenario

**Identificador:** LB-PC-2026-09-17  
**Estado:** vigente como referencia funcional, técnica y documental del proyecto.

## 1. Propósito y fuente de verdad

Esta línea base consolida los acuerdos funcionales, reglas normativas y decisiones técnicas del Proyecto Centenario. Desde esta versión, cualquier requisito nuevo debe registrarse como adición, modificación o eliminación, evitando reconstruir decisiones desde conversaciones antiguas.

Fuentes revisadas: Constitución y Reglamento; Protocolo para la tramitación de insinuaciones, afiliaciones y solicitudes de ceremonias 2026; formularios institucionales 2026; Matriz de Perfiles, Vistas y Firmas; acuerdos del proyecto.

## 2. Gobierno del proyecto

- Sponsor / Product Owner: Pablo Domínguez.
- Arquitectura funcional y técnica: ChatGPT / Chatito.
- Desarrollo y automatización: Codex.
- GitHub es la fuente única de verdad técnica para código, documentación, backlog y decisiones.
- Google Drive conserva los documentos oficiales y la línea base funcional/documental.

## 3. Infraestructura QA

- Servidor QA real: `srv01`.
- Sistema operativo: Ubuntu 26.04.1 LTS.
- Recursos: 8 vCPU y aproximadamente 8 GB RAM.
- Docker 29.1.3 y Docker Compose 2.40.3.
- Repositorio clonado en `/opt/centenario/app`.
- Deploy Key GitHub operativa.
- El código funcional debe consolidarse en GitHub antes del despliegue reproducible.

## 4. Alcance funcional

La plataforma será multi-Taller y mantendrá segregación de datos por Taller, con acceso transversal controlado para las autoridades superiores.

Módulos vigentes: Miembros, Talleres, Secretaría, Gestión Logial, Tenidas, Insinuaciones, Ceremonias, Tesorería, Hospitalaria, Docencia, Régimen Interior, Gran Secretaría, Gran Tesorería, Gran Hospitalaria, Biblioteca Virtual, Gran Archivero, Gestión Documental, Agenda de templos y salas, Notificaciones, Reportes, Auditoría y Sistema.

**CENDOC como módulo único queda fuera del proyecto.** Biblioteca Virtual y Gran Archivero se gestionan como componentes separados.

## 5. Perfiles de Taller y funciones

### Venerable Maestro
Máxima autoridad funcional del Taller. Preside y representa la Logia, preside Tenidas y Consejo de Administración, hace cumplir acuerdos y normativa, dirige debates, supervisa docencia, revisa registros de Tesorería y Hospitalaria, firma actas y comunicaciones y autoriza actos administrativos definidos por el flujo.

### Inmediato Ex-Venerable Maestro
Reemplaza al Venerable cuando corresponde, integra el Consejo de Administración y tiene responsabilidad directa en la instrucción de Maestros. Sustituye al antiguo perfil genérico “Ex Venerable” cuando se requiera la función reglamentaria.

### Primer Vigilante
Guardián del orden de su Columna, transmite las órdenes del Venerable, es responsable de la instrucción de Compañeros, registra cámaras de instrucción, asistencia, trabajos y seguimiento. Puede subrogar la presidencia después del Inmediato Ex-Venerable.

### Segundo Vigilante
Guardián del orden de su Columna y responsable de la instrucción de Aprendices. Registra cámaras, asistencia, trabajos y seguimiento. Puede subrogar después del Primer Vigilante.

### Orador
Referente de la Ley Masónica en la Logia. Revisa cumplimiento normativo, resuelve consultas reglamentarias, puede solicitar suspensión de debates que comprometan la armonía, lee decretos, prepara la Memoria Anual, firma actas y extractos, controla elecciones y votaciones y realiza escrutinios. No preside la Logia.

### Secretario/a
Principal operador administrativo del Taller y colaborador inmediato del Venerable. Registra y firma actas, administra correspondencia, comunica elecciones, insinuaciones, iniciaciones, aumentos, exaltaciones, afiliaciones, permisos y retiros, remite antecedentes a Gran Secretaría y custodia documentación.

### Tesorero/a
Guardián y depositario del Tesoro del Taller. Propone presupuesto, recauda y resguarda ingresos, emite comprobantes, mantiene libros y estados de Tesorería, controla morosidad, integra cotizaciones a Gran Tesorería y entrega información para la Memoria Anual.

### Hospitalario/a
Responsable independiente del Tronco de Beneficencia y de la asistencia fraternal. Custodia fondos, registra aportes y ayudas, ejecuta socorros autorizados, presenta estados mensuales y balance, visita y asiste a hermanos en necesidad y propone obras de beneficencia.

**Orden de subrogación:** Venerable Maestro → Inmediato Ex-Venerable Maestro → Primer Vigilante → Segundo Vigilante.

**Docencia:** Segundo Vigilante → Aprendices; Primer Vigilante → Compañeros; Inmediato Ex-Venerable Maestro → Maestros.

## 6. Consejo de Administración

Debe existir como entidad funcional del sistema. Lo integran Venerable Maestro, Inmediato Ex-Venerable Maestro, Primer Vigilante, Segundo Vigilante, Orador, Secretario, Tesorero y Hospitalario.

Debe permitir registrar sesiones, asistencia, acuerdos, documentos, revisiones financieras y decisiones con trazabilidad.

## 7. Miembros y estados históricos

La ficha del Hermano debe conservar historial longitudinal y no solo un estado actual.

Estados y situaciones a considerar: activo, past activo, honorario, permisos temporales, retiro voluntario, retiro forzoso, reintegro, traslado, afiliación y rayamiento cuando corresponda.

Todo cambio debe conservar fecha de vigencia, causa, acta, documento, autoridad que aprueba y auditoría. La movilidad entre Talleres conserva el historial en el Taller de origen y crea la nueva relación en el Taller de destino.

Past activo debe manejarse como flujo específico. Los permisos de ausencia deben gestionarse separados de las excusas de una Tenida y mantener vigencia y fundamento.

## 8. Cartas de retiro

**Carta de Retiro Voluntario (CRV):** aprobada por Cámara del Medio cuando se cumplen los requisitos. Firmantes funcionales: **Venerable Maestro, Tesorero, Orador y Secretario**.

**Carta de Retiro Forzoso (CRF):** debe conservar motivo, acta, votación, cuotas impagas cuando corresponda y los mismos cuatro firmantes.

Una CRF por morosidad puede transformarse posteriormente en CRV al regularizarse la situación. El sistema debe conservar ambos actos y el historial completo; nunca reemplazar ni borrar el documento anterior.

## 9. Insinuaciones

El expediente de insinuación debe ser único y versionado. Si Gran Secretaría observa o devuelve antecedentes, se corrige y reenvía el mismo expediente; no se crea una insinuación nueva.

El flujo incluye presentación, deliberación inicial, publicación institucional, entrevistas, antecedentes confidenciales, votación de tercer grado, cumplimiento del plazo de publicación y balotaje.

La publicación mínima vigente es de **20 días corridos** y la cantidad mínima de entrevistas es **tres**; ambos valores deben ser parametrizables. Los documentos y antecedentes sensibles deben tener acceso restringido.

## 10. Ceremonias

Ceremonias contempladas: iniciación, afiliación, aumento de salario, exaltación e incorporación.

La solicitud del Taller se relaciona con persona, Taller, grado, actas, requisitos, pagos y documentos. Las aprobaciones deben contemplar Régimen Interior, Gran Tesorería y Gran Hospitalaria según corresponda, con visto bueno final de Gran Maestría. Gran Secretaría emite la plancha de autorización.

Las dispensas deben registrar requisito afectado, fundamento, reducción autorizada, acta, autoridad y fecha.

## 11. Calendario masónico y Tenidas

El sistema debe distinguir calendario masónico de agenda general. Debe soportar período anual masónico, receso administrativo, autorizaciones excepcionales de Tenidas durante receso y día/hora regular del Taller.

Los cambios de día u horario deben conservar autorización y Decreto correspondiente.

Las Tenidas registran grado, tipo, fecha, oficialidad, asistencia, excusas, trabajos, correspondencia, acuerdos, votaciones, planchas, documentos y acta.

## 12. Tesorería y Hospitalaria

Tesorería y Hospitalaria son fondos y responsabilidades independientes. Los egresos deben quedar sometidos a las aprobaciones definidas en los flujos del Taller.

Gran Tesorería y Gran Hospitalaria administran la regularidad institucional y sus validaciones forman parte de las condiciones previas de determinados procesos. La información de ayudas hospitalarias debe tener protección reforzada.

## 13. Agenda de templos y salas

El sistema debe administrar disponibilidad, solicitudes, conflictos, aprobaciones, responsable y recursos técnicos. El uso del Gran Templo debe contemplar plazo mínimo de solicitud, actividad, horario, responsable de equipos y requerimientos especiales.

## 14. Sistema, seguridad y auditoría

El menú Sistema incluye usuarios, perfiles, vistas, permisos, parámetros, flujos, catálogos, logos, colores, SMTP, cuenta remitente, notificaciones, backup y restauración.

La bitácora auditable registra como mínimo usuario, fecha/hora, IP, Taller o ámbito, menú, submenú, acción, entidad afectada, identificador y resultado.

Los perfiles y vistas deben ser parametrizables sin depender de cambios de código cuando sea técnicamente razonable.

La solución debe incorporar privacidad y seguridad desde el diseño conforme a la Ley 21.719, con minimización, control de acceso, trazabilidad y protección reforzada de datos sensibles.

## 15. Demo y datos de UAT

La demo base contempla 20 Talleres. Por Taller: 12 Maestros operativos, 5 Compañeros, 5 Aprendices y 2 Past Activos.

Deben estar visibles los perfiles de Venerable, Secretario, Tesorero, Hospitalario, Orador, Primer Vigilante, Segundo Vigilante e Inmediato Ex-Venerable.

La demo debe permitir cargar fotografías y archivos Word/PDF temporales.

## 16. Regla de control de cambios

Esta línea base es el punto de comparación para toda modificación futura.

Toda decisión nueva debe quedar registrada en Drive y GitHub con identificador, descripción, motivo y efecto sobre requisitos, datos, perfiles, flujos o arquitectura.

**No se considerará vigente un cambio que solo exista en una conversación y no haya sido consolidado en la documentación del proyecto.**
