namespace ContentUnderstanding.Api.Models;

public sealed class SchemaSubmissionResponse
{
    public required string AnalyzerId { get; init; }

    public required string Message { get; init; }

    public required string Mode { get; init; }

    public string? RequestUri { get; init; }

    public string? ResponsePayload { get; init; }
}
