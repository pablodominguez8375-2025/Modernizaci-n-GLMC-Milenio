# PMGM-ARCH-017 — Origen y pertenencia de la ficha del Taller

**Estado:** implementación en PR hacia `dev`  
**Fecha:** 27-09-2026  
**Issue:** [#188](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/188)

## Alcance funcional

La ficha del Taller conserva tres datos históricos/institucionales editables:

- fecha de creación o fundación histórica del Taller;
- ciudad u Oriente;
- país.

La fecha se guarda como `EstablishedOn`, ciudad como `City` y país como `Country`. Los datos existentes quedan inicialmente nulos hasta su validación e ingreso; no se deducen desde la fecha técnica de alta ni desde datos externos.

`Organization.CreatedAtUtc` conserva su significado de fecha técnica de creación del registro. La interfaz la rotula **Registro creado** y la separa de la fecha histórica del Taller.

## Perfiles habilitados

La edición queda limitada a estos tres perfiles:

| Perfil | Alcance |
| --- | --- |
| Secretaría del Taller | Sólo Talleres incluidos en su claim de organización |
| Gran Secretaría | Talleres consultables con alcance institucional de Orden |
| Régimen Interior | Talleres consultables con alcance institucional de Orden |

El permiso se comprueba en el backend para cada organización; el control visual de la interfaz no reemplaza la autorización del endpoint. Otros perfiles, incluyendo Tesorería, Venerable y administrador técnico, no reciben este permiso específico.

## Relación con cuotas

La ciudad y el país permiten segmentar/reportar los Talleres y pueden orientar reglas futuras cuando exista una regla institucional aprobada. No asignan por sí mismos la clasificación `TreasuryTerritory` ni determinan una tarifa.

El Decreto 1759 de cuotas 2026 define tramos Santiago, otros Orientes y Perú, pero no contiene una correspondencia general entre cada ciudad/país posible y dichos tramos. `TreasuryTerritory` sigue siendo una clasificación separada, bajo el flujo vigente de Gran Tesorería.

## Auditoría y validación

- Sólo se modifica una entidad `workshop`.
- Se permite fecha histórica vacía si se desconoce; se rechazan fechas futuras.
- Ciudad y país aceptan texto de hasta 120 caracteres, con espacios externos eliminados y vacío normalizado a nulo.
- Cada actualización registra en auditoría los valores previos y posteriores.
- La escritura de estos campos no altera `CreatedAtUtc`, `TreasuryTerritory`, tarifas ni cuotas existentes.

## Criterios de aceptación

1. Los tres perfiles autorizados pueden modificar fecha, ciudad y país en su ámbito.
2. Un secretario de Taller no puede editar otro Taller; Secretaría de Orden puede elegir sólo Talleres accesibles por su scope.
3. Tesorería y los demás perfiles reciben denegación desde API.
4. Entidades que no son Taller no se modifican.
5. Fecha futura y texto sobre el máximo son rechazados; limpiar un campo lo guarda como nulo.
6. La ficha presenta el origen y la clasificación tarifaria por separado, y conserva visible la fecha técnica con una etiqueta distinta.
7. Los datos sintéticos de Demo permiten recorrer consulta y edición sin red; no representan una instalación ni UAT.

## Límites del corte

Los campos no están precargados con fechas/ubicaciones oficiales de Talleres. La asignación tarifaria automatizada, la validación institucional de datos históricos, la instalación en `srv01` y la UAT quedan fuera del PR. `srv01` sigue pausado.
