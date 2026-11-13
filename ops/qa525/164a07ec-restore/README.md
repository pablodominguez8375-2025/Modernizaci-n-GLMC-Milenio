# QA525 G6 restore aislado — corte 164a07ec (PREPARACIÓN, NO GO)
Kits separados de los de backup (PIN/manifiesto/GO propios). GO-T = el mismo `qa525-g6-go-termux-164a07ec.sh` de `ops/qa525/164a07ec/` (SHA 90f17664…, genérico por comando; el trailing newline puede diferir).
GO-H restore v3: valida el PIN contra el manifiesto root y mide la copia descargada (`downloads-v2` root 0600, 1 enlace, SHA del archivo, ventana 48 h, sin informe/workdir previos) ANTES de instalar el GO; ante cualquier salida el GO instalado se aparta como usado. No usa ventana MinIO.
Orden: KIT-A → (Bitwarden: MANIFEST_PIN) → KIT-B → GO-T → GO-H. Un solo intento; ante STOP no reintentar.
