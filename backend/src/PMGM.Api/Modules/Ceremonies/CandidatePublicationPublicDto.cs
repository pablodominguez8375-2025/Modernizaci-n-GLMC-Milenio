namespace PMGM.Api.Modules.Ceremonies;

public sealed record CandidatePublicationPublicDto(
    Guid Id,
    string DisplayName,
    string WorkshopName,
    string? WorkshopNumber,
    string PhotoUrl,
    DateTimeOffset PublishedFromUtc,
    DateTimeOffset? PublishedUntilUtc,
    int RequiredDays,
    int ElapsedDays,
    DateTimeOffset ComplianceDateUtc,
    string RuleCode,
    string Status);
