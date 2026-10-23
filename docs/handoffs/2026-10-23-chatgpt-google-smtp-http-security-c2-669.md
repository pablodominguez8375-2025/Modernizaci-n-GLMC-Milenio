# #669 C2 — HTTP E2E de correo Google cifrado y límite antiabuso
Fecha 23-10-2026, ChatGPT. PR #670 fusionada solo dev@6d1183b2f056caf4b3c2ef488e7e05e3ec2deb61, gates 3/3 éxito.
Se reclama una segunda capa: nuevo test integración HTTP/PostgreSQL para POST clave, GET redacción (estado vs ciphertext), GET versiones bloqueada, POST prueba con inyección de sender sintético sin red, auditoría sin PII y permiso admin; y límite especial por actor de 5 pruebas SMTP/min en infraestructura RateLimit, manteniendo el límite general para otras escrituras. Modificaciones exclusivas:
* nuevo backend/tests/PMGM.Api.Tests/Integration/SystemMailGoogleHttpTests.cs
* backend/src/PMGM.Api/Infrastructure/RequestRateLimiting.cs (solo categoría SMTP)
* .github/backend-integration-shards.json (registro nuevo test en shard B)
* nuevo este handoff y registro GOV004.
No se modificará SMTP base ni UI de Clau, tampoco claves reales, servidor físico ni main. PR draft primero; 3/3 exact SHA antes de integrar. Advertencia: no interpretar simulación con sender inyectado como envío real Gmail.
