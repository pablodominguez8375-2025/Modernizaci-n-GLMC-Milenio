using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
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

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lodge = new Organization { Name = $"Taller Archivo {suffix}", Number = $"A{suffix[..3]}", Type = "workshop" };
        await using (var coreDb = new PmgmDbContext(coreOptions))
        {
            await coreDb.Database.MigrateAsync(cancellationToken);
            coreDb.Organizations.Add(lodge);
            await coreDb.SaveChangesAsync(cancellationToken);
        }

        await using var documentDb = new DocumentManagementDbContext(documentOptions);
        await using var archiveDb = new GrandArchiveDbContext(archiveOptions);
        await archiveDb.Database.MigrateAsync(cancellationToken);

        var migrations = (await archiveDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909192100_AddGrandArchive", migrations);

        var orderCollection = new DocumentCollection { Code = $"GA-{suffix}", Name = $"Archivo Orden {suffix}", Scope = DocumentManagementCodes.Scope.Order, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa" };
        var lodgeCollection = new DocumentCollection { Code = $"TL-{suffix}", Name = $"Taller {suffix}", Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = lodge.Id, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa" };
        var decree = Document(orderCollection, "Decreto histórico QA", "decree");
        var workPaper = Document(orderCollection, "Plancha QA", "work_paper");
        var lodgeDocument = Document(lodgeCollection, "Acta de Taller QA", "minute");
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
            lodgeDocument.Id, lodgeVersion.Id, $"GA-TL-{suffix}", GrandArchiveCodes.RecordType.Minute,
            null, null, null, null), actor, cancellationToken));

        var withdrawn = await service.WithdrawAsync(registered.Id, "Retiro controlado para validar trazabilidad archivística.", actor, cancellationToken);
        Assert.Equal(GrandArchiveCodes.Status.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawnAtUtc);
        Assert.NotNull(withdrawn.WithdrawalReason);
        Assert.Equal(3, await documentDb.DocumentVersions.CountAsync(x => x.Id == decreeVersion.Id || x.Id == workPaperVersion.Id || x.Id == lodgeVersion.Id, cancellationToken));
    }

    [Fact]
    public async Task Publication_filters_by_degree_and_blocks_internal_types_and_work_papers()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var cancellationToken = TestContext.Current.CancellationToken;
        var documentOptions = new DbContextOptionsBuilder<DocumentManagementDbContext>().UseNpgsql(connectionString).Options;
        var archiveOptions = new DbContextOptionsBuilder<GrandArchiveDbContext>().UseNpgsql(connectionString).Options;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        await using (var coreDb = new PmgmDbContext(coreOptions)) await coreDb.Database.MigrateAsync(cancellationToken);
        await using var documentDb = new DocumentManagementDbContext(documentOptions);
        await using var archiveDb = new GrandArchiveDbContext(archiveOptions);
        await archiveDb.Database.MigrateAsync(cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var collection = new DocumentCollection { Code = $"GP-{suffix}", Name = $"Publicaciones {suffix}", Scope = DocumentManagementCodes.Scope.Order, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa" };
        var docs = new[]
        {
            Document(collection, "Decreto para todos", "decree"),
            Document(collection, "Resolución desde Compañero", "resolution"),
            Document(collection, "Reglamento solo Maestros", "regulation"),
            Document(collection, "Correspondencia interna", "correspondence"),
            Document(collection, "Plancha de autorización", "plancha_autorizacion")
        };
        documentDb.AddRange(collection); documentDb.AddRange(docs);
        await documentDb.SaveChangesAsync(cancellationToken);
        var versions = docs.Select((d, i) => Version(d, $"doc{i}-{suffix}.pdf")).ToArray();
        documentDb.AddRange(versions);
        await documentDb.SaveChangesAsync(cancellationToken);

        var service = new GrandArchiveService(archiveDb, documentDb, new FakeObjectStore());
        var actor = new ArchiveActor("qa-archivist", "Gran Archivero QA");
        var types = new[] { "decree", "resolution", "regulation", "correspondence", "other" };
        var records = new List<GrandArchiveRecordDto>();
        for (var i = 0; i < 4; i++)
            records.Add(await service.RegisterAsync(new RegisterGrandArchiveCommand(docs[i].Id, versions[i].Id, $"GP-{suffix}-{i}", types[i], new DateOnly(2026, 10, 1), null, null, null), actor, cancellationToken));

        await service.PublishAsync(records[0].Id, 1, actor, cancellationToken);
        await service.PublishAsync(records[1].Id, 2, actor, cancellationToken);
        await service.PublishAsync(records[2].Id, 3, actor, cancellationToken);
        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.PublishAsync(records[3].Id, 1, actor, cancellationToken));
        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.PublishAsync(records[0].Id, 4, actor, cancellationToken));
        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.PublishAsync(records[0].Id, 0, actor, cancellationToken));

        var query = new GrandArchiveQuery(null, null, null, null, suffix, 100);
        Assert.Equal(["Decreto para todos"], (await service.ListPublishedAsync(query, 1, cancellationToken)).Items.Select(x => x.Title).ToArray());
        Assert.Equal(2, (await service.ListPublishedAsync(query, 2, cancellationToken)).Total);
        Assert.Equal(0, (await service.ListPublishedAsync(query with { Search = null, Limit = 1 }, 0, cancellationToken)).Items.Count(x => x.ArchiveCode.Contains(suffix)));
        Assert.Equal(3, (await service.ListPublishedAsync(query, 3, cancellationToken)).Total);

        // Contenido y detalle directos respetan el mismo grado.
        Assert.Null(await service.OpenPublishedContentAsync(records[2].Id, 2, cancellationToken));
        Assert.Null(await service.GetPublishedAsync(records[2].Id, 1, cancellationToken));
        Assert.NotNull(await service.OpenPublishedContentAsync(records[2].Id, 3, cancellationToken));
        Assert.Null(await service.GetPublishedAsync(records[3].Id, 3, cancellationToken));

        // Retirar la publicación la oculta a todos y deja el motivo.
        var unpublished = await service.UnpublishAsync(records[0].Id, "Retiro de prueba por corrección del decreto.", actor, cancellationToken);
        Assert.Null(unpublished.PublishedMinimumDegree);
        Assert.NotNull(unpublished.UnpublishedAtUtc);
        Assert.Null(await service.GetPublishedAsync(records[0].Id, 3, cancellationToken));
        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.UnpublishAsync(records[0].Id, "Segundo retiro inválido sin publicar.", actor, cancellationToken));

        // Un registro retirado del archivo deja de ser visible aunque siga marcado.
        await service.WithdrawAsync(records[1].Id, "Retiro del archivo con publicación vigente.", actor, cancellationToken);
        Assert.Null(await service.GetPublishedAsync(records[1].Id, 3, cancellationToken));
        await Assert.ThrowsAsync<GrandArchivePolicyException>(() => service.PublishAsync(records[1].Id, 1, actor, cancellationToken));
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
