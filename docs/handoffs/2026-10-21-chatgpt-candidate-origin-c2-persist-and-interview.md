# #613 C2 — Origen persistido y exclusión del recomendante

Base dev@8dd95641c960d8197bfd74a0e783142d9de13eca, 21-10-2026. Agente ChatGPT. Alcance C2: campos opcionales de origen en CeremonyRequest, migración aditiva core.ceremony_requests; validación de recomendante activo en servidor al dar de alta por Secretaría Taller; exclusión al listar y asignar maestros entrevistadores. Campos NULL históricos = sin registrar. Preservar formularios antiguos hasta adaptar UI en Claude; registrar brecha de obligatoriedad de origen para nuevas solicitudes sin campos por compatibilidad.

No se toca main, QA físico, ni se autoriza voto. C3 posterior: edición de origen con motivo/auditoría y vistas de expediente/PDF; perfiles y protocolos de acceso se preservan. Gates exact-head obligatorios.
