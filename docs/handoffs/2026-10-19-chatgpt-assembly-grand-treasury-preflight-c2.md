# Handoff #533 — Etapa C2: autoridad y prevalidación GT

Fecha 2026-10-19. Base `dev@d32369f474108cf93282289bad1ea3bcf748c06e`; branch `feature/gran-asamblea-certificacion-gt-preflight-20261019-gpt`. QA físico es gestionado en otro chat. `main` permanece protegida.

## Alcance limitado
- Comprobar que solo un usuario **autenticado**, de alcance Gran Logia y con rol explícito **Gran Tesorería**, pueda proponer una certificación contable del Taller. La autorización efectiva en endpoints posteriores también requiere acción `write` dinámica otorgada y auditable, y no puede apoyarse en el bypass de admin de plataforma.
- Prevalidar un expediente financiero con certificación documental de apertura, meses/corte y aprobación responsable; no utilizar datos de comprobantes aislados como prueba de morosidad histórica cero.
- Un resultado de prevalidación **no produce** `AssemblyGrandTreasuryBalance` ni desbloquea padrón/votos. Certificación final aún requiere acto persistente append-only, firma/hash, revisión de rectificaciones, verificación de fuente y separación de funciones.

## Restricciones
No exponer HTTP, no introducir exportaciones, firmas reales ni cerrar padrones. No tocar UI Claude, QA, servidores, producción ni main. Nuevas pruebas de negación de Administrador sin rol GT, Venerable, Tesorero Taller y ausencias de alcance/autenticación. CI exact-HEAD requerido para integración a dev.

## Implementación de preflight (sin emisión de certificado)
- `AssemblyGrandTreasuryCertificationAuthority.HasExplicitGrandTreasuryAuthority` exige usuario autenticado, claim institucional `scope=order`, claim **explícito** `role=gran_tesoreria` y `sub` no vacío. No utiliza `InstitutionalAccessService.HasRole`, que admite bypass del superadministrador. Este guard no reemplaza el futuro `DynamicGrandTreasuryAccess.AllowsAsync(..., "write")`, ni la bitácora de autoridad.
- `AssemblyGrandTreasuryCertificationPreflight.Assess` coteja un informe interno C1 de cuadros sin inconsistencias con el expediente documental del emisor: mismo Taller/corte/cobertura, moneda ISO mayúscula, certificación de saldo de apertura a la víspera de cobertura, referencia de la resolución, registro de rectificaciones, hash SHA-256 documental y constancias de revisión.
- La salida `DocumentsReadyForIndependentReview` solo señala expediente documental listo para validación posterior. `MayAuthorizeSuffrage = false` **siempre**, aun con preflight completo. Las declaraciones de completitud **no autentican** por sí mismas el documento, la firma ni la identidad del emisor; no se obtiene `AssemblyGrandTreasuryBalance` desde este modelo.
- Pruebas `AssemblyGrandTreasuryCertificationPreflightTests`: solo rol GT expreso, niega gran administrador/administrador plataforma/RI/Tesorería Taller/VM; falta de scope/sub/autenticación; monedas, deuda apertura, fechas, SHA, referencias y cuadros observados incompletos. No exponer API ni desplegar QA. Pruebas deben aprobar CI del HEAD exacto.
- Cierre C2 posterior: diseñar persistencia de acto firmado/atestación verificada, rol y autorización dinámica en ruta backend, cadena de auditoría append-only, prueba de modificación/revocación y cálculo histórico real con corte y rectificaciones antes de alimentar habilitación. No aceptar que el funcionario marque `CompleteDebtHistoryReviewed` como sustituto de la verificación de fuentes oficiales.
