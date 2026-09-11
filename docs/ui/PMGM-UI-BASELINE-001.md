# PMGM-UI-BASELINE-001 · Registro de baseline visual institucional

**Estado actual:** PENDIENTE DE APROBACIÓN UAT VISUAL  
**Regla:** este documento no debe marcarse como `APROBADO` hasta que exista aceptación explícita del Product Owner conforme a `PMGM-UAT-VISUAL-001`.

## 1. Propósito

Identificar de forma inequívoca la versión visual aprobada de Proyecto Centenario para que futuras mejoras puedan distinguir entre:

- evolución funcional válida;
- cambio visual intencional aprobado;
- regresión visual accidental.

La baseline no es sólo una captura: es la combinación de **versión visual de código + fuente visual contractual + evidencia responsive + decisión de UAT**.

## 2. Regla de identificación de versión

Para evitar una referencia circular, se distinguen tres identificadores:

1. **SHA de código visual:** último commit que modifica frontend, estilos o automatización visual y cuyo resultado es objeto de UAT.
2. **SHA de validación:** commit posterior que puede incluir documentación y sobre el cual CI/Showcase se ejecutaron satisfactoriamente sin modificar el resultado visual respecto del SHA de código.
3. **Commit integrado en `dev`:** commit final resultante de la promoción aprobada.

Escribir o actualizar el acta UAT no obliga a cambiar el SHA de código visual aprobado si esos commits posteriores sólo modifican documentación.

## 3. Candidato actual

| Campo | Valor |
|---|---|
| Estado | Candidato, no aprobado |
| Pull Request | #62 |
| Rama | `feat/ui-ppt-responsive` |
| SHA de código visual candidato | `dc3ecd96a15154d8b93e47d8232f4ad903c4d755` |
| SHA de validación que abrió UAT | `46e786ae0ef6a7af200b9c093b9c62107607769f` |
| PMGM CI de referencia | #827 · success |
| Showcase de referencia | #151 · success |
| Artefacto visual | `pmgm-responsive-visual-evidence` |
| Especificación | `PMGM-UI-001-identidad-visual-responsive.md` |
| Acta UAT | `PMGM-UAT-VISUAL-001.md` |
| Seguimiento | Issue #63 |

Los commits posteriores que sólo modifican documentación UAT no alteran el candidato visual. Si se modifica frontend, CSS, iconografía o automatización que cambie las capturas, debe registrarse un nuevo SHA de código visual candidato y repetirse la validación.

## 4. Baseline aprobada

Completar únicamente después de la aprobación institucional.

| Campo | Valor aprobado |
|---|---|
| Estado | PENDIENTE |
| SHA de código visual aprobado | PENDIENTE |
| SHA de validación aprobado | PENDIENTE |
| Fecha aprobación | PENDIENTE |
| Product Owner | PENDIENTE |
| Run PMGM CI | PENDIENTE |
| Run Showcase | PENDIENTE |
| Digest/identificador de evidencia | PENDIENTE |
| PR integrado | PENDIENTE |
| Commit en `dev` | PENDIENTE |
| GitHub Pages verificada | PENDIENTE |

## 5. Elementos protegidos por la baseline

Una vez aprobada, los cambios posteriores deben conservar o justificar explícitamente modificaciones sobre:

- paleta institucional azul/dorado y acentos semánticos;
- tipografía y jerarquía de títulos, etiquetas y contenido;
- familia de iconos SVG institucionales;
- topbar, navegación lateral de escritorio y navegación compacta móvil/tablet;
- composición de Dashboard, Mi ficha, Biblioteca Virtual, Gestión Logial, Gran Tesorería, Gran Hospitalaria, Gran Secretaría y Gran Archivero;
- reglas responsive y ausencia de scroll horizontal global;
- comportamiento de tablas, formularios y objetivos táctiles;
- coherencia de escritorio, tablet y móvil con las PPT aprobadas.

## 6. Evidencia mínima asociada

La baseline debe estar respaldada por:

- 7 capturas de Mi ficha: 360×800, 390×844, 768×1024, 1024×768, 1366×768, 1440×900 y 1920×1080;
- capturas 390×844 y 1440×900 de Inicio, Biblioteca Virtual, Gestión Logial, Gran Tesorería, Gran Hospitalaria, Gran Secretaría y Gran Archivero;
- al menos 21 PNG en el artefacto `pmgm-responsive-visual-evidence`;
- CI y Showcase verdes sobre una validación que contenga exactamente el código visual aprobado;
- acta `PMGM-UAT-VISUAL-001` con resultado APROBADO o APROBADO CON OBSERVACIONES.

## 7. Política de regresión

Después de fijar la baseline:

1. Toda modificación visual significativa debe indicar qué elemento de la baseline cambia y por qué.
2. Un cambio funcional que no pretende alterar UI debe mantener la composición aprobada.
3. Si la evidencia automática muestra cortes, superposición, pérdida de navegación o divergencia importante, el cambio no debe promoverse hasta ser revisado.
4. Cambios institucionales de identidad visual requieren nueva aprobación del Product Owner y una nueva revisión de este registro.
5. La baseline anterior no se elimina: queda como referencia histórica mediante Git.

## 8. Relación con la demo y la VM

- **GitHub Pages:** referencia visual pública de testing, con datos ficticios.
- **VM institucional:** despliegue funcional completo; debe aplicar la misma baseline visual del frontend aprobado.
- **Paquete instalable:** debe contener la versión del frontend correspondiente al código visual promovido y no una variante distinta.

La aprobación visual en GitHub Pages no sustituye UAT funcional de la VM, pero la VM no debe introducir un frontend visualmente diferente al baseline aprobado.
