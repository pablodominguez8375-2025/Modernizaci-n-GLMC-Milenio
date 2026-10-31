# QA525 — diagnóstico seguro del controlador

Correctivo posterior a PR815 para conservar los códigos públicos permitidos de GitHub/cuota y los códigos estáticos del entrypoint en las CLI de actualización y migración. No expone excepciones arbitrarias, cuerpos, secretos ni configura accesos. Alcance autorizado: dejar futuras actualizaciones rápidas y diagnosticables. Reserva scripts/qa/operators/qa525-update.py, qa525-support00-migration.py y tests/test_qa525_update_automation.py. Sin aplicación, migraciones fuente, workflows o host. Resultados y SHA finales en descripción del PR y #525.
