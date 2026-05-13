using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;
using ContentUnderstanding.Api.Models;
using Microsoft.Extensions.Configuration;

namespace ContentUnderstanding.Api.Services;

public sealed class ContentUnderstandingService(
    HttpClient httpClient,
    TokenCredential credential,
    IConfiguration configuration)
{
    private const string DefaultApiVersion = "2025-11-01";
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    public async Task<SchemaSubmissionResponse> UpsertAnalyzerAsync(
        SchemaSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var requestPayload = BuildRequestPayload(request);
        var previewPayload = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });

        var endpoint = NormalizeEndpoint(configuration["CONTENT_UNDERSTANDING_ENDPOINT"]);
        var apiVersion = configuration["CONTENT_UNDERSTANDING_API_VERSION"] ?? DefaultApiVersion;

        if (endpoint is null)
        {
            return new SchemaSubmissionResponse
            {
                AnalyzerId = request.AnalyzerId,
                Message = "Schema validated locally. Set CONTENT_UNDERSTANDING_ENDPOINT to submit it to Azure Content Understanding.",
                Mode = "Preview",
                ResponsePayload = previewPayload
            };
        }

        var requestUri = new Uri(endpoint, $"contentunderstanding/analyzers/{Uri.EscapeDataString(request.AnalyzerId)}?api-version={Uri.EscapeDataString(apiVersion)}");
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);

        using var message = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(requestPayload)
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure Content Understanding returned {(int)response.StatusCode} ({response.ReasonPhrase}). {responseText}",
                null,
                response.StatusCode);
        }

        return new SchemaSubmissionResponse
        {
            AnalyzerId = request.AnalyzerId,
            Message = "Analyzer schema submitted to Azure Content Understanding.",
            Mode = "Submitted",
            RequestUri = requestUri.ToString(),
            ResponsePayload = responseText
        };
    }

    private static Dictionary<string, object?> BuildRequestPayload(SchemaSubmissionRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["description"] = string.IsNullOrWhiteSpace(request.Description)
                ? $"Analyzer created from the Content Understanding schema editor on {DateTimeOffset.UtcNow:O}."
                : request.Description,
            ["baseAnalyzerId"] = string.IsNullOrWhiteSpace(request.BaseAnalyzerId)
                ? "prebuilt-document"
                : request.BaseAnalyzerId,
            ["fieldSchema"] = request.FieldSchema,
            ["dynamicFieldSchema"] = request.DynamicFieldSchema
        };

        if (!string.IsNullOrWhiteSpace(request.CompletionModel))
        {
            payload["models"] = new Dictionary<string, string>
            {
                ["completion"] = request.CompletionModel
            };
        }

        return payload;
    }

    private static Uri? NormalizeEndpoint(string? rawEndpoint)
    {
        if (string.IsNullOrWhiteSpace(rawEndpoint))
        {
            return null;
        }

        var normalized = rawEndpoint.Trim().TrimEnd('/');

        if (normalized.EndsWith("/contentunderstanding", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"/contentunderstanding".Length];
        }

        return new Uri($"{normalized}/", UriKind.Absolute);
    }

    public async Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(
        AnalyzeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var analyzerId = SanitizeAnalyzerId(request.SchemaName);

        // Build and upsert the analyzer schema.
        var fieldSchemaJson = BuildUserFieldSchema(request.Fields);
        using var fieldSchemaDoc = JsonDocument.Parse(fieldSchemaJson);

        var schemaRequest = new SchemaSubmissionRequest
        {
            AnalyzerId = analyzerId,
            Description = $"User-defined schema: {request.SchemaName}",
            BaseAnalyzerId = "prebuilt-document",
            DynamicFieldSchema = false,
            FieldSchema = fieldSchemaDoc.RootElement.Clone()
        };

        await UpsertAnalyzerAsync(schemaRequest, cancellationToken);

        var endpoint = NormalizeEndpoint(configuration["CONTENT_UNDERSTANDING_ENDPOINT"]);

        if (endpoint is null)
        {
            return new AnalyzeDocumentResponse
            {
                AnalyzerId = analyzerId,
                Status = "Preview",
                Message = "Schema saved locally. Set CONTENT_UNDERSTANDING_ENDPOINT to analyze documents with Azure Content Understanding.",
                ExtractedFields = request.Fields
                    .Select(f => new ExtractedField
                    {
                        Name = f.Name,
                        Value = "(requires endpoint configuration)",
                        Type = f.Type
                    })
                    .ToList()
            };
        }

        var apiVersion = configuration["CONTENT_UNDERSTANDING_API_VERSION"] ?? DefaultApiVersion;
        var analyzeUri = new Uri(
            endpoint,
            $"contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerId)}:analyze?api-version={Uri.EscapeDataString(apiVersion)}");

        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);

        byte[] fileBytes = Convert.FromBase64String(request.FileContentBase64);

        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.MimeType);
        multipart.Add(fileContent, "file", request.FileName);

        using var analyzeMessage = new HttpRequestMessage(HttpMethod.Post, analyzeUri)
        {
            Content = multipart
        };
        analyzeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        using var analyzeResponse = await httpClient.SendAsync(analyzeMessage, cancellationToken);
        var analyzeBody = await analyzeResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!analyzeResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure Content Understanding returned {(int)analyzeResponse.StatusCode} ({analyzeResponse.ReasonPhrase}) for analyze request. {analyzeBody}",
                null,
                analyzeResponse.StatusCode);
        }

        // Retrieve the operation-location URL for polling.
        var operationLocation = analyzeResponse.Headers.TryGetValues("operation-location", out var vals)
            ? vals.FirstOrDefault()
            : null;

        if (string.IsNullOrEmpty(operationLocation))
        {
            throw new InvalidOperationException("Azure Content Understanding did not return an operation-location header.");
        }

        // Poll for results (up to ~60 s).
        var resultJson = await PollForResultsAsync(operationLocation, accessToken.Token, cancellationToken);

        return MapToResponse(analyzerId, request.Fields, resultJson);
    }

    private async Task<JsonDocument> PollForResultsAsync(
        string operationLocation,
        string accessToken,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;
        const int delayMs = 2000;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(delayMs, cancellationToken);

            using var pollMessage = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            pollMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var pollResponse = await httpClient.SendAsync(pollMessage, cancellationToken);
            var pollBody = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!pollResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Polling failed with status {(int)pollResponse.StatusCode}. {pollBody}",
                    null,
                    pollResponse.StatusCode);
            }

            var doc = JsonDocument.Parse(pollBody);
            var status = doc.RootElement.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return doc;
            }

            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Document analysis ended with status '{status}'. Response: {pollBody}");
            }

            // status is "NotStarted" or "Running" — keep polling.
            doc.Dispose();
        }

        throw new TimeoutException("Document analysis did not complete within the expected time.");
    }

    private static AnalyzeDocumentResponse MapToResponse(
        string analyzerId,
        List<UserDefinedField> fields,
        JsonDocument resultDoc)
    {
        // Build a lookup from camelCase key → friendly name.
        var keyToName = fields.ToDictionary(
            f => ToCamelCaseKey(f.Name),
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var extractedFields = new List<ExtractedField>();

        if (resultDoc.RootElement.TryGetProperty("result", out var result)
            && result.TryGetProperty("contents", out var contents)
            && contents.ValueKind == JsonValueKind.Array)
        {
            foreach (var content in contents.EnumerateArray())
            {
                if (!content.TryGetProperty("fields", out var fieldsEl))
                    continue;

                foreach (var field in fieldsEl.EnumerateObject())
                {
                    if (!keyToName.TryGetValue(field.Name, out var userField))
                        continue;

                    var value = ExtractFieldValue(field.Value);
                    var confidence = field.Value.TryGetProperty("confidence", out var confProp)
                        ? confProp.GetDouble()
                        : (double?)null;

                    extractedFields.Add(new ExtractedField
                    {
                        Name = userField.Name,
                        Value = value,
                        Type = userField.Type,
                        Confidence = confidence
                    });
                }

                // Only use the first content block.
                break;
            }
        }

        // For any fields not returned by the service, include them with a null value.
        var returnedNames = extractedFields.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.Where(f => !returnedNames.Contains(f.Name)))
        {
            extractedFields.Add(new ExtractedField
            {
                Name = field.Name,
                Value = null,
                Type = field.Type
            });
        }

        return new AnalyzeDocumentResponse
        {
            AnalyzerId = analyzerId,
            Status = "Succeeded",
            ExtractedFields = extractedFields
        };
    }

    private static string ExtractFieldValue(JsonElement fieldElement)
    {
        if (fieldElement.TryGetProperty("valueString", out var vs))
            return vs.GetString() ?? string.Empty;

        if (fieldElement.TryGetProperty("valueNumber", out var vn))
            return vn.ToString();

        if (fieldElement.TryGetProperty("valueDate", out var vd))
            return vd.GetString() ?? string.Empty;

        if (fieldElement.TryGetProperty("valueBoolean", out var vb))
            return vb.GetBoolean() ? "Yes" : "No";

        if (fieldElement.TryGetProperty("valueArray", out var va) && va.ValueKind == JsonValueKind.Array)
        {
            var items = va.EnumerateArray()
                .Select(item => item.TryGetProperty("valueString", out var s) ? s.GetString() : item.ToString())
                .Where(s => s is not null);
            return string.Join(", ", items);
        }

        if (fieldElement.TryGetProperty("content", out var c))
            return c.GetString() ?? string.Empty;

        return string.Empty;
    }

    private static string BuildUserFieldSchema(List<UserDefinedField> fields)
    {
        var obj = new JsonObject();

        foreach (var field in fields)
        {
            var key = ToCamelCaseKey(field.Name);
            var fieldObj = new JsonObject { ["type"] = field.Type };

            if (!string.IsNullOrWhiteSpace(field.Description))
                fieldObj["description"] = field.Description;
            else
                fieldObj["description"] = field.Name;

            if (string.Equals(field.Type, "array", StringComparison.OrdinalIgnoreCase))
                fieldObj["items"] = new JsonObject { ["type"] = "string" };

            obj[key] = fieldObj;
        }

        return obj.ToJsonString();
    }

    internal static string ToCamelCaseKey(string name)
    {
        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            var word = new string(words[i].Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(word)) continue;

            if (i == 0)
                sb.Append(char.ToLowerInvariant(word[0]));
            else
                sb.Append(char.ToUpperInvariant(word[0]));

            if (word.Length > 1)
                sb.Append(word[1..]);
        }

        return sb.Length > 0 ? sb.ToString() : "field";
    }

    internal static string SanitizeAnalyzerId(string name)
    {
        var lower = name.ToLowerInvariant().Trim();
        var sanitized = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "user-schema" : sanitized;
    }
}
