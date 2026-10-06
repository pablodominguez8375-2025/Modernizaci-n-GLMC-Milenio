-- Solo lectura. Ejecutar tras las migraciones hasta AddGrandTreasuryTariffVersions.
-- Resultado: todos los Talleres cuyo cache difiere de la zona de su Ficha,
-- incluyendo Fichas incompletas/inconsistentes. Sin nombres ni datos de miembros.
BEGIN TRANSACTION READ ONLY;
WITH fichas AS (
  SELECT "Id", "OrienteCode", "TreasuryTerritory",
    CASE WHEN nullif(trim("City"), '') IS NULL THEN NULL
      WHEN lower(trim("Country")) = 'chile' AND lower(trim("City")) = 'santiago' THEN 'santiago'
      WHEN lower(trim("Country")) = 'chile' THEN 'other_chile'
      WHEN lower(trim("Country")) IN ('peru', 'perú') THEN 'peru'
      ELSE NULL END AS location_code
  FROM core.organizations WHERE "Type" = 'workshop'
), zonas AS (
  SELECT *, CASE WHEN "OrienteCode" IS NULL OR "OrienteCode" = location_code THEN
    CASE coalesce("OrienteCode", location_code)
      WHEN 'santiago' THEN 'santiago' WHEN 'other_chile' THEN 'other_oriente' WHEN 'peru' THEN 'peru' END
    END AS expected_territory
  FROM fichas
)
SELECT "Id" AS organization_id, "OrienteCode" AS ficha_oriente,
  "TreasuryTerritory" AS stored_territory, expected_territory,
  CASE WHEN expected_territory IS NULL THEN 'Completar/corregir Oriente, ciudad y país en la Ficha'
       ELSE 'Guardar la Ficha para actualizar la zona derivada con auditoría' END AS correction
FROM zonas WHERE "TreasuryTerritory" IS DISTINCT FROM expected_territory OR expected_territory IS NULL
ORDER BY "Id";
ROLLBACK;
