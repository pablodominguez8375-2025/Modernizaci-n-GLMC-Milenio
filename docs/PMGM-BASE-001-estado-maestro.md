# PMGM-BASE-001 — Estado Maestro del Proyecto Milenio

## 1. Objetivo
Modernizar y centralizar el ecosistema digital de la Gran Logia Mixta de Chile mediante una plataforma institucional única, segura, modular, trazable e integrada.

## 2. Principios funcionales
- Acceso único institucional mediante SSO.
- Base de datos institucional única.
- Datos maestros gobernados y no duplicados.
- Servicios internos centralizados en una sola intranet.
- Portal público separado de los procesos internos.
- Auditoría transversal.
- Seguridad y privacidad desde el diseño.
- Arquitectura modular y extensible.
- Evolución mediante MVPs y entregas demostrables.

## 3. Gobierno
- Sponsor / Product Owner: Pablo Domínguez.
- Arquitectura funcional y técnica: ChatGPT / Chatito.
- Desarrollo y automatización: Codex.
- GitHub: fuente única de verdad técnica.

## 4. Flujo de trabajo
Requisito → Diseño → Aprobación de arquitectura → Desarrollo → Pruebas → Documentación → Publicación.

## 5. Alcance funcional
### Portal público
- Noticias.
- Información institucional.
- Quiénes somos.
- Contacto.
- Publicaciones y contenidos administrados por Comunicaciones.

### Plataforma Institucional / Intranet
- Identidad y acceso.
- Dashboard.
- Miembros.
- Talleres / Logias.
- Secretaría.
- Régimen Interior.
- Gestión Logial.
- Tesorería.
- Hospitalaria.
- Docencia.
- Biblioteca Virtual.
- CENDOC / gestor documental.
- Museo o archivo histórico digital.
- Calendario.
- Tenidas.
- Ceremonias y solicitudes.
- Comunicaciones internas.
- Notificaciones.
- Auditoría.
- Reportes y memorias.
- Administración.

## 6. Sistemas actuales y estrategia
Cada sistema existente deberá clasificarse como Mantener, Integrar, Migrar, Reemplazar o Retirar.

Sistemas relevantes identificados:
- Joomla institucional.
- Intranet Joomla.
- CENDOC.
- Tesorería PHP.
- Procesos y planillas paralelas.
- Gestión documental existente.

## 7. Repositorios
- Nuevo ecosistema: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- Migración Joomla: `ehshackleton/glm-platform-migration`.

## 8. MVP
Login/SSO → Dashboard → Miembros → Talleres → Secretaría → Régimen Interior → Documentos/Biblioteca → Auditoría básica.

## 9. Decisiones vigentes
1. Este repositorio es el núcleo técnico del Proyecto Milenio.
2. `main` representa la base estable.
3. `dev` concentra integración y trabajo en curso.
4. La migración Joomla permanece separada.
5. La base de datos institucional debe ser única.
6. El acceso debe ser único y gobernado por roles.
7. La auditoría es transversal.
8. La solución será modular.
9. La seguridad y privacidad son requisitos de diseño.
10. El proyecto se desarrollará por etapas y MVPs.
