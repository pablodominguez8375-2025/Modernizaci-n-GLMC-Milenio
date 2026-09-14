# PMGM — Recorrido Ejecutivo del QA

## Objetivo

Mostrar en pocos minutos que Proyecto Milenio no es una colección de pantallas aisladas, sino una plataforma institucional integrada con trazabilidad, permisos, privacidad y operación transversal.

## Preparación

Levantar el showcase autónomo:

### Windows

```powershell
.\scripts\start-showcase.ps1
```

### Linux/macOS

```bash
./scripts/start-showcase.sh
```

Abrir `http://127.0.0.1:8082`.

## Recorrido recomendado — 5 minutos

### 1. Inicio — Centro de mando

Mostrar:

- avisos pendientes;
- próximos hitos institucionales;
- insinuados actualmente publicados;
- ámbito de acceso del usuario;
- controles incorporados: Ley 21.719, auditoría, RBAC/OIDC y gestión documental protegida;
- accesos rápidos a los principales procesos.

Mensaje sugerido: **la plataforma concentra la operación sin perder la separación de funciones ni el control de acceso**.

### 2. Notificaciones

Entrar a `Notificaciones`.

Mostrar:

- avisos pendientes y leídos;
- avisos obligatorios;
- clasificación interna/confidencial;
- origen funcional de cada aviso;
- acción `Marcar como leído`.

En modo integrado, esta vista consume `/api/notifications/me` y registra la lectura mediante el endpoint dedicado. No es una bandeja ficticia separada del backend.

### 3. Calendario Institucional

Entrar a `Calendario`.

Mostrar:

- Agenda y Mes;
- Tenidas;
- docencia;
- ceremonias;
- reservas de espacios;
- filtro por Taller;
- evento protegido mostrado sólo como `Ocupado`;
- reconciliación y control de conflictos para Gran Secretaría.

Mensaje sugerido: **cada módulo mantiene su fuente de verdad, mientras el calendario proyecta una agenda común respetando permisos**.

### 4. Gran Secretaría / Ceremonias

Mostrar la relación entre:

1. solicitud de ceremonia;
2. validaciones de Régimen Interior;
3. regularidad de Gran Tesorería;
4. regularidad de Gran Hospitalaria;
5. publicación del insinuado cuando corresponda;
6. disponibilidad de espacios;
7. autorización formal;
8. proyección al calendario y notificaciones.

Mensaje sugerido: **la autorización no depende de una revisión manual informal; el sistema consolida las condiciones institucionales y deja trazabilidad**.

### 5. Gestión Logial

Mostrar:

- Tenidas;
- asistencia;
- correcciones preservando historial;
- actas versionadas;
- aprobación de la versión vigente.

Cerrar explicando que la misma información puede alimentar calendario, notificaciones, reportes y futuro expediente institucional sin duplicar bases paralelas.

## Puntos que conviene destacar ante autoridades

- Base maestra única con historia, no sobreescritura destructiva.
- Separación por roles y ámbito Orden/Taller.
- Trazabilidad de acciones críticas.
- Protección de datos incorporada desde diseño.
- Eventos sensibles en calendario sin exposición de detalles a usuarios no autorizados.
- Arquitectura preparada para crecer por módulos sin reemplazar el núcleo.
- QA autónomo para presentaciones y QA integrado para validación funcional real.

## Criterio de aceptación visual

El QA ejecutivo se considera mostrable cuando:

- Inicio carga sin errores y presenta contenido útil inmediatamente;
- Notificaciones tiene al menos tres avisos pendientes en modo demo;
- Calendario presenta eventos de múltiples fuentes;
- existe al menos un evento `Ocupado` que demuestre masking;
- el diseño es utilizable en escritorio y móvil;
- ningún dato de demostración corresponde a una persona real;
- el contenedor showcase construye y valida Nginx en CI.
