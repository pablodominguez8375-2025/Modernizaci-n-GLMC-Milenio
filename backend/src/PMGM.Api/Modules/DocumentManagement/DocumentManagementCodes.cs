using System.Text.RegularExpressions;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentManagementCodes
{
    public static class Scope
    {
        public const string Order = "order";
        public const string Organization = "organization";
        public static bool IsValid(string value) => value is Order or Organization;
    }

    public static class CollectionStatus
    {
        public const string Active = "active";
        public const string Retired = "retired";
        public static bool IsValid(string value) => value is Active or Retired;
    }

    public static class DocumentStatus
    {
        public const string Draft = "draft";
        public const string Active = "active";
        public const string Published = "published";
        public const string Retired = "retired";
        public static bool IsValid(string value) => value is Draft or Active or Published or Retired;
    }

    public static class Classification
    {
        public const string Internal = "internal";
        public const string Confidential = "confidential";
        public const string Sensitive = "sensitive";
        public const string Restricted = "restricted";
        public static bool IsValid(string value) => value is Internal or Confidential or Sensitive or Restricted;
    }

    public static class AccessPolicy
    {
        public const string LibraryAuthenticated = "library_authenticated";
        public const string OrganizationAuthenticated = "organization_authenticated";
        public const string ManagementOnly = "management_only";
        public static bool IsValid(string value) => value is LibraryAuthenticated or OrganizationAuthenticated or ManagementOnly;
        public static bool CanPublish(string value) => value is LibraryAuthenticated or OrganizationAuthenticated;
    }

    public static class ProcessingStatus
    {
        public const string PendingUpload = "pending_upload";
        public const string Uploaded = "uploaded";
        public const string Scanning = "scanning";
        public const string Available = "available";
        public const string Rejected = "rejected";
        public static bool IsValid(string value) => value is PendingUpload or Uploaded or Scanning or Available or Rejected;
    }
}

public static partial class DocumentIntegrity
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    public static bool IsValidSha256(string? value)
        => !string.IsNullOrWhiteSpace(value) && Sha256Regex().IsMatch(value.Trim());
}

public static class DocumentObjectKeyFactory
{
    public static string Create(Guid documentId, Guid versionId)
        => $"documents/{documentId:N}/{versionId:N}";
}

public sealed record DocumentVersionTransitionDecision(bool Allowed, string? Error);

public static class DocumentVersionLifecycle
{
    public static DocumentVersionTransitionDecision CanTransition(
        string currentStatus,
        string targetStatus,
        string? sha256,
        string? scanReference)
    {
        if (!DocumentManagementCodes.ProcessingStatus.IsValid(currentStatus) ||
            !DocumentManagementCodes.ProcessingStatus.IsValid(targetStatus))
        {
            return new(false, "El estado documental indicado no es válido.");
        }

        var allowed = currentStatus switch
        {
            DocumentManagementCodes.ProcessingStatus.PendingUpload => targetStatus == DocumentManagementCodes.ProcessingStatus.Uploaded,
            DocumentManagementCodes.ProcessingStatus.Uploaded => targetStatus is DocumentManagementCodes.ProcessingStatus.Scanning or DocumentManagementCodes.ProcessingStatus.Rejected,
            DocumentManagementCodes.ProcessingStatus.Scanning => targetStatus is DocumentManagementCodes.ProcessingStatus.Available or DocumentManagementCodes.ProcessingStatus.Rejected,
            _ => false
        };

        if (!allowed)
            return new(false, $"No se permite avanzar de {currentStatus} a {targetStatus}.");

        if (targetStatus is DocumentManagementCodes.ProcessingStatus.Uploaded or
            DocumentManagementCodes.ProcessingStatus.Scanning or
            DocumentManagementCodes.ProcessingStatus.Available)
        {
            if (!DocumentIntegrity.IsValidSha256(sha256))
                return new(false, "La versión debe tener un SHA-256 válido antes de avanzar en el procesamiento.");
        }

        if (targetStatus == DocumentManagementCodes.ProcessingStatus.Available && string.IsNullOrWhiteSpace(scanReference))
            return new(false, "La versión requiere evidencia de escaneo antes de quedar disponible.");

        return new(true, null);
    }
}

public interface IDocumentObjectStore
{
    Task StoreAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
