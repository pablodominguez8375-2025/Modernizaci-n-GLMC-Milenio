#!/usr/bin/env bash
# A0 SOLO LECTURA (Talleres #1035). Corre como `soporte` en glmdch. No modifica nada, no escribe archivos, no imprime secretos.
# v2 (corrige NO-GO 7009939051): pipefail + capturas comprobadas, línea base cotejada contra la lista de migraciones del corte da274d65
# (derivada de git: backend/src/PMGM.Api/Migrations/ en da274d651d8e9e056525413406c0aab62131504f, 100 ids), rol de la app UNKNOWN => no se adivina.
set -u
set -o pipefail
umask 077
stop() { echo "STOP: $*" >&2; exit 2; }

PATH=/usr/sbin:/usr/bin:/sbin:/bin; export PATH
EXPECTED_SOURCE_SHA="da274d651d8e9e056525413406c0aab62131504f"
EXPECTED_IDS_SHA256="50896346f08f4cf9d6613e1dd663bbb9f305fd6f863e6676093ab58e939dd744"
DELTA_IDS="20261118150000_AddWorkshopRegistryState 20261118170000_AddWorkshopRetiredGuard 20261119010000_AddWorkshopStatusTransitions 20261119010100_AddWorkshopStatusTransitionsAppendOnly"

expected_ids() { cat <<'IDS'
20260907122500_InitialCore
20260907131000_AddMembershipHistory
20260907132500_AddCeremonyEligibility
20260907133500_AddTreasuryHospitalariaRegularity
20260907141000_AddAuditAndPrivacyCompliance
20260907154500_AddRetentionHoldsAndEvaluations
20260907155000_ExpandPrivacyIncidentWorkflow
20260907160000_AddRetentionExecutionState
20260907163200_AddGrandSecretariat
20260908101500_AddLodgeManagement
20260908130000_AddDocumentManagement
20260909005000_AddLibraryDegreeSecurity
20260909011000_AddLodgeInstruction
20260909013000_AddMemberSelfServiceProfile
20260909071400_EnsureNotificationCoreSchema
20260909071500_AddInstitutionalNotifications
20260909112500_AddInstitutionalCalendar
20260909183500_EnsureRegimenInteriorCoreSchema
20260909183600_AddDataQualityCaseQueue
20260909192000_EnsureGrandArchiveCoreSchema
20260909192100_AddGrandArchive
20260909220500_AddInstitutionalBootstrap
20260910151500_AddCandidatePublicationNotificationTemplate
20260910154500_AddCandidateIntakeProfiles
20260911153000_AddLibraryCatalogMetadata
20260911181000_AddAdmissionCaseReferenceToCeremony
20260911182000_AddAdmissionCases
20260911190000_LinkAdmissionCaseToCeremony
20260912103000_AddTreasuryMonthlyStatements
20260912140000_AddAnonymousLodgeBallots
20260912141000_AddBallotProcedureNumber
20260912150000_AddActionableNotifications
20260912210000_CompleteCandidateIntake2026
20260913203000_AddMemberWithdrawalRequests
20260913220000_AddLodgeTreasury
20260913223000_AddLodgeHospitalariaMovements
20260913230000_AddLodgeApprovalControls
20260914130000_AddAuditableEventLogFields
20260917203000_AddLodgeAdministrationCouncil
20260918190000_AddSecretariatFullWorkflow
20260919013000_AddMeetingDocumentClosure
20260919043000_AddHospitalariaMonthlySubmissions
20260919210000_AddOperationalSecretariat
20260923000000_AddHospitalariaDeathReplenishments
20260923140000_AddTreasuryTerritoryToOrganizations
20260923150000_AddLodgeTreasuryIncomes
20260923160000_AddLodgeTreasuryConfiguration
20260923210000_AddLodgeTreasuryYearClosures
20260924120000_AddLodgePaymentIdempotency
20260924160000_AddCeremonyRightPayments
20260926100000_AddLodgeTreasuryReconciliations
20260926210000_AddLodgeSummaryAccessGrants
20260927100000_AddWorkPaperOwnership
20260927120000_AddWorkshopOriginMetadata
20260927180000_CompleteWorkshopProfile
20260928120000_AddTreasuryCurrenciesAndReceiptConfirmation
20260929150000_AddLodgeMemberReceiptsAndAllocations
20260929233000_AddLodgeReceiptAdjustments
20261003113000_AddWithdrawalLetterGrantedDate
20261004031137_AddDynamicAccessSnapshots
20261004205000_AddGrandTreasuryTariffVersions
20261006002500_AddLodgeUnrecoveredDues
20261007120000_AddHospitalariaOwnHistoryAndContributions
20261010021000_AddAdvancementPaperAttestations
20261010153000_AddCandidateInterviewAssignments
20261010153100_AddCandidateInterviewAssignmentNotification
20261010214500_AddCandidateInterviewAcceptance
20261010221500_AddCandidateInterviewRescheduleNotification
20261011023000_AddAdvancementDispensationCases
20261012180000_AddFormSchemaVersions
20261012200000_AddCandidateFormCaptures
20261013230000_EnforceAppendOnlyAuditEvents
20261015230000_AddCalendarEventGrade
20261017130000_AddInstitutionalSpaceRequests
20261019180000_AddAssemblyRosterFoundations
20261019190000_AddAssemblyGrandTreasuryReviewEvents
20261019210000_AddAssemblyTreasurySignedReviews
20261020090000_AddAssemblyReviewDecisionEvents
20261020214500_AddSpaceRequestDecisionNotification
20261021150000_AddCandidateOriginToCeremonyRequests
20261021154000_AddCandidateOriginCorrectionHistory
20261021162000_AddCandidateWorkshopSponsor
20261021173000_AddSocialMediaCandidateOrigin
20261022094500_AddSpaceRequestCreatedNotification
20261022160000_AddLibraryPublisherProfile
20261023130000_AddGrandArchivePublication
20261023150000_AddOfficialDocumentPublishedNotification
20261023170000_AddGrandArchivePublicationExpiry
20261023200000_AddMemberPhotos
20261023230000_AddSupportViewSessions
20261024030000_AddLibraryCategories
20261024183000_AddInitialLoadWindow
20261025000000_AddWorkPaperReview
20261025220000_AddInitialLoadBatchLedger
20261026000000_AddSupportCorrections
20261026120000_AddSupportCorrectionDetail
20261026130000_AddAssemblyAttestationEvents
20261026140000_AddAssemblyClosure
20261029120000_AddMemberHistoryProposals
20261030223000_SeedSupportWorkshop00
IDS
}
[ "$(expected_ids | sha256sum | cut -d' ' -f1)" = "$EXPECTED_IDS_SHA256" ] || stop "la lista de migraciones esperadas embebida no coincide con su hash (script alterado)"

# 1) Preflight: host, usuario y sudo sin clave. Cualquier desajuste = STOP.
[ "$(hostname)" = "glmdch" ] || stop "hostname distinto de glmdch"
[ "$(id -un)" = "soporte" ] || stop "usuario distinto de soporte"
sudo -n true >/dev/null 2>&1 || stop "sudo -n pide clave"
echo "PREFLIGHT=OK host=glmdch user=soporte fecha_utc=$(date -u +%FT%TZ)"

# 2) Contenedores e imágenes: captura comprobada (un fallo de docker NO produce resultados parciales). Solo nombres/estado/digests.
PS="$(sudo -n docker ps --format '{{.Names}} {{.Image}} {{.Status}}' 2>/dev/null)" || stop "docker ps falló"
[ -n "$PS" ] || stop "docker ps no devolvió contenedores"
IMGS="$(sudo -n docker image ls --digests --no-trunc --format '{{.Repository}}:{{.Tag}} {{.ID}} {{.Digest}}' 2>/dev/null)" || stop "docker image ls falló"
echo "== CONTENEDORES (nombre imagen estado)"; printf '%s\n' "$PS" | sort
echo "== IMAGENES (repo:tag id digest)"; printf '%s\n' "$IMGS" | sort

# 3) Contenedor de BD: descubierto, exactamente uno con imagen postgres.
DBLIST="$(printf '%s\n' "$PS" | awk '$2 ~ /postgres/ {print $1}')"
[ -n "$DBLIST" ] || stop "no hay contenedor postgres"
[ "$(printf '%s\n' "$DBLIST" | wc -l)" -eq 1 ] || stop "hay más de un contenedor postgres"
DB="$DBLIST"
printf '%s' "$DB" | grep -Eq '^[A-Za-z0-9_.-]+$' || stop "nombre de contenedor de BD no válido"
echo "CONTENEDOR_BD=$DB"

# 4) Consultas de solo lectura (transacción de solo lectura forzada). Los errores NO se imprimen (pueden contener datos).
q() { sudo -n docker exec -i -e PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=15000' "$DB" \
      sh -c 'psql -X -A -t -F "|" -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}"' 2>/dev/null; }

SRV="$(q <<'SQL'
select 'server_version=' || current_setting('server_version');
select 'database=' || current_database();
SQL
)" || stop "no se pudo consultar la BD (código $?)"
echo "== SERVIDOR"; printf '%s\n' "$SRV"

# 5) Línea base: el historial EF debe ser EXACTAMENTE la lista del corte da274d65 (sin el delta). Si no, UNKNOWN/STOP sin adaptar.
HIST="$(q <<'SQL'
select "MigrationId" from public."__EFMigrationsHistory" order by 1;
SQL
)" || stop "no se pudo leer __EFMigrationsHistory"
HIST_SORTED="$(printf '%s\n' "$HIST" | LC_ALL=C sort)"
EXP_SORTED="$(expected_ids | LC_ALL=C sort)"
echo "== HISTORIAL EF: filas=$(printf '%s\n' "$HIST_SORTED" | grep -c .) esperadas=$(printf '%s\n' "$EXP_SORTED" | grep -c .) sha256=$(printf '%s\n' "$HIST_SORTED" | sha256sum | cut -d' ' -f1)"
MISSING="$(comm -13 <(printf '%s\n' "$HIST_SORTED") <(printf '%s\n' "$EXP_SORTED"))"
EXTRA="$(comm -23 <(printf '%s\n' "$HIST_SORTED") <(printf '%s\n' "$EXP_SORTED"))"
if [ -n "$MISSING$EXTRA" ]; then
  echo "BASELINE=UNKNOWN"; echo "faltan (esperadas y no aplicadas):"; printf '%s\n' "$MISSING"; echo "sobran (aplicadas y no esperadas):"; printf '%s\n' "$EXTRA"
  stop "el historial no coincide con el corte $EXPECTED_SOURCE_SHA; no se adapta a ciegas"
fi
for id in $DELTA_IDS; do printf '%s\n' "$HIST_SORTED" | grep -qx "$id" && stop "ya está aplicada una migración del delta: $id"; done
echo "BASELINE=OK (historial = corte $EXPECTED_SOURCE_SHA, sin delta)"

SH="$(q <<'SQL'
select (select count(*) from information_schema.columns where table_schema='core' and table_name='organizations' and column_name='Status')
 || ',' || (to_regclass('core.workshop_status_transitions') is not null)::int;
SQL
)" || stop "no se pudo verificar el esquema de Talleres"
[ "$SH" = "0,0" ] || stop "el esquema ya trae Status o tabla de transiciones ($SH)"
echo "ESQUEMA_TALLERES=OK (sin columna Status ni tabla de transiciones)"

echo "== CONTEOS AGREGADOS (sin datos de personas)"
q <<'SQL' || stop "no se pudieron contar organizaciones"
select 'organizaciones_total=' || count(*) from core.organizations;
select 'organizaciones_tipo:' || "Type" || '=' || count(*) from core.organizations group by "Type" order by "Type";
SQL

# 6) Roles: dueño de core.organizations (dato de catálogo) y roles de la APLICACIÓN = sesiones de cliente de la BD que no sean el dueño ni superusuario.
#    Sin sesiones => ROLE_APP=UNKNOWN y A0 INCOMPLETO (no se adivina ni se enumeran roles globales).
echo "== ROLES RELEVANTES"
ROLES="$(q <<'SQL'
with owner as (select tableowner::text as r from pg_tables where schemaname='core' and tablename='organizations'),
 app as (select distinct usename::text as r from pg_stat_activity where datname=current_database() and backend_type='client backend' and usename is not null
         and usename::text not in (select r from owner) and not coalesce((select rolsuper from pg_roles where rolname=usename::text), true)),
 c as (select r, 'owner' as k from owner union select r, 'app' from app)
select k || '|' || r || '|super=' || coalesce((select rolsuper::text from pg_roles where rolname=r),'?')
 || '|insert=' || has_table_privilege(r,'core.organizations','INSERT')
 || '|update=' || has_table_privilege(r,'core.organizations','UPDATE')
 || '|delete=' || has_table_privilege(r,'core.organizations','DELETE')
 || '|truncate=' || has_table_privilege(r,'core.organizations','TRUNCATE')
 || '|create_en_core=' || has_schema_privilege(r,'core','CREATE')
from c order by k, r;
SQL
)" || stop "no se pudieron consultar roles/privilegios"
printf '%s\n' "$ROLES"
printf '%s\n' "$ROLES" | grep -q '^owner|' || { echo "ROLE_OWNER=UNKNOWN"; echo "A0_DIAG=INCOMPLETE_ROLE_UNKNOWN"; exit 3; }
printf '%s\n' "$ROLES" | grep -q '^app|' || { echo "ROLE_APP=UNKNOWN (sin sesiones de un rol de aplicación visibles; obtener el rol desde la configuración autorizada, no secreta)"; echo "A0_DIAG=INCOMPLETE_ROLE_UNKNOWN"; exit 3; }
echo "A0_DIAG=DONE (solo lectura; no se escribió ningún archivo)"
