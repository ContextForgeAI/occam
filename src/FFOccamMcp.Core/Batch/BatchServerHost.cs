using System.Text.Json;
using System.Reflection;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OccamMcp.Core.Batch;

public static class BatchServerHost
{
    public static async Task RunAsync(OccamMcpCli cli, CancellationToken cancellationToken)
    {
        var listenUrl = $"http://{BatchSettings.DefaultBindAddress}:{cli.Port}/";
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(listenUrl);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddOccamBatch();

        var app = builder.Build();
        var store = app.Services.GetRequiredService<IBatchJobStore>();
        store.Initialize();

        app.MapGet("/v1/health", static (HttpContext context, IBatchJobStore store) => HealthAsync(context, store));
        app.MapPost("/v1/batch/submit", static (HttpContext context, IBatchJobService service) => SubmitAsync(context, service));
        app.MapGet("/v1/batch/{jobId}/status", static (HttpContext context, IBatchJobService service, string jobId) => StatusAsync(context, service, jobId));
        app.MapGet("/v1/batch/{jobId}/results", static (HttpContext context, IBatchJobService service, string jobId) => ResultsAsync(context, service, jobId));

        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task HealthAsync(HttpContext context, IBatchJobStore store)
    {
        var ok = store.IsHealthy();
        var assembly = typeof(BatchServerHost).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        context.Response.StatusCode = ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var health = new BatchHealthResponse(ok, version, "ready");

        await JsonSerializer
            .SerializeAsync(context.Response.Body, health, BatchJsonContext.Default.BatchHealthResponse, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async Task SubmitAsync(HttpContext context, IBatchJobService service)
    {
        BatchSubmitRequest? request;
        try
        {
            request = await JsonSerializer
                .DeserializeAsync(context.Request.Body, BatchJsonContext.Default.BatchSubmitRequest, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "Malformed JSON body.")
                .ConfigureAwait(false);
            return;
        }

        if (request is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "Request body is required.")
                .ConfigureAwait(false);
            return;
        }

        var (response, error) = service.Submit(request);
        if (error is not null)
        {
            var status = error.Code == "job_not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            await WriteErrorAsync(context, status, error.Code, error.Message).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status202Accepted;
        context.Response.ContentType = "application/json";
        await JsonSerializer
            .SerializeAsync(context.Response.Body, response!, BatchJsonContext.Default.BatchSubmitResponse, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async Task StatusAsync(HttpContext context, IBatchJobService service, string jobId)
    {
        var (response, error) = service.GetStatus(jobId);
        if (error is not null)
        {
            var status = error.Code == "job_not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            await WriteErrorAsync(context, status, error.Code, error.Message).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = "application/json";
        await JsonSerializer
            .SerializeAsync(context.Response.Body, response!, BatchJsonContext.Default.BatchStatusResponse, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async Task ResultsAsync(HttpContext context, IBatchJobService service, string jobId)
    {
        var cursor = int.TryParse(context.Request.Query["cursor"], out var parsedCursor) ? parsedCursor : 0;
        var limit = int.TryParse(context.Request.Query["limit"], out var parsedLimit) ? parsedLimit : 50;

        var (response, error) = service.GetResults(jobId, cursor, limit);
        if (error is not null)
        {
            var status = error.Code == "job_not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            await WriteErrorAsync(context, status, error.Code, error.Message).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = "application/json";
        await JsonSerializer
            .SerializeAsync(context.Response.Body, response!, BatchJsonContext.Default.BatchResultsResponse, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = new BatchErrorResponse(new BatchFailureInfo(code, message));
        await JsonSerializer
            .SerializeAsync(context.Response.Body, body, BatchJsonContext.Default.BatchErrorResponse, context.RequestAborted)
            .ConfigureAwait(false);
    }
}
