using System.Net.Http.Json;
using System.Text.Json;
using web.Models;

namespace web.Services;

public sealed class ContentUnderstandingClient(HttpClient httpClient)
{
    public async Task<SchemaSubmissionResponse> SubmitSchemaAsync(
        string analyzerId,
        string description,
        string baseAnalyzerId,
        string? completionModel,
        bool dynamicFieldSchema,
        string fieldSchemaText,
        CancellationToken cancellationToken = default)
    {
        using var jsonDocument = JsonDocument.Parse(fieldSchemaText);

        var payload = new SchemaSubmissionRequest
        {
            AnalyzerId = analyzerId.Trim(),
            Description = description.Trim(),
            BaseAnalyzerId = baseAnalyzerId.Trim(),
            CompletionModel = string.IsNullOrWhiteSpace(completionModel) ? null : completionModel.Trim(),
            DynamicFieldSchema = dynamicFieldSchema,
            FieldSchema = jsonDocument.RootElement.Clone()
        };

        using var response = await httpClient.PostAsJsonAsync("api/content-understanding/analyzers", payload, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(responseText);
        }

        return JsonSerializer.Deserialize<SchemaSubmissionResponse>(responseText, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The API returned an empty response.");
    }
}
