# PMGM-PLAN-001 — Roadmap Maestro

**Versión:** 0.1  
**Objetivo:** llevar Proyecto Milenio desde la definición arquitectónica hasta una plataforma institucional operativa mediante entregas demostrables.

## 1. Estrategia de ejecución
El proyecto se desarrollará incrementalmente. Cada etapa debe producir un resultado utilizable o demostrable y no depender de completar todo el ecosistema.

Flujo de cada incremento:
Requisito → diseño → aprobación de arquitectura → desarrollo → pruebas → documentación → publicación.

## 2. Fase 0 — Cimientos y gobierno
### Resultado
Repositorio, arquitectura, seguridad, modelo de datos, backlog, entornos y convenciones listos para desarrollar.

### Entregables
- Estado Maestro.
- Arquitectura lógica.
- Modelo conceptual y lógico inicial.
- Baseline de seguridad.
- ADRs principales.
- Backlog priorizado.
- Esqueleto backend/frontend/database/infrastructure/tests.
- CI inicial.
- Docker Compose de desarrollo.
- datos sintéticos de demostración.

### Salida
Proyecto compilable y desplegable localmente, aunque aún con funcionalidad mínima.

## 3. Fase 1 — Identidad y núcleo institucional
### Alcance
- SSO.
- usuarios.
- roles y permisos.
- personas y miembros.
- Talleres/Logias.
- pertenencias.
- cargos y periodos.
- grados e historial.
- auditoría básica.

### Demostración esperada
Un usuario inicia sesión, ve su perfil y las funciones permitidas. Un administrador autorizado crea/actualiza miembros y Talleres sin duplicar datos.

## 4. Fase 2 — Secretaría y Régimen Interior
### Alcance
- expedientes institucionales.
- cambios de estado.
- historial masónico/administrativo permitido.
- certificados y solicitudes.
- control de documentación asociada.
- reportes operativos.
- flujos de aprobación.

### Demostración esperada
Un Secretario procesa una solicitud y Régimen Interior accede a la información centralizada según permisos, sin planillas paralelas.

## 5. Fase 3 — Gestión Logial y Tenidas
### Alcance
- calendario de Tenidas.
- convocatorias.
- tipos y grados.
- asistencia y excusas.
- actas.
- planchas/documentos.
- cargos del periodo.
- reportes por Taller y grado.

### Demostración esperada
Un Taller planifica una Tenida, registra asistencia, genera su acta y conserva todo en el expediente institucional.

## 6. Fase 4 — Documentos, Biblioteca y CENDOC
### Alcance
- gestor documental.
- biblioteca virtual.
- taxonomía.
- búsqueda.
- versiones.
- niveles de acceso.
- CENDOC.
- archivo histórico/museo digital inicial.

### Demostración esperada
Los miembros encuentran documentación según permisos desde el mismo acceso institucional.

## 7. Fase 5 — Tesorería y Hospitalaria
### Tesorería
- conceptos de ingreso/egreso.
- cuotas.
- obligaciones.
- conciliación y reportes.
- permisos segregados.
- auditoría financiera funcional.

### Hospitalaria
- gestión de casos/solicitudes según reglas institucionales.
- seguimiento.
- documentación restringida.
- controles de privacidad reforzados.

## 8. Fase 6 — Docencia y comunicaciones
- rutas de aprendizaje.
- material por grado/perfil.
- actividades.
- calendario institucional.
- noticias internas.
- notificaciones.
- correo y mensajería integrados.

## 9. Fase 7 — Integraciones y migración
- inventario definitivo de sistemas legados.
- migraciones por dominio.
- adaptadores temporales.
- importación con validación y reconciliación.
- retiro controlado de planillas y sistemas reemplazados.

Regla: ningún sistema legado se retira hasta validar datos, funcionalidad y contingencia.

## 10. Fase 8 — Experiencia móvil y optimización
- PWA o aplicación móvil según ADR futuro.
- experiencia simplificada para miembros.
- notificaciones push cuando exista justificación.
- optimización de rendimiento.
- observabilidad avanzada.
- mejoras de accesibilidad.

## 11. Hitos de producto
### Hito A — Esqueleto demostrable
Login simulado/real + dashboard + navegación modular + base de datos + auditoría mínima.

### Hito B — Núcleo funcional
SSO + miembros + Talleres + cargos + grados + RBAC.

### Hito C — Operación administrativa
Secretaría + Régimen Interior + solicitudes + reportes.

### Hito D — Operación logial
Tenidas + asistencia + actas + documentos.

### Hito E — Intranet unificada
Biblioteca + CENDOC + calendario + comunicaciones.

## 12. Priorización
Se usará la clasificación:
- P0: bloqueante/seguridad/cimiento.
- P1: imprescindible para MVP.
- P2: siguiente entrega.
- P3: mejora o extensión.

## 13. Definition of Done mínima
Un ítem funcional se considera terminado cuando:
- requisito y criterios de aceptación están claros;
- implementación está versionada;
- pruebas relevantes pasan;
- seguridad y permisos fueron revisados;
- migración de BD está versionada si aplica;
- documentación se actualizó;
- no contiene secretos ni datos reales de prueba;
- puede desplegarse reproduciblemente;
- Product Owner puede validarlo en un entorno demostrable.

## 14. Riesgos principales
- datos legados inconsistentes;
- duplicación de fuentes maestras;
- permisos demasiado amplios;
- crecimiento de alcance sin priorización;
- dependencia prematura de integraciones antiguas;
- migración sin reconciliación;
- ausencia de política de conservación documental;
- falta de pruebas de restauración.

## 15. Regla de gobierno del roadmap
El orden puede cambiar por prioridad institucional, pero las decisiones que afecten arquitectura, datos maestros, seguridad o interoperabilidad deberán quedar registradas mediante ADR o documento de cambio antes de su implementación.
