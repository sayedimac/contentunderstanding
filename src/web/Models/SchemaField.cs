namespace web.Models;

public sealed class SchemaField
{
    /// <summary>Human-readable field label entered by the user (e.g. "Invoice Number").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CU-compatible type: string, number, date, boolean, or array.</summary>
    public string Type { get; set; } = "string";

    /// <summary>Optional hint that improves extraction accuracy.</summary>
    public string? Description { get; set; }
}
