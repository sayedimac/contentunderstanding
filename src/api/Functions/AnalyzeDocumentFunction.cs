using System.Net;
using System.Text.Json;
using ContentUnderstanding.Api.Models;
using ContentUnderstanding.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ContentUnderstanding.Api.Functions;

public sealed class AnalyzeDocumentFunction(
    ContentUnderstandingService contentUnderstandingService,
    ILogger<AnalyzeDocumentFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Function("AnalyzeDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "content-understanding/analyze")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        AnalyzeDocumentRequest? payload;

        try
        {
            payload = await request.ReadFromJsonAsync<AnalyzeDocumentRequest>(cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid analyze request payload.");
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
                new { message = "An analyze document payload is required." },
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(payload.SchemaName))
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "SchemaName is required." },
                cancellationToken);
        }

        if (payload.Fields.Count == 0)
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "At least one field must be defined." },
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(payload.FileContentBase64))
        {
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = "FileContentBase64 is required." },
                cancellationToken);
        }

        try
        {
            var response = await contentUnderstandingService.AnalyzeDocumentAsync(payload, cancellationToken);
            return await CreateJsonResponseAsync(request, HttpStatusCode.OK, response, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "The Content Understanding analyze request failed.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadGateway,
                new { message = exception.Message },
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "The Content Understanding analyze request was rejected.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                new { message = exception.Message },
                cancellationToken);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Document analysis timed out.");
            return await CreateJsonResponseAsync(
                request,
                HttpStatusCode.GatewayTimeout,
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
