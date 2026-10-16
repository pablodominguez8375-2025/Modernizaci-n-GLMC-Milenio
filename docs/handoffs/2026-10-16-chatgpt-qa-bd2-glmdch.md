# QA G4 — PostgreSQL segregado en glmdch (trabajo en curso)

Issue #470 / QA #97. Rama aislada desde dev@dc9bc9c8467b50febfbe22422ab6d0fc60c3c807.

Alcance reservado: bootstrap de roles BD2 exclusivo de QA sintético, Keycloak y API con credenciales separadas, migrador efímero, grants/restricciones y comprobaciones. Cambios a Compose de QA sólo con modo opt-in y bloqueo a volúmenes ya existentes. No afecta el piloto reservado en #479, UI de Claude, `main`, DNS o host `glmdch`.

ESTADO: reserva inicial; todavía sin implementación ni pruebas; **despliegue QA pendiente**.
