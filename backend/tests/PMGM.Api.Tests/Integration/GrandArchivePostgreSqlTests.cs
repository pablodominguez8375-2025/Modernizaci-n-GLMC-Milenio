using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using PMGM.Api.Modules.GrandArchive;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class GrandArchivePostgreSqlTests
{
    [Fact]
    public async Task Archive_registers_order_document_and_rejects_work_papers_and_lodge_documents()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var cancellationToken = TestContext.Current.CancellationToken;

        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var documentOptions = new DbContextOptionsBuilder<DocumentManagementDbContext>().UseNpgsql(connectionString).Options;
        var archiveOptions = new DbContextOptionsBuilder<GrandArchiveDbContext>().UseNpgsql(connectionString).Options;

        await using (var coreDb = new PmgmDbContext(coreOptions))
            await coreDb.Database.MigrateAsync(cancellationToken);
        await using var documentDb = new DocumentManagementDbContext(documentOptions);
        await using var archiveDb = new GrandArchiveDbContext(archiveOptions);
        await archiveDb.Database.MigrateAsync(cancellationToken);

        var migrations = (await archiveDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909192100_AddGrandArchive", migrations);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var orderCollection = new DocumentCollection { Code = $"GA-{suffix}", Name = $"Archivo Orden {suffix}", Scope = DocumentManagementCodes.Scope.Order, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa" };
        var lodgeCollection = new DocumentCollection { Code = $"TL-{suffix}", Name = $"Taller {suffix}", Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = Guid.NewGuid(), Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa" };
        var decree = Document(orderCollection, "Decreto histórico QA", "decree");
        var workPaper = Document(orderCollection, "Plancha QA", "work_paper");
        var lodgeDocument = Document(lodgeCollection, "Acta de Taller QA", "minutes");
        documentDb.AddRange(orderCollection, lodgeCollection, decree, workPaper, lodgeDocument);
        await documentDb.SaveChangesAsync(cancellationToken);

        var decreeVersion = Version(decree, $"decreto-{suffix}.pdf");
        var workPaperVersion = Version(workPaper, $"plancha-{suffix}.pdf");
        var lodgeVersion = Version(lodgeDocument, $"acta-{suffix}.pdf");
        documentDb.AddRange(decreeVersion, workPaperVersion, lodgeVersion);
        await documentDb.SaveChangesAsync(cancellationToken);

        var service = new GrandArchiveService(archiveDb, documentDb, new FakeObjectStore());
        var actor = new ArchiveActor("qa-grand-archivist", "Gran Archivero QA");
        var registered = await service.RegisterAsync(new RegisterGrandArchiveCommand(
            decree.Id, decreeVersion.Id, $"GA-QA-{suffix}", GrandArchiveCodes.RecordType.Decree,
            new DateOnly(2026, 9, 9), "Gran Secretaría", "2026", "Registro histórico QA"), actor, cancellationToken);

        Assert.Equal(GrandArchiveCodes.Status.Active, registered.Status);
        Assert.Equal(decree.Id, registered.DocumentId);
        Assert.Equal(decreeVersion.Id, registered.DocumentVersionId);

        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.RegisterAsync(new RegisterGrandArchiveCommand(
            workPaper.Id, workPaperVersion.Id, $"GA-WP-{suffix}", GrandArchiveCodes.RecordType.HistoricalRecord,
            null, null, null, null), actor, cancellationToken));

        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.RegisterAsync(new RegisterGrandArchiveCommand(
            lodgeDocument.Id, lodgeVersion.Id, $"GA-TL-{suffix}", GrandArchiveCodes.RecordType.Minutes,
            null, null, null, null), actor, cancellationToken));

        var withdrawn = await service.WithdrawAsync(registered.Id, "Retiro controlado para validar trazabilidad archivística.", actor, cancellationToken);
        Assert.Equal(GrandArchiveCodes.Status.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawnAtUtc);
        Assert.NotNull(withdrawn.WithdrawalReason);
        Assert.Equal(3, await documentDb.DocumentVersions.CountAsync(x => x.Id == decreeVersion.Id || x.Id == workPaperVersion.Id || x.Id == lodgeVersion.Id, cancellationToken));
    }

    private static InstitutionalDocument Document(DocumentCollection collection, string title, string type) => new()
    {
        Collection = collection,
        OrganizationId = collection.OrganizationId,
        Title = title,
        DocumentType = type,
        Classification = DocumentManagementCodes.Classification.Confidential,
        AccessPolicy = DocumentManagementCodes.AccessPolicy.ManagementOnly,
        Status = DocumentManagementCodes.DocumentStatus.Active,
        CreatedBySubject = "qa"
    };

    private static DocumentVersion Version(InstitutionalDocument document, string fileName) => new()
    {
        Document = document,
        VersionNumber = 1,
        OriginalFileName = fileName,
        ContentType = "application/pdf",
        SizeBytes = 1024,
        Sha256 = new string('a', 64),
        ObjectKey = $"qa/{Guid.NewGuid():N}",
        ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available,
        ScanReference = "clamav:qa-clean",
        CreatedBySubject = "qa"
    };

    private sealed class FakeObjectStore : IDocumentObjectStore
    {
        public Task StoreAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
