# Proyecto Centenario — #533 / Etapa C6: autenticidad técnica de revisión GT

Fecha: 2026-10-19. Base: `dev@d8f73fe74dd30a459218c939a6e690b5e8df579d`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin autorización de promoción. QA físico en otro hilo.

## Propósito
Política backend de verificación de firma RSA-PSS SHA-256 de una **revisión documental Gran Tesorería**, ligada criptográficamente a bytes del respaldo, propuesta C3, revisión independiente y ámbito Asamblea/Taller. Usar clave pública registrada y confiable suministrada por el *servidor*, con subject, vigencia y revocación. La interfaz NO puede autoacreditar una clave. Solo material sintético de pruebas.

## Invariantes
- Verificar primero C4: hash de bytes, misma Asamblea/Taller, propuesta/revisión correctas, secuencia y autores distintos.
- Firma cubre versión del formato, IDs de Asamblea/Taller/propuesta/revisión, identidad de ambos actores, calidad de propuesta, fechas/cobertura, origen y hash del documento, datos de revisión, key-id y fecha del acto.
- Clave RSA 2048+ bits, algoritmo RSA-PSS con SHA-256. Identidad del revisor vinculada a la clave confiable.
- Rechazar documento alterado, firma manipulada, duplicidad o reemplazo de clave, llave fuera de vigencia/revocada, revisor u ámbito diferentes, sello emitido antes del acto, referencias cambiadas y firma copiada a otro expediente.
- Resultado `MayAuthorizeSuffrage=false` siempre; ningún endpoint público ni certificación de saldo histórico en C6.

## Límites
Se verifica posesión de clave asociada a firmante autorizado en registro servidor, no equivalencia jurídica de firma electrónica, ni autorización financiera definitiva. Futuro trabajo: custodia/rotación oficial de claves, atestación persistida append-only con autoridad dinámica y auditoría, certificado histórico GT por moneda y período, Tribunal/Régimen Interior y cierre de padrón. No intervenir QA, main, DNS, credenciales ni datos reales. PR draft hasta los tres gates exact-head.
