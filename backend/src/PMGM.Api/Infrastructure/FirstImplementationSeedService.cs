using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using PMGM.Api.Modules.GrandArchive;
using PMGM.Api.Modules.GrandArchive.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Infrastructure;

/// <summary>
/// Datos exclusivamente ficticios para el paquete local/QA de primera implementación.
/// Nunca se ejecuta en Production y no reemplaza los flujos reales de ingreso documental.
/// </summary>
public sealed class FirstImplementationSeedService(
    PmgmDbContext coreDb,
    GrandSecretariatDbContext secretariatDb,
    LodgeManagementDbContext lodgeDb,
    DocumentManagementDbContext documentDb,
    GrandArchiveDbContext archiveDb,
    IDocumentObjectStore objectStore,
    ILogger<FirstImplementationSeedService> logger)
{
    public static readonly Guid GrandLodgeId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid Lodge1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Lodge23Id = Guid.Parse("23232323-2323-2323-2323-232323232323");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCoreAsync(cancellationToken);
        await SeedSecretariatAsync(cancellationToken);
        await SeedLodgeAsync(cancellationToken);
        await SeedDocumentsAndArchiveAsync(cancellationToken);
        logger.LogInformation("PMGM first-implementation demo data verified. Dataset=fictional-v1");
    }

    private async Task SeedCoreAsync(CancellationToken cancellationToken)
    {
        if (await coreDb.Organizations.AnyAsync(x => x.Id == GrandLodgeId, cancellationToken)) return;

        var grandLodge = new Organization { Id = GrandLodgeId, Name = "Gran Logia Mixta — QA", Number = null, Type = "grand_lodge" };
        var lodge1 = new Organization { Id = Lodge1Id, Name = "Taller Demostrativo Nº 1", Number = "1", Type = "workshop", ParentOrganizationId = GrandLodgeId };
        var lodge23 = new Organization { Id = Lodge23Id, Name = "Taller Demostrativo Nº 23", Number = "23", Type = "workshop", ParentOrganizationId = GrandLodgeId };

        var p1 = new Person { Id = Guid.Parse("10101010-1010-1010-1010-101010101010"), FirstNames = "Hermana", LastNames = "Demostrativa Uno", Email = "persona1@pmgm.invalid" };
        var p2 = new Person { Id = Guid.Parse("30303030-3030-3030-3030-303030303030"), FirstNames = "Hermano", LastNames = "Demostrativo Dos", Email = "persona2@pmgm.invalid" };
        var p3 = new Person { Id = Guid.Parse("50505050-5050-5050-5050-505050505050"), FirstNames = "Hermana", LastNames = "Histórica QA", Email = "persona3@pmgm.invalid" };
        var candidate = new Person { Id = Guid.Parse("70707070-7070-7070-7070-707070707070"), FirstNames = "Persona", LastNames = "Insinuada Demostrativa", Email = "insinuada@pmgm.invalid" };

        var m1 = new Member { Id = Guid.Parse("20202020-2020-2020-2020-202020202020"), Person = p1, InstitutionalNumber = "GLM-QA-0230" };
        var m2 = new Member { Id = Guid.Parse("40404040-4040-4040-4040-404040404040"), Person = p2, InstitutionalNumber = "GLM-QA-0404" };
        var m3 = new Member { Id = Guid.Parse("60606060-6060-6060-6060-606060606060"), Person = p3, InstitutionalNumber = "GLM-QA-0606" };

        var oldMembership = new Membership
        {
            Id = Guid.Parse("81818181-8181-8181-8181-818181818181"), Member = m1, Organization = lodge1,
            MembershipType = "regular", StartDate = new DateOnly(2018, 3, 10), EndDate = new DateOnly(2024, 12, 31),
            Status = MembershipCodes.MembershipStatus.Transferred, EndReason = "Traslado demostrativo", EvidenceReference = "ACTA-QA-TRASLADO-001"
        };
        var currentMembership1 = new Membership
        {
            Id = Guid.Parse("82828282-8282-8282-8282-828282828282"), Member = m1, Organization = lodge23,
            MembershipType = "regular", StartDate = new DateOnly(2025, 1, 1), Status = MembershipCodes.MembershipStatus.Active,
            EvidenceReference = "ACTA-QA-INGRESO-023"
        };
        var currentMembership2 = new Membership
        {
            Id = Guid.Parse("83838383-8383-8383-8383-838383838383"), Member = m2, Organization = lodge23,
            MembershipType = "regular", StartDate = new DateOnly(2021, 5, 15), Status = MembershipCodes.MembershipStatus.Active
        };
        var currentMembership3 = new Membership
        {
            Id = Guid.Parse("84848484-8484-8484-8484-848484848484"), Member = m3, Organization = lodge1,
            MembershipType = "regular", StartDate = new DateOnly(1999, 7, 1), Status = MembershipCodes.MembershipStatus.Active
        };

        coreDb.AddRange(grandLodge, lodge1, lodge23, p1, p2, p3, candidate, m1, m2, m3,
            oldMembership, currentMembership1, currentMembership2, currentMembership3);

        coreDb.DegreeEvents.AddRange(
            new DegreeEvent { Member = m1, Organization = lodge1, Degree = "apprentice", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = new DateOnly(2018, 3, 10), EvidenceReference = "ACTA-QA-I-001" },
            new DegreeEvent { Member = m1, Organization = lodge1, Degree = "fellowcraft", EventType = MembershipCodes.DegreeEvent.WageIncrease, EffectiveDate = new DateOnly(2019, 4, 20), EvidenceReference = "ACTA-QA-A-001" },
            new DegreeEvent { Member = m1, Organization = lodge1, Degree = "master", EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2020, 6, 12), EvidenceReference = "ACTA-QA-E-001" },
            new DegreeEvent { Member = m2, Organization = lodge23, Degree = "apprentice", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = new DateOnly(2021, 5, 15) },
            new DegreeEvent { Member = m2, Organization = lodge23, Degree = "master", EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2022, 5, 1) },
            new DegreeEvent { Member = m2, Organization = lodge23, Degree = "fellowcraft", EventType = MembershipCodes.DegreeEvent.WageIncrease, EffectiveDate = new DateOnly(2022, 8, 1) },
            new DegreeEvent { Member = m3, Organization = lodge1, Degree = "apprentice", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = new DateOnly(1999, 7, 1) },
            new DegreeEvent { Member = m3, Organization = lodge1, Degree = "fellowcraft", EventType = MembershipCodes.DegreeEvent.WageIncrease, EffectiveDate = new DateOnly(2000, 8, 1) },
            new DegreeEvent { Member = m3, Organization = lodge1, Degree = "master", EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2001, 9, 1) });

        coreDb.OfficeAssignments.Add(new OfficeAssignment
        {
            Member = m1, Organization = lodge23, OfficeType = "secretary", Period = "2026",
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), EvidenceReference = "ELECCION-QA-2026"
        });

        coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
        {
            Member = m1, Organization = lodge23, EventType = MembershipCodes.InstitutionalStatus.WorkshopTransfer,
            EffectiveDate = new DateOnly(2025, 1, 1), Reason = "Traslado demostrativo", EvidenceReference = "ACTA-QA-TRASLADO-001"
        });

        var asOf = new DateOnly(2026, 9, 9);
        coreDb.FinancialRegularitySnapshots.AddRange(
            new FinancialRegularitySnapshot { Organization = lodge1, Scope = TreasuryCodes.RegularityScope.Organization, Status = TreasuryCodes.RegularityStatus.UpToDate, AsOfDate = asOf, SourceReference = "TES-QA-001" },
            new FinancialRegularitySnapshot { Organization = lodge23, Scope = TreasuryCodes.RegularityScope.Organization, Status = TreasuryCodes.RegularityStatus.UpToDate, AsOfDate = asOf, SourceReference = "TES-QA-023" },
            new FinancialRegularitySnapshot { Organization = lodge23, Member = m1, Scope = TreasuryCodes.RegularityScope.Organization, Status = TreasuryCodes.RegularityStatus.UpToDate, AsOfDate = asOf, SourceReference = "TES-QA-M0230" },
            new FinancialRegularitySnapshot { Organization = lodge23, Member = m2, Scope = TreasuryCodes.RegularityScope.Organization, Status = TreasuryCodes.RegularityStatus.Delinquent, AsOfDate = asOf, SourceReference = "TES-QA-M0404" });

        coreDb.HospitalariaRegularitySnapshots.AddRange(
            new HospitalariaRegularitySnapshot { Organization = lodge1, Status = HospitalariaCodes.RegularityStatus.UpToDate, AsOfDate = asOf, SourceReference = "HOSP-QA-001" },
            new HospitalariaRegularitySnapshot { Organization = lodge23, Status = HospitalariaCodes.RegularityStatus.UpToDate, AsOfDate = asOf, SourceReference = "HOSP-QA-023" });

        var initiation = new CeremonyRequest
        {
            Id = Guid.Parse("91919191-9191-9191-9191-919191919191"), Organization = lodge23,
            CeremonyType = CeremonyCodes.Type.Initiation, CandidatePerson = candidate,
            ProposedDate = new DateOnly(2026, 10, 3), Status = CeremonyCodes.RequestStatus.UnderReview,
            Notes = "Solicitud ficticia para primera implementación."
        };
        var wageIncrease = new CeremonyRequest
        {
            Id = Guid.Parse("92929292-9292-9292-9292-929292929292"), Organization = lodge23,
            CeremonyType = CeremonyCodes.Type.WageIncrease, Member = m1,
            ProposedDate = new DateOnly(2026, 9, 25), Status = CeremonyCodes.RequestStatus.Eligible
        };
        coreDb.CeremonyRequests.AddRange(initiation, wageIncrease);
        coreDb.CeremonyValidations.AddRange(
            Validation(wageIncrease, CeremonyCodes.ValidationType.InternalAffairs, CeremonyCodes.ValidationStatus.Approved, asOf),
            Validation(wageIncrease, CeremonyCodes.ValidationType.Treasury, CeremonyCodes.ValidationStatus.Approved, asOf),
            Validation(wageIncrease, CeremonyCodes.ValidationType.Hospitalaria, CeremonyCodes.ValidationStatus.Approved, asOf),
            Validation(initiation, CeremonyCodes.ValidationType.InternalAffairs, CeremonyCodes.ValidationStatus.Approved, asOf),
            Validation(initiation, CeremonyCodes.ValidationType.Treasury, CeremonyCodes.ValidationStatus.Approved, asOf),
            Validation(initiation, CeremonyCodes.ValidationType.Hospitalaria, CeremonyCodes.ValidationStatus.Approved, asOf));
        coreDb.CandidatePublications.Add(new CandidatePublication
        {
            CeremonyRequest = initiation, Person = candidate, Organization = lodge23,
            PublishedFromUtc = new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero),
            RequiredDays = 20, RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        });
        coreDb.InstitutionalRuleSettings.Add(new InstitutionalRuleSetting
        {
            Code = CeremonyCodes.Rules.InitiationPublicationMinimumDays, Value = "20", EffectiveFrom = new DateOnly(2026, 1, 1),
            Status = "active", SourceReference = "CONFIG-QA-001"
        });

        await coreDb.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSecretariatAsync(CancellationToken cancellationToken)
    {
        var spaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        if (await secretariatDb.InstitutionalSpaces.AnyAsync(x => x.Id == spaceId, cancellationToken)) return;

        var space = new InstitutionalSpace
        {
            Id = spaceId, Code = "TEMP-QA-01", Name = "Templo Principal QA", SpaceType = GrandSecretariatCodes.SpaceType.Temple,
            Location = "Sede institucional demostrativa", Capacity = 80, Status = GrandSecretariatCodes.SpaceStatus.Active
        };
        var reservation = new InstitutionalSpaceReservation
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Space = space, OrganizationId = Lodge23Id,
            CeremonyRequestId = Guid.Parse("92929292-9292-9292-9292-929292929292"), Purpose = "Aumento de salario — QA",
            StartsAtUtc = new DateTimeOffset(2026, 9, 25, 22, 0, 0, TimeSpan.Zero),
            EndsAtUtc = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero), Status = GrandSecretariatCodes.ReservationStatus.Reserved
        };
        var decree = new SecretariatDocument
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), DocumentType = GrandSecretariatCodes.DocumentType.Decree,
            DocumentCode = "DEC-QA-001-2026", Title = "Decreto demostrativo de primera implementación",
            Content = "Documento ficticio generado para validar el flujo de Gran Secretaría.", Status = GrandSecretariatCodes.DocumentStatus.Issued,
            IssuedAtUtc = new DateTimeOffset(2026, 9, 9, 15, 0, 0, TimeSpan.Zero), IssuedBySubject = "qa.admin"
        };
        secretariatDb.AddRange(space, reservation, decree);
        await secretariatDb.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedLodgeAsync(CancellationToken cancellationToken)
    {
        var meetingId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        if (await lodgeDb.LodgeMeetings.AnyAsync(x => x.Id == meetingId, cancellationToken)) return;

        var meeting = new LodgeMeeting
        {
            Id = meetingId, OrganizationId = Lodge23Id, MeetingDate = new DateOnly(2026, 9, 12),
            MeetingType = LodgeManagementCodes.MeetingType.Regular, Grade = LodgeManagementCodes.Grade.All,
            Title = "Tenida Regular — Primera implementación", Status = LodgeManagementCodes.MeetingStatus.Scheduled
        };
        var minute = new LodgeMinute
        {
            Id = Guid.Parse("dededede-dede-dede-dede-dededededede"), Meeting = meeting, Version = 1,
            Content = "Borrador de acta ficticia para validación del sistema.", Status = LodgeManagementCodes.MinuteStatus.Draft,
            CreatedBySubject = "qa.taller23"
        };
        var instruction = new LodgeInstructionSession
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), OrganizationId = Lodge23Id,
            InstructionDate = new DateOnly(2026, 9, 16), Grade = LodgeManagementCodes.Grade.Fellowcraft,
            Topic = "Docencia de Compañeros — QA", ResponsibleOffice = LodgeManagementCodes.InstructionOffice.FirstWarden,
            InstructorMemberId = Guid.Parse("20202020-2020-2020-2020-202020202020"), Status = LodgeManagementCodes.InstructionStatus.Held,
            CreatedBySubject = "qa.taller23"
        };
        lodgeDb.AddRange(meeting, minute, instruction);
        lodgeDb.LodgeAttendanceRecords.AddRange(
            new LodgeAttendanceRecord { Meeting = meeting, MemberId = Guid.Parse("20202020-2020-2020-2020-202020202020"), Status = LodgeManagementCodes.AttendanceStatus.Present },
            new LodgeAttendanceRecord { Meeting = meeting, MemberId = Guid.Parse("40404040-4040-4040-4040-404040404040"), Status = LodgeManagementCodes.AttendanceStatus.Excused, ExcuseReason = "Excusa demostrativa" });
        lodgeDb.LodgeInstructionAttendanceRecords.Add(new LodgeInstructionAttendanceRecord
        {
            InstructionSession = instruction, MemberId = Guid.Parse("20202020-2020-2020-2020-202020202020"),
            Status = LodgeManagementCodes.InstructionAttendanceStatus.Present, RecordedBySubject = "qa.taller23"
        });
        await lodgeDb.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDocumentsAndArchiveAsync(CancellationToken cancellationToken)
    {
        var collectionId = Guid.Parse("fafafafa-fafa-fafa-fafa-fafafafafafa");
        if (!await documentDb.DocumentCollections.AnyAsync(x => x.Id == collectionId, cancellationToken))
        {
            var collection = new DocumentCollection
            {
                Id = collectionId, Code = "QA-ORDEN", Name = "Documentos institucionales QA",
                Description = "Colección ficticia de primera implementación", Scope = DocumentManagementCodes.Scope.Order,
                Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = "qa.admin"
            };
            var libraryDoc = NewDocument(Guid.Parse("f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1"), collection, "Historia institucional — documento demostrativo", "historical_publication", DocumentManagementCodes.AccessPolicy.LibraryAuthenticated);
            var workPaper = NewDocument(Guid.Parse("f2f2f2f2-f2f2-f2f2-f2f2-f2f2f2f2f2f2"), collection, "Plancha de trabajo demostrativa autorizada", "work_paper", DocumentManagementCodes.AccessPolicy.LibraryAuthenticated);
            var historical = NewDocument(Guid.Parse("f3f3f3f3-f3f3-f3f3-f3f3-f3f3f3f3f3f3"), collection, "Acta histórica — Gran Archivero QA", "historical_record", DocumentManagementCodes.AccessPolicy.ManagementOnly);
            documentDb.AddRange(collection, libraryDoc, workPaper, historical);
            await documentDb.SaveChangesAsync(cancellationToken);

            var libraryVersion = await AddGeneratedVersionAsync(libraryDoc, Guid.Parse("f4f4f4f4-f4f4-f4f4-f4f4-f4f4f4f4f4f4"), "historia-institucional-qa.txt", "Contenido institucional ficticio de Biblioteca Virtual.", cancellationToken);
            var planchaVersion = await AddGeneratedVersionAsync(workPaper, Guid.Parse("f5f5f5f5-f5f5-f5f5-f5f5-f5f5f5f5f5f5"), "plancha-trabajo-qa.txt", "Plancha ficticia autorizada exclusivamente para demostrar Biblioteca Virtual.", cancellationToken);
            var archiveVersion = await AddGeneratedVersionAsync(historical, Guid.Parse("f6f6f6f6-f6f6-f6f6-f6f6-f6f6f6f6f6f6"), "acta-historica-qa.txt", "Acta histórica ficticia para demostrar Gran Archivero.", cancellationToken);

            var publishedAt = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
            libraryDoc.Status = DocumentManagementCodes.DocumentStatus.Published; libraryDoc.PublishedVersionId = libraryVersion.Id; libraryDoc.PublishedAtUtc = publishedAt;
            workPaper.Status = DocumentManagementCodes.DocumentStatus.Published; workPaper.PublishedVersionId = planchaVersion.Id; workPaper.PublishedAtUtc = publishedAt;
            historical.Status = DocumentManagementCodes.DocumentStatus.Active;
            await documentDb.SaveChangesAsync(cancellationToken);

            if (!await archiveDb.GrandArchiveRecords.AnyAsync(x => x.DocumentVersionId == archiveVersion.Id, cancellationToken))
            {
                archiveDb.GrandArchiveRecords.Add(new GrandArchiveRecord
                {
                    Id = Guid.Parse("f7f7f7f7-f7f7-f7f7-f7f7-f7f7f7f7f7f7"), ArchiveCode = "GA-QA-1986-001",
                    DocumentId = historical.Id, DocumentVersionId = archiveVersion.Id, RecordType = GrandArchiveCodes.RecordType.HistoricalRecord,
                    DocumentDate = new DateOnly(1986, 7, 12), OriginatingBody = "Gran Logia Mixta de Chile — referencia QA",
                    HistoricalPeriod = "1980–1989", Description = "Registro histórico ficticio de primera implementación.",
                    Status = GrandArchiveCodes.Status.Active, CreatedBySubject = "qa.admin", CreatedByDisplayName = "Administración QA Milenio"
                });
                await archiveDb.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<DocumentVersion> AddGeneratedVersionAsync(
        InstitutionalDocument document, Guid versionId, string fileName, string content, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var objectKey = DocumentObjectKeyFactory.Create(document.Id, versionId);
        if (!await objectStore.ExistsAsync(objectKey, cancellationToken))
        {
            await using var stream = new MemoryStream(bytes, writable: false);
            await objectStore.StoreAsync(objectKey, stream, "text/plain; charset=utf-8", cancellationToken);
        }
        var version = new DocumentVersion
        {
            Id = versionId, Document = document, VersionNumber = 1, OriginalFileName = fileName,
            ContentType = "text/plain; charset=utf-8", SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), ObjectKey = objectKey,
            ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available,
            ScanReference = "qa-seed:generated-content", CreatedBySubject = "qa.admin"
        };
        documentDb.DocumentVersions.Add(version);
        await documentDb.SaveChangesAsync(cancellationToken);
        return version;
    }

    private static InstitutionalDocument NewDocument(Guid id, DocumentCollection collection, string title, string type, string accessPolicy)
        => new()
        {
            Id = id, Collection = collection, Title = title, DocumentType = type,
            Classification = DocumentManagementCodes.Classification.Confidential, AccessPolicy = accessPolicy,
            MinimumDegreeRequired = null, Status = DocumentManagementCodes.DocumentStatus.Draft, CreatedBySubject = "qa.admin"
        };

    private static CeremonyValidation Validation(CeremonyRequest request, string type, string status, DateOnly asOf)
        => new() { CeremonyRequest = request, ValidationType = type, Status = status, AsOfDate = asOf, SourceReference = "QA-SEED" };
}
