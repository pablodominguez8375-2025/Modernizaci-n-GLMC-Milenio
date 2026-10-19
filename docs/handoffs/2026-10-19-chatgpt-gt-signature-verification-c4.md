# #533 — Handoff C4: integridad documental, no firma digital (19-10-2026)

Rama PR #567, reconciliada sobre dev@11035fafa877a5d042a302ba3217c45538f3191f. QA físico se trabaja en otro hilo. No modificar main.

Código: `AssemblyGrandTreasuryDocumentBindingPolicy.cs` comprueba SHA-256 real de los bytes, propuesta documental GT, revisión separada de segundo funcionario y coincidencia de Taller/Asamblea. La salida `MayAuthorizeSuffrage` es false incondicional. No hay endpoint ni tabla adicional.

Pruebas en `AssemblyDocumentBindingPolicyTests.cs`: bytes originales, alteración, autorrevisión, organización ajena y digest inválido. Integración y CI exact-head pendientes de confirmación tras rebase. **No fusionar hasta aprobación de los tres controles**.

Advertencia: **integridad SHA-256 no equivale a autenticidad de emisor ni firma digital**. Verificador de firmas RSA-PSS, registro de claves públicas confiables con revocación/vigencia, cadena de auditoría y certificación histórica GT quedan pendientes. El bloqueo previo de la conexión impidió publicar el primer diseño de verificación de firmas, así que C4 se delimitó correctamente a vínculo documental.

Para completar #533 faltan: firma digital verificable, fuente oficial histórica de saldo por moneda/corte/rectificaciones, acto de certificación definitivo por Gran Tesorería con autorización dinámica; derechos y períodos V.M. acreditados, Tribunal, decisión RI auditada, cierre de padrón y PDF/CSV con acceso autorizado. No usar preflight o review como habilitación de voto. Sin tocar QA/srv01/producción ni datos reales.
