namespace web.Models;

public sealed class SchemaSubmissionResponse
{
    public string AnalyzerId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Mode { get; set; } = string.Empty;

    public string? RequestUri { get; set; }

    public string? ResponsePayload { get; set; }
}
