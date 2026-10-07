# Handoff — cierre técnico de #266

Reserva ChatGPT desde dev c554a6e8d08d46cfe1f3f70f0552bc495b56ea3c; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Alcance: completar aplicación de vistas/acciones del catálogo dinámico en módulos restantes, navegación y operaciones de demo; revisar impresión/exportación y regularidad individual Hospitalaria. Mantener autoridad institucional, baja lógica, auditoría y compatibilidad de sujetos no gestionados. No alterar reglas, identidad ni CSS.

Reservas: Authorization/**; endpoints de dominios con brechas y pruebas backend correspondientes; Program.cs para integración si necesaria; frontend/src/App.tsx, clientes API y hooks de acceso, controles de acciones y pruebas. Ningún PR vigente de Claude; PR históricos #58/#60 no se fusionan ni se modifican sus ramas.

Drive: búsqueda de modificaciones en Proyecto Centenario desde el cierre anterior sólo devuelve Línea Base actualizada con #359; no nuevas instrucciones aplicables. Revisión detallada de alcance y normas antes de implementación. Sin migraciones previstas.

Pendientes: inventario y código, pruebas locales/remotas, gates exact-head, merge serializado a dev, Pages/instalable QA del SHA integrado, recibo final GitHub/Drive. srv01/UAT #97 pausados: despliegue QA pendiente; no promover main.

Implementación en PR #360: filtro común de endpoints con vista/acción explícitas, fábrica ligada al handler real, origen de registros persistentes (Tenidas, Consejo, Docencia, cargas históricas, Admisiones, Insinuados, documentos/versiones/colecciones). Nunca confiar un ID a un selector de otro Taller. En IDs no resolubles se comprueban todos los contextos históricos del sujeto; en listas sin selector también se exige permiso en todos los contextos gestionados, evitando exposición parcial. Restricciones de Orden se intersectan además con el contexto del registro. Los controles financieros específicos y proyecciones mínimas inter-área permanecen en su dominio.

Proyección propia /api/session/view-access no expone catálogo/sujetos/asignaciones. Navegación, buscador, pestañas y montaje intersectan capacidades institucionales con restricciones; revocación/error retira acceso. Clientes demo se envuelven con contrato explícito de operaciones y restricciones vivas antes de leer/mutar, incluidas llamadas públicas indirectas y Consejo. Impresión por vista requiere autorización backend auditada antes de window.print; exportación/descarga sigue siendo lectura bajo permiso institucional y técnico, nunca hereda print.

Regularidad individual Hospitalaria: contrato propio /api/membership/me/hospitalaria de #355 contiene obligaciones/pagos/decreto del propio Hermano, ahora protegido por vista member y vínculo de identidad backend. No se introduce regularidad individual administrativa inexistente ni otro modelo financiero.

Sin migración ni cambio de estructuras, normativa, CSS o roles OIDC. Local frontend 462 pruebas aprobadas en corte previo; lint/build aprobados. Pruebas HTTP nuevas para denegación multi-módulo, crear sin escribir, origen contra selector falso, impresión auditada, revocación y continuidad de administración. Gates/publicación/recibo finales pendientes. Main y srv01 intactos.
