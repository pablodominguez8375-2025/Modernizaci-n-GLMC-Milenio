-- Proyecto Centenario
-- Esquema inicial PostgreSQL para RBAC normativo de cargos de Taller.
-- No crea usuarios ni talleres: usa UUID externos para integrarse al modelo maestro.

CREATE TABLE IF NOT EXISTS rbac_role (
    role_key text PRIMARY KEY,
    display_name text NOT NULL,
    normative_reference text,
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS rbac_view (
    view_key text PRIMARY KEY,
    display_name text NOT NULL,
    sensitive boolean NOT NULL DEFAULT false,
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS rbac_action (
    action_key text PRIMARY KEY,
    display_name text NOT NULL,
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS rbac_permission (
    permission_id bigserial PRIMARY KEY,
    role_key text NOT NULL REFERENCES rbac_role(role_key),
    view_key text NOT NULL REFERENCES rbac_view(view_key),
    action_key text NOT NULL REFERENCES rbac_action(action_key),
    basis text NOT NULL CHECK (basis IN ('NormativaDirecta', 'ProtocoloInstitucional', 'ControlOperativo')),
    source_reference text NOT NULL,
    sensitive boolean NOT NULL DEFAULT false,
    active boolean NOT NULL DEFAULT true,
    UNIQUE (role_key, view_key, action_key)
);

CREATE TABLE IF NOT EXISTS rbac_temporary_grant (
    grant_id bigserial PRIMARY KEY,
    user_id uuid NOT NULL,
    workshop_id uuid NOT NULL,
    view_key text NOT NULL REFERENCES rbac_view(view_key),
    action_key text NOT NULL REFERENCES rbac_action(action_key),
    valid_from timestamptz NOT NULL,
    valid_until timestamptz NOT NULL,
    reason text NOT NULL,
    authority_reference text NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz,
    revoked_by_user_id uuid,
    CHECK (valid_until > valid_from)
);

CREATE INDEX IF NOT EXISTS ix_rbac_temporary_grant_active
    ON rbac_temporary_grant (user_id, workshop_id, valid_from, valid_until)
    WHERE revoked_at IS NULL;

CREATE TABLE IF NOT EXISTS workshop_council_decision (
    decision_id bigserial PRIMARY KEY,
    workshop_id uuid NOT NULL,
    decision_type text NOT NULL,
    decision_reference text NOT NULL,
    decision_date date NOT NULL,
    description text NOT NULL,
    recorded_by_user_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (workshop_id, decision_reference)
);

CREATE TABLE IF NOT EXISTS hospital_aid_authorization (
    aid_id uuid PRIMARY KEY,
    workshop_id uuid NOT NULL,
    council_decision_id bigint REFERENCES workshop_council_decision(decision_id),
    venerable_approval_reference text,
    authorized_at timestamptz NOT NULL,
    CHECK (
        council_decision_id IS NOT NULL
        OR NULLIF(trim(venerable_approval_reference), '') IS NOT NULL
    )
);

CREATE TABLE IF NOT EXISTS withdrawal_signature (
    withdrawal_id uuid NOT NULL,
    signer_role_key text NOT NULL REFERENCES rbac_role(role_key),
    signer_user_id uuid NOT NULL,
    signed_at timestamptz NOT NULL,
    signature_reference text NOT NULL,
    PRIMARY KEY (withdrawal_id, signer_role_key)
);

INSERT INTO rbac_role (role_key, display_name, normative_reference) VALUES
('VenerableMaestro', 'Venerable Maestro', 'Art. 11.2'),
('InmediatoExVenerableMaestro', 'Inmediato Ex-Venerable Maestro', 'Art. 12.2 y 12.5'),
('PrimerVigilante', 'Primer Vigilante', 'Art. 12.3, 12.5 y 12.6'),
('SegundoVigilante', 'Segundo Vigilante', 'Art. 12.3, 12.5 y 12.6'),
('Orador', 'Orador/a', 'Art. 12.8 y 12.9'),
('Secretario', 'Secretario/a', 'Art. 12.11'),
('Tesorero', 'Tesorero/a', 'Art. 12.12'),
('Hospitalario', 'Hospitalario/a', 'Art. 12.13')
ON CONFLICT (role_key) DO UPDATE
SET display_name = EXCLUDED.display_name,
    normative_reference = EXCLUDED.normative_reference,
    active = true;

INSERT INTO rbac_action (action_key, display_name) VALUES
('Ver', 'Ver'),
('Crear', 'Crear'),
('Editar', 'Editar'),
('Registrar', 'Registrar'),
('Revisar', 'Revisar'),
('Validar', 'Validar'),
('Aprobar', 'Aprobar'),
('Autorizar', 'Autorizar'),
('Firmar', 'Firmar'),
('Remitir', 'Remitir'),
('Informar', 'Informar'),
('Inspeccionar', 'Inspeccionar')
ON CONFLICT (action_key) DO UPDATE
SET display_name = EXCLUDED.display_name,
    active = true;

-- Las vistas y permisos específicos se sincronizarán desde el catálogo
-- Centenario.Authorization en una migración/API posterior para evitar
-- duplicar manualmente la fuente de verdad.
