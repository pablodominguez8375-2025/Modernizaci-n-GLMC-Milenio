-- QA525: read-only post-migration gate for workshop registry and transition history.
-- Run only after the pinned migrator and db-grants have succeeded, while API/WEB
-- remain stopped. This is evidence, not a migration or permission grant.
BEGIN READ ONLY;
DO $qa525$
DECLARE
  history regclass := to_regclass('public."__EFMigrationsHistory"');
  transitions regclass := to_regclass('core.workshop_status_transitions');
  organizations regclass := to_regclass('core.organizations');
  missing_count integer;
BEGIN
  IF history IS NULL OR transitions IS NULL OR organizations IS NULL THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_SCHEMA_MISSING';
  END IF;

  SELECT count(*) INTO missing_count FROM (VALUES
    ('20261118150000_AddWorkshopRegistryState'),
    ('20261118170000_AddWorkshopRetiredGuard'),
    ('20261119010000_AddWorkshopStatusTransitions'),
    ('20261119010100_AddWorkshopStatusTransitionsAppendOnly')
  ) AS required(id)
  WHERE NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" h WHERE h."MigrationId" = required.id);
  IF missing_count <> 0 THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_MIGRATIONS_INCOMPLETE';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_class WHERE oid = transitions AND relkind = 'r'
    AND pg_get_userbyid(relowner) = 'pmgm_migrator') THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_TABLE_INVALID';
  END IF;
  IF (SELECT count(*) FROM pg_attribute WHERE attrelid = transitions AND attnum > 0 AND NOT attisdropped) <> 11
    OR EXISTS (
      SELECT 1 FROM (VALUES
        ('Id', 'uuid', true), ('Sequence', 'bigint', true), ('SubjectRef', 'uuid', true),
        ('PreviousStatus', 'character varying(20)', true), ('NextStatus', 'character varying(20)', true),
        ('EffectiveAtUtc', 'timestamp with time zone', true),
        ('ActorSubject', 'character varying(320)', false), ('ReasonRef', 'character varying(120)', false),
        ('Origin', 'character varying(20)', true), ('DateKnown', 'boolean', true),
        ('RecordedAtUtc', 'timestamp with time zone', true)
      ) AS required(name, data_type, not_null)
      WHERE NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = transitions
        AND a.attname = required.name AND NOT a.attisdropped
        AND format_type(a.atttypid, a.atttypmod) = required.data_type
        AND a.attnotnull = required.not_null)
    ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_COLUMNS_INVALID';
  END IF;

  IF EXISTS (
    SELECT 1 FROM (VALUES ('Status', 'character varying(20)'),
      ('Origin', 'character varying(40)'), ('RetiredAtUtc', 'timestamp with time zone'),
      ('RetiredReason', 'character varying(500)')) AS required(name, data_type)
    WHERE NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = organizations
      AND a.attname = required.name AND NOT a.attisdropped
      AND format_type(a.atttypid, a.atttypmod) = required.data_type)
  ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_REGISTRY_COLUMNS_INVALID';
  END IF;

  IF EXISTS (
    SELECT 1 FROM (VALUES
      (organizations, 'CK_organizations_Status'),
      (transitions, 'CK_workshop_status_transitions_Origin'),
      (transitions, 'CK_workshop_status_transitions_Status')
    ) AS required(table_oid, constraint_name)
    WHERE NOT EXISTS (SELECT 1 FROM pg_constraint c
      WHERE c.conrelid = required.table_oid AND c.conname = required.constraint_name
      AND c.contype = 'c' AND c.convalidated)
  ) OR NOT EXISTS (
    SELECT 1 FROM pg_class idx JOIN pg_index i ON i.indexrelid = idx.oid
    WHERE i.indrelid = transitions
      AND idx.relname = 'IX_workshop_status_transitions_SubjectRef_EffectiveAtUtc_Sequence'
      AND i.indisvalid AND i.indisready
  ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_CONSTRAINTS_INVALID';
  END IF;

  IF EXISTS (
    SELECT 1 FROM (VALUES
      ('a00_workshop_status_transitions_validate_insert', 7, 'core.workshop_status_transitions_validate_insert()'),
      ('a00_workshop_status_transitions_append_only', 27, 'core.workshop_status_transitions_append_only()'),
      ('a00_workshop_status_transitions_no_truncate', 34, 'core.workshop_status_transitions_append_only()')
    ) AS required(name, trigger_type, function_name)
    WHERE NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid = transitions
      AND t.tgname = required.name AND t.tgtype = required.trigger_type
      AND t.tgenabled = 'O' AND NOT t.tgisinternal
      AND t.tgfoid = to_regprocedure(required.function_name))
  ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_TRIGGERS_INVALID';
  END IF;

  IF NOT (
    has_table_privilege('pmgm_runtime', transitions, 'SELECT')
    AND has_table_privilege('pmgm_runtime', transitions, 'INSERT')
    AND NOT has_table_privilege('pmgm_runtime', transitions, 'UPDATE')
    AND NOT has_table_privilege('pmgm_runtime', transitions, 'DELETE')
    AND NOT has_table_privilege('pmgm_runtime', transitions, 'TRUNCATE')
  ) THEN
    RAISE EXCEPTION 'QA525_WORKSHOP_TRANSITIONS_RUNTIME_PRIVILEGES_INVALID';
  END IF;
END;
$qa525$;
COMMIT;
SELECT 'QA525_WORKSHOP_POSTMIGRATION_READ_ONLY_PASS' AS result;
