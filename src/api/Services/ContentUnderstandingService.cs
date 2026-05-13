using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
}
