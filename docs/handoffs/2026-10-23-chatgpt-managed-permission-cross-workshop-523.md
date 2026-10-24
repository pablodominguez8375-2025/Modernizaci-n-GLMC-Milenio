# #523 S3 — negar permisos dinámicos implícitos en Taller no asignado
Fecha: 23-10-2026; dev inicial 579dc655e2ec7ab6a66f3fcdb748c71189f50d4d; agente:chatgpt.

## Alcance
Modificar la política de permisos por vista de backend `DynamicViewAccess.Allows` y réplica frontend `allowsView`: si el sujeto ya tiene **alguna** asignación dinámica en el catálogo y se consulta un Taller **distinto** al de su asignación, denegar por defecto aunque no exista una fila para ese Taller. Conservar fallback legacy de sujetos nunca enrolados; conservar comportamiento orden/null cuando un sujeto sólo tiene perfiles por Taller para evitar regresión de `restrictClient`. Las asignaciones históricas inactivas o vencidas siguen contando para el control. Añadir pruebas negativas para sujeto asignado en A que consulta B sin ninguna asignación, y sujeto de orden sin grant Taller.

## No modificar
Permisos de datos/atribuciones institucionales, firmas, autoridades de cargos, archivos de Clau, base de datos ni listas en Drive. Esto NO activa los 5037 permisos pendientes ni cierra #523. Backend es autoridad; demo debe replicar exactamente. Gate CI/Showcase/QA antes de merge a dev, sin main ni QA físico.

## Cambios implementados
`Allows` (backend) y `allowsView` (frontend) deniegan la elevación por omisión al consultar un Taller distinto si el sujeto tiene cualquier asignación histórica, incluso inactiva. Sujetos sin ningún enrolamiento conservan el fallback legacy. `restrictClient` exige la superposición de permiso Orden sólo si el sujeto realmente posee una asignación de Orden; una asignación local de Taller no hereda a Talleres externos. Pruebas backend y frontend para Taller A/B, nulidad de ámbito, legacy y revocaciones. No altera derechos institucionales ni activa la matriz.
