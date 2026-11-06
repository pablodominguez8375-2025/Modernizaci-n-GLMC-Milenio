# QA525 — admisión expirada y segunda instalación

Issue #822/#525. Base dev6eed4f00b7917da3cca36bbda70e16d16328648d. Reserva ChatGPT: qa525-signed-root-bootstrap-prep.py, tests/test_qa525_signed_root_bootstrap_prep.py y runbook nuevo. No otro launcher ni cambios API/BD. Cierre técnico previsto03-Nov sujeto revisión/CI.

Host: admisiónA cfe4 root0600 expirada; QA41d9 y journalAPI/WEBcfe4PENDING. ZIP6eed4 descargado/hashPASS, firma host aún no verificada. Archivo de admisión debe conservar bytes/receipt, ligar instalaciónCOMMITTED y launcher intacto, acta distinta TTL<=900, locksbootstrap+cincoQA, marcador durable ante fallo parcial. No ejecutar en host sin revisión y firma. Probar segunda preparación completa y rechazo por tampering/interrupción/replay antes de pedir ejecuciónSponsor. No repetir EF100 ni tocar prepared/journalsAPI/WEB.
