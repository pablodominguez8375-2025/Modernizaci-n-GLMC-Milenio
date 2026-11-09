# QA525 — Handoff ChatGPT 2026-11-09 (NO_GO_FISICO)

Estado transitorio; antes de actuar consultar GitHub HEAD vivo de dev/main, Issue 525 y Google Drive Estado Maestro. Este registro no es aprobación de ejecución ni acta G6.

## Gobierno y fuentes
- Repositorio pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio; único host QA físico glmdch. srv01 es antecedente documental y nunca destino actual.
- Fuente primaria https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525
- Espejo https://docs.google.com/document/d/1gyhxyWULLLHUjm32zLu5fsrWI2wdfQlgcSm7s98LFZg/edit
- AGENTS.md y PMGM-GOV-003: PR y CI exact-head, squash SOLO dev, un merge a la vez, sin main ni host sin GO.

## Cambios ejecutados (solo GitHub)
1. PR 971 https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/971
   - Test sintético integración cinco locks/lease + nonce durable, un único archivo test blob 715d3ff24ddd34ffe9d67dc287c37c0a6ff20f26.
   - Head 56bfd9855ab2802e5777cf0259b9754921f540e5; 4/4 CI SUCCESS y Claude 0P0/P1 (cmt 6739200077).
   - Integrado squash SOLO dev 07d8df42b2ed9ac403a322a808ecb938938deb15. Push postmerge 9/9 SUCCESS, prerelease QA sintética qa-signed-07d8df42b2ed9ac403a322a808ecb938938deb15 (111724195 bytes), NO instalada.
2. PR 958 https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/958
   - G6 v2 entrypoint/lógica INACTIVA, tests, runbook. Head 009cd7e7f2dee06cb82d17ba4f67607b7f2d03c3, 7/7 workflows SUCCESS, revisión ChatGPT de código inactivo 0P0/P1 (cmt 6741760596).
   - Squash SOLO dev 9019e6e4240a979adf020ad21ffb9bb01d6c4129, CI push postmerge EN CURSO al registrar.
   - Entry directa falla G6RUN_UNTRUSTED_ENTRY sin cargar código de árbol como root; la API interna solo recibe bytes Verified. NO equivale a TCB externo ni GO físico.
3. PR 976 Claude https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/976
   - FlightPlane adaptador root_flight a root-entry; inactivo y sintético. Head reconciliado 60e5901ec937a8c5d157268bfcb673e2731ee4e5 basado dev 9019e6e.
   - Diff solo scripts/qa/qa525-vnext-root-entry.py blob b09fc0cacfa92d457f2bf2a0cd15867aa5a9b53f + tests/test_qa525_vnext_root_entry_flight_plane.py blob 7a21bc1f6f2d95cb0c05910c91927b6f5a2710f7.
   - Revisión ChatGPT 0P0/P1 PARA INACTIVO (cmt 6741813256), CI siete workflows todavía en progreso. NO MERGE hasta 7/7 SUCCESS SHA exacto, dev sin drift, dos archivos y mergeable true.

## Bloqueos físicos
- Primer byte privilegiado y ejecutable/interprete siguen SIN ancla TCB externa pinneada por Sponsor fuera Git/Drive; falta cadena autenticada APP Sigstore exact SHA + OPS v3 + GO nonce bajo cinco locks reales; #936/#822. No ejecutar código de ramas ni sudo.
- Restic B nuevo (clave anterior expuesta), G6 cifrado contemporáneo de dos BD+globals+Silo, offhost Drive re-descargado y SHA verificado, restore aislado físico y watchdog MinIO probado; PENDIENTE.
- review G6 v2 en /var/lib/pmgm-qa525-g6-v2 no se consume automáticamente por validate_g6 legacy en /opt/centenario/qa-g6 y /home/soporte. Seguimiento Issue #939 cmt 6741856843. No fabricar actas ni hacer fallback de v2 a legacy.
- GO LAUNCHER_ONLY independiente del GO API_WEB, ambos con autorización vigente específica y rollback; UAT Ver como HTTP415, Taller00 soporte, Crear usuario, Asignar perfil PENDIENTE.
- main observado sin cambios 6dfb9546a4873baff15955cf86abfd7d47e3d111; no acceso físico glmdch de esta IA, ni cambios Docker, MinIO, BD, identidad o producción.

## Continuación accionable
Validar postmerge dev 9019e6e, gates 976 y eventual merge solo dev. Priorizar un único mecanismo instalador PRE-SUDO autenticado y no otro runner. Coordinar con Claude en #958/#976/#936, sin editar su rama. Registrar nuevos SHA y resultados en Issue 525 y Estado Maestro Drive.

## Delta de continuidad 09-11 — PR #976 fusionada por Claude
GitHub PR #976 está CLOSED/MERGED, merge SHA a45cc718c514c7c39309b8e8e840f17fd7d0c5b0; el nuevo dev avanza un commit respecto de 9019e6e. Archivo root-entry.py y test FlightPlane integrados únicamente como biblioteca inactiva. Lo indicado arriba como `NO MERGE hasta 7/7` describe el corte ANTERIOR a esa fusión; NO interpretarlo como pendiente actual. Revisar CI postmerge del SHA a45cc718, artifact exacto y estado del GO físico por separado. La PR #977 de este handoff debe reconciliarse al dev ya fusionado sin force y revalidar CI propio antes de merge. NO_GO_FISICO sigue: ningún acceso host, sudo ni G6 real se acreditó aquí.
