# QA525 — actualización preservando identidad

Reserva ChatGPT: scripts/qa/operators/qa525-update-readiness.py y tests/test_qa525_update_readiness.py. Base dev e3e90cbbc21d131a477445b3127b4ce96a7e3605. No tocar backend, frontend ni fuentes de otros PR.

Estado físico Sponsor: API6ff4ae0d092d4214df4ea31af484efbc969fde54, WEB hotfix424153c3a06c2b0bc0f8c2e8599eed4bb9ac6786, ambas running y marcador público coincidente con WEB. API tiene tres capas Compose, incluida identidad privada; WEB dos capas independientes. V10 histórico no preserva esta topología y no debe reejecutarse.

Primera entrega: preflight exclusivamente de lectura, sin importación de ejecutores ni opción apply. Confirmar configuración efectiva y secretos conservados sólo en memoria, identidad/Master/cifrado activos, backup reciente, salud y migraciones. Una salida PASS no autoriza un despliegue ni acredita DR. Preparación/aplicación conjunta y reversión siguen pendientes de implementar y probar después del resultado físico. main intacta.
