namespace IntegrationPro.Domain.Entities;

public sealed record JobProgressEntry(
    int CurrentStep,
    int TotalSteps,
    string Description,
    DateTime TimestampUtc);
