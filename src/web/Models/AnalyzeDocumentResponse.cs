namespace web.Models;

public sealed class AnalyzeDocumentResponse
{
    public string AnalyzerId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public List<ExtractedField> ExtractedFields { get; set; } = [];

    public string? Message { get; set; }
}

public sealed class ExtractedField
{
    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Type { get; set; }

    public double? Confidence { get; set; }
}
