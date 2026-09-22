# PMGM QA v0.58 — Navegación por cargo de Secretaría

## Objetivo

Verificar que Secretaría del Taller y Gran Secretaría dispongan de una sola entrada lateral por cargo, con todas sus funciones vigentes accesibles desde una navegación interna clara, sin ampliar permisos ni perder recorridos aprobados.

## QA-030

### Secretaría del Taller

- el menú lateral muestra **Secretaría** como única entrada operativa del cargo;
- no duplica `Carga de insinuados`, `Circuito de Iniciación`, `Fichas de miembros`, `Ficha de Taller`, `Gestión Logial` ni `Gestor Documental` como entradas laterales;
- la navegación interna abre Tenidas y actas, Insinuados, Circuito de iniciación, Cuadro del Taller, Ficha del Taller y Documentos;
- **Nuevo insinuado** continúa disponible desde Insinuados;
- Tesorería, Hospitalaria y Docencia no se presentan como atribuciones de Secretaría.

### Gran Secretaría

- el menú lateral muestra **Gran Secretaría** como única entrada operativa del cargo;
- no duplica Revisión de insinuados, Circuito de Iniciación, Ceremonias, Fichas de miembros ni Gestor Documental;
- la navegación interna abre Bandeja institucional, Revisión de insinuados, Circuito de iniciación, Ceremonias, Cuadro General y Documentos;
- la consulta de miembros conserva minimización;
- Planchas y Decretos se describen y cargan como PDF firmado físicamente; no se consideran generados o firmados por el sistema.

### Visual y regresión

- escritorio y móvil 390×844 sin recorte global;
- pestaña activa identificable y accesible mediante `aria-current="page"`;
- todos los perfiles ajenos conservan sus menús anteriores según permisos;
- términos `Cuadro del Taller`, `Cuadro General de la Orden` y `Padrón de la Gran Asamblea` usados en su ámbito correcto.

## Evidencia local de la rama

- frontend: **35 archivos / 153 pruebas aprobadas**;
- build TypeScript/Vite: **aprobado**;
- CI exact-head, Showcase, Pages e instalable QA: pendientes del PR.
