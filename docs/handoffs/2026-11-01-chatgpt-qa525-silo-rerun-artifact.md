# Handoff QA525 — colisión de artifacts por reintento, 2026-11-01

PRE: dev y main consultados, dev 0d045f75e62d283987a0fa5e3d293918e113c811. Issue #822. Workflow Silo run 41342763316 intento 1 generó artefacto firmado y terminó upload principal con ECONNRESET; el artefacto auxiliar silo-rebuild-review-0d045f75 quedó guardado con ID 13098665099. Se activó rerun de job fallido intento 2. Práctica de GitHub upload-artifact v4 prohíbe otro artifact con mismo nombre dentro del run.

ALCANCE: preparar cambio MINIMO aislado en workflow .github/workflows/qa-silo-rebuild.yml para sufijar el NOMBRE de evidencia auxiliar con github.run_attempt, y prueba asociada en tests/test_silo_rebuild_contract.py. NO cambiar nombre del artefacto principal qa-glmdch-signed-storage-SHA: lo usa scripts/qa/qa-agent-readiness.py para publicación firmada y Gate QA. NO eliminar evidence original ni activar release manual.

ESTADO: este handoff se creó antes del PR draft para poder abrirlo (GitHub rechaza PR sin diferencia). No autoriza integración ni ejecución en glmdch. Verificar CI exact HEAD, luego decisión de merge sobre dev vigente, documentar Drive y GitHub. Sin changes de main, docker, API, WEB, Keycloak, G6, SMTP, EF, segunda VM o srv01.
