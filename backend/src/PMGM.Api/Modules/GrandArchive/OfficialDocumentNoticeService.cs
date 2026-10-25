using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Notifications;

namespace PMGM.Api.Modules.GrandArchive;

/// <summary>
/// Aviso interno (solo dentro de la plataforma) a los Hermanos cuyo grado alcanza el mínimo publicado (#653).
/// Idempotente por documento y destinatario: ampliar la audiencia avisa solo a quienes antes no veían el documento.
/// </summary>
public interface IOfficialDocumentNoticeService
{
    Task<OfficialDocumentNoticeResult> NotifyPublishedAsync(Guid recordId, string archiveCode, string recordType, string title, int minimumDegree, DateTimeOffset cycleStartedAtUtc, CancellationToken cancellationToken);
}

public sealed record OfficialDocumentNoticeResult(int Eligible, int Queued, int Failed);

public sealed class OfficialDocumentNoticeService(PmgmDbContext coreDb, IInstitutionalNotificationService notifications) : IOfficialDocumentNoticeService
{
    private sealed class LinkRow { public Guid MemberId { get; set; } public string Subject { get; set; } = string.Empty; }
    private sealed class DegreeRow { public Guid MemberId { get; set; } public string? Degree { get; set; } public DateOnly EffectiveDate { get; set; } public DateTimeOffset RecordedAtUtc { get; set; } }

    public async Task<OfficialDocumentNoticeResult> NotifyPublishedAsync(
        Guid recordId, string archiveCode, string recordType, string title, int minimumDegree, DateTimeOffset cycleStartedAtUtc, CancellationToken cancellationToken)
    {
        var links = await coreDb.Database.SqlQuery<LinkRow>($"""
            SELECT "MemberId", "Subject" FROM core.member_identity_links WHERE "RevokedAtUtc" IS NULL
            """).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var degreeRows = await coreDb.Database.SqlQuery<DegreeRow>($"""
            SELECT "MemberId", "Degree", "EffectiveDate", "RecordedAtUtc" FROM core.degree_events WHERE "EffectiveDate" <= {today}
            """).ToListAsync(cancellationToken);

        var degreeByMember = degreeRows
            .GroupBy(x => x.MemberId)
            .Select(g => (MemberId: g.Key, Degree: g.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc)
                .Select(x => InstitutionalDegree.TryParse(x.Degree, out var d) ? d : 0).FirstOrDefault(d => d > 0)))
            .ToDictionary(x => x.MemberId, x => x.Degree);

        // Un Hermano con varias cuentas recibe un solo aviso por documento (las claves son por cuenta; se evita el duplicado por Hermano).
        var recipients = links
            .Where(x => degreeByMember.TryGetValue(x.MemberId, out var degree) && degree >= minimumDegree)
            .GroupBy(x => x.MemberId)
            .Select(g => g.OrderBy(x => x.Subject, StringComparer.Ordinal).First())
            .ToList();

        var queued = 0; var failed = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                var result = await notifications.QueueAsync(new QueueNotificationCommand(
                    NotificationCodes.Template.OfficialDocumentPublished, null,
                    NotificationCodes.Type.OfficialDocumentPublished,
                    recipient.Subject, null,
                    new[] { NotificationCodes.Channel.Internal },
                    new Dictionary<string, string?> { ["type"] = TypeLabel(recordType), ["title"] = title },
                    $"official-document-published:{recordId:N}:{recipient.MemberId:N}:{cycleStartedAtUtc.UtcTicks}",
                    recordId.ToString("N"), recordId.ToString("N"),
                    "/official-documents", false, null,
                    RelatedResourceType: "grand_archive_record",
                    RelatedResourceId: recordId.ToString("N")), cancellationToken);
                if (result.Created) queued++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or ArgumentException)
            {
                failed++;
            }
        }
        return new OfficialDocumentNoticeResult(recipients.Count, queued, failed);
    }

    private static string TypeLabel(string recordType) => recordType switch
    {
        GrandArchiveCodes.RecordType.Decree => "Decreto",
        GrandArchiveCodes.RecordType.Communication => "Comunicado",
        GrandArchiveCodes.RecordType.Minute => "Acta",
        GrandArchiveCodes.RecordType.Resolution => "Resolución",
        GrandArchiveCodes.RecordType.Regulation => "Reglamento",
        _ => "Documento"
    };
}
