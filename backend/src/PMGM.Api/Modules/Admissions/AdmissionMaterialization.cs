using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.SecretariatOperations;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionMaterialization
{
    public sealed record Receipt(Guid MembershipId, Guid MemberId, DateOnly EffectiveDate, string EvidenceReference);

    public static bool Matches(Receipt receipt, MaterializeAdmissionRequest request)
        => receipt.EffectiveDate == request.EffectiveDate && receipt.EvidenceReference == request.EvidenceReference.Trim();

    public static async Task<IResult> MaterializeAsync(Guid caseId, MaterializeAdmissionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        GrandSecretariatDbContext secretariatDb, LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        if (request.EffectiveDate > today || string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Length > 500)
            return Results.BadRequest(new { message = "Indique una fecha no futura y la referencia institucional de hasta 500 caracteres." });

        admissionsDb.Database.SetDbConnection(coreDb.Database.GetDbConnection());
        secretariatDb.Database.SetDbConnection(coreDb.Database.GetDbConnection());
        lodgeDb.Database.SetDbConnection(coreDb.Database.GetDbConnection());
        await using var transaction = await coreDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await admissionsDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        await secretariatDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        await lodgeDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        try
        {
            var entity = await admissionsDb.AdmissionCases.Include(x => x.Evidence).Include(x => x.Decisions)
                .SingleOrDefaultAsync(x => x.Id == caseId, ct);
            if (entity is null) return Results.NotFound();
            if (!access.CanManageLodgeSecretariat(context.User, entity.OrganizationId)) return Results.Forbid();
            var previous = entity.Decisions.SingleOrDefault(x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.MembershipMaterialized);
            if (previous is not null)
            {
                Receipt? receipt;
                try { receipt = JsonSerializer.Deserialize<Receipt>(previous.Notes ?? "null"); }
                catch (JsonException) { receipt = null; }
                if (receipt is null || !Matches(receipt, request))
                    return Results.Conflict(new { message = "El expediente ya fue materializado con otros antecedentes o requiere revisión del recibo legado." });
                var membership = await coreDb.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.MembershipId &&
                    x.MemberId == receipt.MemberId && x.OrganizationId == entity.OrganizationId, ct);
                if (membership is null) return Results.Conflict(new { message = "No se encontró la pertenencia del recibo del expediente." });
                return Results.Ok(new { idempotent = true, membershipId = membership.Id, admissionCaseId = entity.Id,
                    memberId = membership.MemberId, effectiveDate = membership.StartDate });
            }
            if (entity.Status is AdmissionWorkflowCodes.CaseStatus.Resolved or AdmissionWorkflowCodes.CaseStatus.Rejected ||
                request.EffectiveDate < AdmissionWithdrawalEvidencePolicy.ChileDate(entity.CreatedAtUtc))
                return Results.Conflict(new { message = "El estado o la fecha del expediente no permiten materializarlo." });
            var projection = AdmissionCaseEligibilityProjector.Evaluate(entity);
            if (!projection.Decision.CanProceed)
                return Results.Conflict(new { message = "El expediente no cumple los requisitos vigentes.", projection.Decision.Requirements });

            var ceremony = await coreDb.CeremonyRequests.SingleOrDefaultAsync(x => x.AdmissionCaseId == caseId, ct);
            if (ceremony is null || ceremony.Status != CeremonyCodes.RequestStatus.Authorized ||
                ceremony.OrganizationId != entity.OrganizationId || ceremony.CeremonyType != entity.AdmissionType ||
                ceremony.ProposedDate != request.EffectiveDate)
                return Results.Conflict(new { message = "Se requiere una ceremonia autorizada del mismo expediente, Taller, tipo y fecha." });
            var authorizations = await secretariatDb.SecretariatDocuments.AsNoTracking().Where(x =>
                x.RelatedCeremonyRequestId == ceremony.Id && x.OrganizationId == entity.OrganizationId &&
                x.Status == GrandSecretariatCodes.DocumentStatus.Issued &&
                ((x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha && x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization) ||
                 x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationLegacy ||
                 x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationPlanchaLegacy))
                .Select(x => x.Id).ToListAsync(ct);
            var records = await coreDb.LodgeSecretariatRecords.AsNoTracking().Where(x =>
                x.OrganizationId == entity.OrganizationId && x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                x.EventDate == request.EffectiveDate && x.CeremonyAuthorizationDocumentId != null &&
                authorizations.Contains(x.CeremonyAuthorizationDocumentId.Value) &&
                x.ExtractDocumentVersionId != null && x.FullMinuteDocumentVersionId != null).ToListAsync(ct);
            var meetingIds = records.Select(x => x.SourceRecordId).ToArray();
            if (!await lodgeDb.LodgeMeetings.AsNoTracking().AnyAsync(x => meetingIds.Contains(x.Id) &&
                x.OrganizationId == entity.OrganizationId && x.MeetingDate == request.EffectiveDate &&
                x.CeremonyType == entity.AdmissionType && x.Status == "closed", ct))
                return Results.Conflict(new { message = "Se requiere Tenida cerrada con acta, extracto y Plancha de Autorización vinculados." });

            Member member;
            if (entity.AdmissionType == CeremonyCodes.Type.Affiliation)
            {
                var existing = await coreDb.Members.SingleOrDefaultAsync(x => x.Id == entity.MemberId && x.PersonId == entity.PersonId, ct);
                if (existing is null) return Results.Conflict(new { message = "No coincide la identidad institucional del expediente." });
                member = existing;
            }
            else
            {
                if (!SecretariatOperationsCodes.CurrentDegree.IsValid(entity.Degree ?? "") ||
                    !await coreDb.People.AnyAsync(x => x.Id == entity.PersonId, ct) ||
                    await coreDb.Members.AnyAsync(x => x.PersonId == entity.PersonId, ct))
                    return Results.Conflict(new { message = "La incorporación requiere una identidad externa sin miembro duplicado y grado acreditado." });
                member = new Member { PersonId = entity.PersonId, CurrentDegree = entity.Degree };
                coreDb.Members.Add(member);
            }
            if (await coreDb.Memberships.AnyAsync(x => x.MemberId == member.Id && x.EndDate == null &&
                x.Status == MembershipCodes.MembershipStatus.Active, ct))
                return Results.Conflict(new { message = "Ya existe una pertenencia activa; no se creará otra desde este expediente." });

            var created = new MembershipEntity { Member = member, OrganizationId = entity.OrganizationId,
                MembershipType = "regular", StartDate = request.EffectiveDate, Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = request.EvidenceReference.Trim() };
            coreDb.Memberships.Add(created);
            coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent { Member = member, OrganizationId = entity.OrganizationId,
                EventType = MembershipCodes.InstitutionalStatus.Active, EffectiveDate = request.EffectiveDate,
                EvidenceReference = created.EvidenceReference, Reason = $"Materialización del expediente {entity.Id:D}." });
            var before = new { entity.MemberId, entity.Status, ceremonyStatus = ceremony.Status };
            entity.MemberId = member.Id;
            entity.Status = AdmissionWorkflowCodes.CaseStatus.Resolved;
            ceremony.Member = member;
            ceremony.Status = CeremonyCodes.RequestStatus.Completed;
            admissionsDb.AdmissionDecisions.Add(new AdmissionDecision { AdmissionCaseId = caseId,
                DecisionType = AdmissionWorkflowCodes.DecisionType.MembershipMaterialized, Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = request.EffectiveDate, SourceReference = created.EvidenceReference,
                Notes = JsonSerializer.Serialize(new Receipt(created.Id, member.Id, request.EffectiveDate, created.EvidenceReference)),
                RecordedBySubject = context.User.FindFirst("sub")?.Value ?? "unknown" });
            audit.Add(context, "admission.membership.materialized", nameof(AdmissionCase), caseId.ToString(), entity.OrganizationId,
                AuditResults.Success, new { before, memberId = member.Id, membershipId = created.Id, ceremonyId = ceremony.Id,
                    status = entity.Status, request.EffectiveDate });
            await coreDb.SaveChangesAsync(ct);
            await admissionsDb.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { idempotent = false, membershipId = created.Id, admissionCaseId = entity.Id,
                memberId = member.Id, effectiveDate = created.StartDate });
        }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            return Results.Conflict(new { message = "El expediente cambió durante la operación. Recargue y vuelva a comprobarlo." });
        }
    }

    private static bool IsSerializationConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: "40001" }) return true;
        return false;
    }
}
