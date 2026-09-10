namespace PMGM.Api.Modules.CandidateIntake.Entities;

/// <summary>
/// Expediente privado de insinuación. Los datos de esta entidad nunca forman parte
/// de la proyección transversal de publicaciones para Hermanos.
/// </summary>
public sealed class CandidateIntakeProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PersonId { get; set; }
    public string? PaternalSurname { get; set; }
    public string? MaternalSurname { get; set; }
    public string? RutOrInstitutionalId { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Nationality { get; set; }
    public string? CivilStatus { get; set; }
    public string? Occupation { get; set; }
    public string? City { get; set; }
    public string? Orient { get; set; }
    public string PresentersJson { get; set; } = "[]";
    public DateOnly InsinuationDate { get; set; }
    public Guid? PhotoVersionId { get; set; }
    public string? InterviewSummary { get; set; }
    public string? InternalObservations { get; set; }
    public required string SubmittedBySubject { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string UpdatedBySubject { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
