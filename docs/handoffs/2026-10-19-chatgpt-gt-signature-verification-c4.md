# Handoff C4 — vínculo documental preliminar, NO firma verificada (19-10-2026)

Base dev: 8cf1cfa805a90a16b2edf3c4317bb44dcb013f72. PR #567 en borrador.

Se publicó `AssemblyGrandTreasuryDocumentBindingPolicy.cs` que comprueba los bytes de un respaldo contra su SHA-256 registrado y verifica relación de propuesta/revisión, misma Asamblea y Taller y autores diferentes. Su resultado `MayAuthorizeSuffrage` permanece siempre falso.

No se publicó el verificador de firma digital RSA-PSS ni sus pruebas: dos intentos de escritura de archivos fueron rechazados por los controles de la conexión. No se intentará fusionar sin pruebas unitarias aprobadas ni constancia de firma digital verificada. No confundir SHA-256 con autenticidad del emisor: un hash por sí solo no acredita firma, saldo de Gran Tesorería ni derechos electorales.

Pendiente: firma digital con llaves públicas confiables gestionadas en servidor, pruebas negativas de alteración/firma/claves, autorización dinámica y auditoría, certificación de historial financiero, integración derechos/Tribunal y cierre. Sin endpoints, QA físico, main, srv01 ni datos reales modificados.
