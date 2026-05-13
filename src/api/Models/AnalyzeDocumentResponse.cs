namespace ContentUnderstanding.Api.Models;

public sealed class AnalyzeDocumentResponse
{
    public required string AnalyzerId { get; init; }

    public required string Status { get; init; }

    public List<ExtractedField> ExtractedFields { get; init; } = [];

    public string? Message { get; init; }
}

public sealed class ExtractedField
{
    /// <summary>Human-readable field label as defined by the user.</summary>
    public required string Name { get; init; }

    /// <summary>Extracted value formatted as a string for display.</summary>
    public string? Value { get; init; }

    /// <summary>CU field type (string, number, date, boolean, array).</summary>
    public string? Type { get; init; }

    /// <summary>Extraction confidence score between 0 and 1.</summary>
    public double? Confidence { get; init; }
}
