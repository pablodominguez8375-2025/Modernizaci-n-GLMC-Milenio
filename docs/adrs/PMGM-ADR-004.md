# PMGM-ADR-004 — Baseline visual-arquitectónica vinculante del producto

## Estado
Aceptada por Sponsor / Product Owner.

## Contexto
Durante la evolución técnica de Proyecto Centenario se comprobó que una implementación puede cumplir parcialmente contratos de backend, base de datos, seguridad o CI y, aun así, quedar por debajo del producto funcional y visual previamente aprobado.

Ese desalineamiento no es aceptable para una plataforma institucional que debe ser demostrable, operable y reconocible de forma consistente por Hermanos, autoridades de Taller y autoridades de Gran Logia.

Las vistas de producto objetivo aprobadas durante el diseño contienen decisiones de arquitectura de información, navegación, jerarquía, composición, funciones visibles, rol de uso y madurez gráfica. Por lo tanto, no pueden tratarse como material decorativo o meramente inspiracional.

## Decisión
Las vistas aprobadas de los módulos constituyen la **baseline visual-arquitectónica vinculante de Proyecto Centenario**.

La regla de aceptación oficial es:

> **Lo entregado debe verse y sentirse como las vistas aprobadas del producto objetivo.**

Un módulo no se considera entregado por la sola existencia de tablas, migraciones, servicios, API o pruebas técnicas. Para ser aceptado debe estar visible y utilizable desde la interfaz correspondiente al rol autorizado y conservar una experiencia coherente con la baseline.

## Vistas de referencia
La baseline cubre, como mínimo:

1. Portal del Hermano / Mi ficha.
2. Gestión Logial.
3. Tesorería Logial.
4. Hospitalaria Logial.
5. Secretaría Logial.
6. Docencia / Instructores por grado.
7. Ficha de Insinuado.
8. Régimen Interior / Gran Logia.
9. Gran Tesorería.
10. Gran Hospitalaria.
11. Gran Secretaría.
12. Gran Archivo / Gran Archivero.
13. Biblioteca Virtual.
14. Calendario y Notificaciones.
15. Interfaces transversales de administración, privacidad, auditoría y recuperación cuando correspondan.

Los artefactos visuales de referencia se mantienen en la documentación ejecutiva del proyecto y en la presentación de producto objetivo. Cuando una vista de referencia sea sustituida, deberá registrarse explícitamente qué versión la reemplaza.

## Qué significa paridad
No se exige una copia literal píxel por píxel. Sí se exige conservar:

- estructura principal de navegación;
- bloques funcionales y acciones relevantes;
- jerarquía visual y organización de la información;
- lenguaje visual institucional y nivel de terminación profesional;
- lógica de uso por rol, grado, ámbito y estado de proceso;
- comportamiento responsivo coherente en escritorio y móvil;
- estados de carga, vacío, error, sólo lectura y acción;
- separación clara entre información personal editable e institucional protegida.

Una implementación que reduzca la experiencia a una pantalla técnica, tabla genérica o navegación administrativa no alcanza paridad aunque la API subyacente funcione.

## Paridad demo / instalable
La demo y el producto instalable utilizarán los **mismos componentes React, navegación y contratos funcionales**.

Se permite que cambien:

- proveedor de autenticación;
- fuente de datos;
- identidades y registros ficticios en QA;
- configuración explícita del entorno.

No se permite mantener una aplicación paralela de demostración que oculte diferencias con el producto real.

## Perfiles mínimos de validación
Toda revisión visual/funcional relevante debe poder recorrer, al menos, estos tres perfiles:

### Hermano
Acceso personal a Mi ficha, calendario, notificaciones, insinuados publicados y Biblioteca según grado, además de las funciones personales autorizadas.

### Autoridad de Taller
Acceso anterior más operaciones propias del Taller, Secretaría Logial, Gestión Logial, documentación y procesos autorizados de su ámbito.

### Autoridad de Gran Logia
Acceso de Orden a Régimen Interior, reportería, Gran Tesorería, Gran Hospitalaria, Gran Secretaría, ceremonias, Gran Archivo y demás funciones superiores autorizadas.

Mostrar un módulo fuera del rol correspondiente se considera una falla de paridad y de autorización de interfaz, aunque el backend rechace posteriormente la acción.

## Reglas de dominio visibles
- Gran Archivo y Biblioteca Virtual son dominios distintos.
- Las planchas autorizadas pueden publicarse en Biblioteca Virtual; nunca en Gran Archivo.
- CENDOC queda fuera del alcance de Proyecto Centenario.
- La ficha privada del Insinuado puede contener foto tipo pasaporte y antecedentes completos; la publicación transversal sólo muestra los datos mínimos autorizados.
- El Hermano puede modificar únicamente datos personales expresamente habilitados. Taller, grado, estado, fechas masónicas y demás datos institucionales no se editan desde autoservicio.
- Fuera del modo QA no se utilizarán datos ficticios para rellenar módulos todavía no integrados.

## Control de cambios visual
Una desviación material requiere:

1. documentar el motivo y el impacto;
2. producir una nueva referencia visual;
3. obtener aprobación explícita del Sponsor / Product Owner;
4. registrar qué referencia anterior queda reemplazada;
5. actualizar las pruebas y criterios de aceptación asociados.

Una decisión puramente técnica no sustituye esta aprobación.

## Consecuencias

### Positivas
- Evita repetir entregas técnicamente funcionales pero visualmente distintas del producto aprobado.
- Convierte UX/UI en una parte verificable de la arquitectura y no en una capa opcional.
- Obliga a mantener coherencia entre demo, UAT e instalable.
- Facilita que QA valide por perfil y flujo, no sólo por endpoint.
- Mejora trazabilidad frente al Sponsor y futuros equipos de desarrollo.

### Costos
- Cada módulo requiere QA funcional y visual.
- Los cambios relevantes de UX deben pasar por control de cambios.
- Algunas refactorizaciones técnicas no podrán degradar temporalmente la UI de una rama candidata a integración.
- Será necesario mantener inventario de vistas y evidencia de paridad.

## Criterios de salida para módulos de interfaz
Antes de considerar cerrado un módulo con UI deben estar verificados:

- acceso sólo para roles y ámbitos correctos;
- navegación y bloques funcionales reconocibles frente a la baseline;
- datos reales en producto y ficticios sólo en QA;
- escritorio y móvil;
- estados de carga/error/vacío/lectura/edición;
- auditoría cuando exista edición o acción institucional;
- misma implementación visible en demo e instalable;
- CI técnico verde y revisión visual registrada.

## Relacionados
- PMGM-BLG-073 / issue #36 — Portal del Hermano.
- PMGM-BLG-074 / issue #37 — Formularios configurables.
- PMGM-BLG-082 / issue #56 — baseline visual-arquitectónica vinculante.
- PR #55 — paridad de perfiles Hermano / Taller / Gran Logia.
