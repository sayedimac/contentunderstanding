using System.Net;
using System.Text.Json;
using ContentUnderstanding.Api.Models;
using ContentUnderstanding.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ContentUnderstanding.Api.Functions;

public sealed class UpsertAnalyzerFunction(
    ContentUnderstandingService contentUnderstandingService,
    ILogger<UpsertAnalyzerFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Function("UpsertAnalyzer")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "content-understanding/analyzers")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SchemaSubmissionRequest? payload;

        try
        {
            payload = await request.ReadFromJsonAsync<SchemaSubmissionRequest>(cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid schema request payload.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "The request body must be valid JSON." },
                cancellationToken);
        }

        if (payload is null)
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "A schema submission payload is required." },
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(payload.AnalyzerId))
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "AnalyzerId is required." },
                cancellationToken);
        }

        if (payload.FieldSchema.ValueKind is not JsonValueKind.Object)
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "FieldSchema must be a JSON object." },
                cancellationToken);
        }

        try
        {
            var response = await contentUnderstandingService.UpsertAnalyzerAsync(payload, cancellationToken);
            return await CreateJsonResponseAsync(request, HttpStatusCode.OK, response, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "The Content Understanding request failed.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadGateway,
                new { message = exception.Message },
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "The Content Understanding request was rejected.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = exception.Message },
                cancellationToken);
        }
    }

    private static async Task<HttpResponseData> CreateJsonResponseAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
        return response;
    }
}
