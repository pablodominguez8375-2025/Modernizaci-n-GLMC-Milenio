# Handoff — QA525 #819: recuperación de preparado expirado

- Fecha: 2026-10-31 (America/Santiago).
- Responsable: ChatGPT (agente:chatgpt). Control de operación: Sponsor/PO; IA no accede a glmdch por SSH.
- Punto de partida: dev `a184936d78e6e9ee0fa85f7be060b13a5280710e`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios.
- Issue: [#819](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/819), continuidad [#525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525).
- Último QA físico validado: API/WEB `41d9c490d138419c7286445f37e7aeddf0d1b8d5`, ambos running y journal APPLIED. Paquete v0.189 `a184936d78e6e9ee0fa85f7be060b13a5280710e` preparado con Sigstore verificada, `prepared.json` presente, sin journal de target.
- Gate del operador: `VIGENCIA_RESTANTE_SEGUNDOS=-567`; `NO_APLICAR=VIGENCIA_INSUFICIENTE`. Abortó antes de ejecutar el aplicador.
- Prohibiciones: NO editar actas, timestamps, prepared ni journals en glmdch; NO repetir EF100; NO utilizar srv01, PR771/737/766, main, datos reales o producción.

## Plan de implementación

1. Incorporar un comando opt-in de recuperación de prepared ya construido, con nueva revisión técnica comprobada contra GitHub y CI/Sigstore actual.
2. Bajo locks, exigir que el prepared SHA/digest y esquema coincidan, journal target esté ausente, ningún nonce se haya consumido, baseline e imágenes de Docker sean idénticos, todo journal previo esté APPLIED y las actas antiguas estén realmente expiradas.
3. Conservar las actas antiguas en el archivo histórico root0600; **no** archivar ni eliminar `prepared.json`. Emitir actas nuevas de vida limitada mediante el controlador oficial.
4. Continuar por el aplicador supervisado existente, que revalida cada gate y hace rollback dual si corresponde; no alterar políticas de firma ni relajar requisitos.
5. Regresiones adversariales, CI exact-head, revisión multi-IA y release firmada antes de ejecutar nada en glmdch.

## Estado

En desarrollo, **sin autorización para uso físico**. No marcar como completado hasta tests y revisión.
