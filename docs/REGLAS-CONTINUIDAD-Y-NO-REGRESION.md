# Reglas permanentes de continuidad y no regresión — Proyecto Centenario

Estas reglas son obligatorias para cualquier continuación del Proyecto Centenario, independientemente del chat, hilo, persona o herramienta desde la cual se retome el trabajo.

## 1. Punto de partida obligatorio

Antes de analizar, diseñar, programar, corregir o desplegar:

1. Consultar el último estado de la rama `main` de GitHub.
2. Consultar la Línea Base Maestra vigente y el Control de Cambios.
3. Revisar los documentos oficiales vigentes de Google Drive que afecten el trabajo.
4. Verificar si existe código ya desarrollado, documentación, ADR, migración, prueba o configuración relacionada.

No se debe comenzar desde un chat antiguo por defecto.

## 2. Conversaciones anteriores

Los chats, hilos y conversaciones se consideran antecedentes históricos. Pueden ayudar a entender el origen de una decisión, pero no prevalecen sobre una versión posterior consolidada en GitHub y Drive.

Si una conversación contradice la documentación versionada vigente, se utiliza la documentación versionada.

## 3. Protección del código ya desarrollado

Antes de escribir o reemplazar código se debe comprobar el último commit de `main` y los archivos existentes.

Está prohibido sustituir código ya aprobado/versionado por prototipos, ejemplos, esqueletos o versiones antiguas salvo que exista una decisión explícita de cambio registrada en el Control de Cambios.

Toda modificación debe preservar las funcionalidades aprobadas que no sean objeto del cambio.

## 4. Regla de no regresión

Ninguna continuación del proyecto puede eliminar, degradar o revertir una funcionalidad, acuerdo, modelo de datos, perfil, permiso, flujo, documento o código ya aprobado y versionado sin registrar expresamente:

- qué se reemplaza;
- motivo del cambio;
- impacto funcional y técnico;
- dependencias afectadas;
- versión o solución de reemplazo;
- estado de implementación y verificación.

## 5. Sincronización GitHub + Drive

Todo acuerdo funcional o normativo nuevo aprobado debe quedar consolidado en la misma iteración de trabajo en:

- GitHub: documentación versionada y, cuando corresponda, código;
- Google Drive: Línea Base Maestra y/o documento oficial correspondiente.

Un acuerdo que solo exista en un chat no se considera consolidado.

## 6. Diferencias entre fuentes

Si GitHub y Drive presentan diferencias materiales que afecten una implementación, no se debe elegir silenciosamente una versión. Se debe comparar la fecha, fuente y estado de aprobación, resolver la discrepancia y registrar el resultado antes de modificar el código afectado.

## 7. Orden de prevalencia operativo

Cuando existan antecedentes incompatibles, se utilizará este orden:

1. Normativa institucional vigente y documentos oficiales aplicables.
2. Línea Base Maestra vigente y cambios aprobados.
3. Código versionado en GitHub `main`, ADR y documentación técnica asociada.
4. Otros documentos vigentes de Google Drive.
5. Conversaciones y chats, únicamente como antecedente histórico.

La normativa puede obligar a modificar la línea base o el código, pero ese cambio debe registrarse formalmente.

## 8. Continuidad entre chats

Cuando se abra un nuevo chat o se retome el proyecto después de una pausa, la instrucción inicial es:

> Consultar GitHub `main`, Línea Base Maestra, Control de Cambios y documentos vigentes de Drive antes de continuar. No reconstruir el proyecto desde conversaciones antiguas.

## 9. Estado de esta regla

**Permanente hasta que el Sponsor / Product Owner la modifique expresamente y esa modificación quede versionada.**
