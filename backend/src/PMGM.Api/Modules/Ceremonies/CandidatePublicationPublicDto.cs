namespace PMGM.Api.Modules.Ceremonies;

public sealed record CandidatePublicationPublicDto(
    string DisplayName,
    string WorkshopName,
    string? WorkshopNumber,
    DateTimeOffset PublishedFromUtc,
    DateTimeOffset? PublishedUntilUtc,
    int RequiredDays,
    int ElapsedDays,
    DateTimeOffset ComplianceDateUtc,
    string RuleCode,
    string Status,
    string? PhotoUrl = null);
