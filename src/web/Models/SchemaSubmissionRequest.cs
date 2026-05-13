using System.Text.Json;

namespace web.Models;

public sealed class SchemaSubmissionRequest
{
    public string AnalyzerId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string BaseAnalyzerId { get; set; } = "prebuilt-document";

    public string? CompletionModel { get; set; }

    public bool DynamicFieldSchema { get; set; }

    public JsonElement FieldSchema { get; set; }
}
