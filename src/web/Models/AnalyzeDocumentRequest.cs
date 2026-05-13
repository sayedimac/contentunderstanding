namespace web.Models;

public sealed class AnalyzeDocumentRequest
{
    public string SchemaName { get; set; } = string.Empty;

    public List<SchemaField> Fields { get; set; } = [];

    public string FileName { get; set; } = string.Empty;

    public string FileContentBase64 { get; set; } = string.Empty;

    public string MimeType { get; set; } = "application/octet-stream";
}
