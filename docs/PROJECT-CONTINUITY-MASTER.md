# Proyecto Centenario — Registro maestro de continuidad

**Última consolidación:** 13 de septiembre de 2026  
**Repositorio:** `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`  
**Fuente de código y decisiones:** rama de desarrollo del proyecto

## Propósito

Este documento reúne los acuerdos funcionales, técnicos y de gobierno definidos en las conversaciones del Proyecto Centenario / Modernización Gran Logia Mixta de Chile. Sirve como punto único de continuidad cuando una conversación anterior no aparece en la interfaz.

## Regla permanente de continuidad

Toda conversación, instrucción, corrección o decisión relacionada con el proyecto debe registrarse en este documento único. Cada actualización incorporará:

- fecha y descripción de la conversación;
- decisión o requisito aprobado;
- módulos y documentos afectados;
- implementación, pruebas y publicación asociadas;
- pendientes que pasan al siguiente ciclo.

El documento se actualiza junto con cada avance relevante y se versiona en GitHub. Si se cambia de hilo, se pierde una conversación o se retoma el proyecto después de una pausa, este archivo constituye la referencia oficial para reconstruir el contexto.

### Registro de actualizaciones

| Fecha | Conversación / decisión | Resultado |
|---|---|---|
| 13-09-2026 | Se solicita centralizar todas las conversaciones del proyecto en un único documento para preservar continuidad. | Regla permanente adoptada; este documento queda como registro maestro. |
| 13-09-2026 | Se detecta diferencia entre el reporte de Régimen Interior y el padrón QA. | Reporte alineado a 20 Talleres, 480 integrantes y distribución 12/5/5/2 por Taller. |
| 13-09-2026 | Se solicita demo operacional con cargas temporales. | Demo QA habilitada para fotografías JPG/PNG y antecedentes Word/PDF durante la sesión. |
| 13-09-2026 | Se solicita alerta transversal por rechazo en Cámara del Medio. | Antecedente protegido consultable por Régimen Interior y visible como alerta al Taller. |

## Última conversación recuperada

La última conversación documental solicitó revisar la carpeta de Google Drive **Proyecto Centenario** y comprobar que los cargos y firmantes de los documentos oficiales estuvieran incorporados al sistema.

La matriz de firmantes a validar es:

- Taller: Venerable Maestro, Secretario, Orador y Tesorero.
- Orden: Régimen Interior, Gran Tesorero, Gran Hospitalaria, Gran Secretaría y Gran Maestra.

La verificación pendiente consiste en contrastar esta matriz con cada formulario, decreto, carta, autorización de ceremonia, autorización de espacios y documento de retiro/reintegro, respetando el orden de aprobación y la firma que corresponde a cada acto.

## Acuerdos funcionales consolidados

1. La plataforma es multi-Taller y mantiene una ficha institucional única por persona.
2. Un cambio de Taller conserva el historial de origen y crea la nueva pertenencia con trazabilidad.
3. El circuito de iniciación tiene etapas separadas: ingreso, deliberación, publicación, entrevistas, Cámara del Medio, balotaje, solicitud formal, Régimen Interior, Tesorería, Hospitalaria, Gran Secretaría, Gran Maestra, plancha y ceremonia.
4. Se exigen al menos tres entrevistas; pueden existir más por decisión del Venerable Maestro. En la ficha sólo queda resumen y resultado favorable/desfavorable; el Word/PDF queda como antecedente.
5. Un rechazo en Cámara del Medio genera un antecedente transversal protegido y una alerta para Régimen Interior y cualquier Taller que intente una nueva presentación.
6. Los egresos de Tesorería y Hospitalaria requieren autorización del Venerable Maestro; el Orador participa como firmante cuando corresponda, especialmente en comunicaciones y retiros.
7. Los Talleres deben estar al día con Gran Tesorería y Gran Hospitalaria antes de autorizar ceremonias.
8. La publicación del insinuado debe cumplir el plazo configurable antes de continuar.
9. El padrón de asambleístas separa calidad histórica, habilitación para asistir y habilitación para sufragar; nunca registra el voto individual.
10. Biblioteca Virtual y Gran Archivero son ámbitos separados; el Gran Archivero no forma parte del alcance operativo actual.
11. El diseño considera minimización, trazabilidad, control de acceso y retención conforme a la Ley 21.719.

## Estado de la demo QA

- UI QA v0.56 publicada en GitHub Pages.
- 20 Talleres ficticios.
- Cada Taller: 12 Maestros, 5 Compañeros, 5 Aprendices y 2 PAS activos; total 24 por Taller y 480 en la Orden.
- Carga temporal de fotografías JPG/PNG y antecedentes Word/PDF durante la sesión del navegador.
- Los datos son ficticios; no se deben cargar antecedentes reales.
- CI, Showcase e instalador Post-RC aprobados.

## Próximo trabajo prioritario

Cerrar la auditoría documental de Google Drive: extraer cargos, firmantes, orden de aprobación y requisitos de cada documento, compararlos con los endpoints y pantallas, y registrar las brechas como requisitos o pruebas UAT antes de declarar v1.0.
