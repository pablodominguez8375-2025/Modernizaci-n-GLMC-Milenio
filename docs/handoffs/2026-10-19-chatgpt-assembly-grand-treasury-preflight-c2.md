# Handoff #533 — Etapa C2: autoridad y prevalidación GT

Fecha 2026-10-19. Base `dev@d32369f474108cf93282289bad1ea3bcf748c06e`; branch `feature/gran-asamblea-certificacion-gt-preflight-20261019-gpt`. QA físico es gestionado en otro chat. `main` permanece protegida.

## Alcance limitado
- Comprobar que solo un usuario **autenticado**, de alcance Gran Logia y con rol explícito **Gran Tesorería**, pueda proponer una certificación contable del Taller. La autorización efectiva en endpoints posteriores también requiere acción `write` dinámica otorgada y auditable, y no puede apoyarse en el bypass de admin de plataforma.
- Prevalidar un expediente financiero con certificación documental de apertura, meses/corte y aprobación responsable; no utilizar datos de comprobantes aislados como prueba de morosidad histórica cero.
- Un resultado de prevalidación **no produce** `AssemblyGrandTreasuryBalance` ni desbloquea padrón/votos. Certificación final aún requiere acto persistente append-only, firma/hash, revisión de rectificaciones, verificación de fuente y separación de funciones.

## Restricciones
No exponer HTTP, no introducir exportaciones, firmas reales ni cerrar padrones. No tocar UI Claude, QA, servidores, producción ni main. Nuevas pruebas de negación de Administrador sin rol GT, Venerable, Tesorero Taller y ausencias de alcance/autenticación. CI exact-HEAD requerido para integración a dev.
