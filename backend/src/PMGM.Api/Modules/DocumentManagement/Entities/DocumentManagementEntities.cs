namespace PMGM.Api.Modules.DocumentManagement.Entities;

public sealed class DocumentCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = DocumentManagementCodes.Scope.Order;
    public Guid? OrganizationId { get; set; }
    public string Status { get; set; } = DocumentManagementCodes.CollectionStatus.Active;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBySubject { get; set; } = string.Empty;
}

public sealed class InstitutionalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectionId { get; set; }
    public DocumentCollection Collection { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Classification { get; set; } = DocumentManagementCodes.Classification.Internal;
    public string AccessPolicy { get; set; } = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated;
    public int? MinimumDegreeRequired { get; set; }
    public string Status { get; set; } = DocumentManagementCodes.DocumentStatus.Draft;
    public Guid? PublishedVersionId { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBySubject { get; set; } = string.Empty;
}

public sealed class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public InstitutionalDocument Document { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = DocumentManagementCodes.ProcessingStatus.PendingUpload;
    public string? ScanReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBySubject { get; set; } = string.Empty;
}
