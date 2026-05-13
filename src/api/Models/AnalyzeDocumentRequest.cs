namespace ContentUnderstanding.Api.Models;

public sealed class AnalyzeDocumentRequest
{
    /// <summary>User-friendly name for the schema (becomes the analyzer ID after sanitization).</summary>
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>Fields the user wants to extract from the document.</summary>
    public List<UserDefinedField> Fields { get; set; } = [];

    /// <summary>Original file name, used as the multipart part name when forwarding to Azure.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Base-64 encoded file content.</summary>
    public string FileContentBase64 { get; set; } = string.Empty;

    /// <summary>MIME type of the uploaded file (e.g. application/pdf).</summary>
    public string MimeType { get; set; } = "application/octet-stream";
}

public sealed class UserDefinedField
{
    /// <summary>Human-readable field label (e.g. "Invoice Number").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CU-compatible field type: string, number, date, boolean, or array.</summary>
    public string Type { get; set; } = "string";

    /// <summary>Optional description that improves extraction accuracy.</summary>
    public string? Description { get; set; }
}
