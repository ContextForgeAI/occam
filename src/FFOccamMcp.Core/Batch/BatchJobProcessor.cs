using System.Text.Json;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.Hosting;

namespace OccamMcp.Core.Batch;

internal sealed class BatchJobProcessor(
    IBatchJobStore store,
    TranscodePipeline pipeline) : BackgroundService
{
    private readonly SemaphoreSlim _parallel = new(BatchSettings.Parallel, BatchSettings.Parallel);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            PendingBatchItem? pending = store.ClaimNextPendingItem();
            if (pending is null)
            {
                await Task.Delay(200, stoppingToken).ConfigureAwait(false);
                continue;
            }

            await _parallel.WaitAsync(stoppingToken).ConfigureAwait(false);
            _ = Task.Run(() => ProcessItemAsync(pending, stoppingToken), stoppingToken);
        }
    }

    private async Task ProcessItemAsync(PendingBatchItem item, CancellationToken stoppingToken)
    {
        try
        {
            if (!OccamBackendPolicyParser.TryParse(item.Params.BackendPolicy, out var policy))
            {
                store.MarkItemComplete(
                    item.JobId,
                    item.Seq,
                    ok: false,
                    resultJson: null,
                    failureCode: "invalid_policy",
                    failureMessage: "Stored backend_policy is invalid.");
                return;
            }

            var options = new OccamTranscodeOptions
            {
                MaxTokens = item.Params.MaxTokens,
                FitMarkdown = item.Params.FitMarkdown,
                FocusQuery = item.Params.FocusQuery,
                SessionProfile = item.Params.SessionProfile,
                PlaybookPolicy = item.Params.PlaybookPolicy,
            };

            TranscodeOutcome outcome;
            if (string.Equals(item.Params.OnOversize, HttpExtractOversizeScope.Partial, StringComparison.OrdinalIgnoreCase))
            {
                using var oversizeScope = HttpExtractOversizeScope.PushPartial();
                using (HttpExtractRoutingScope.PushOneShot())
                {
                    outcome = await pipeline.TranscodeAsync(item.Url, policy, options, stoppingToken);
                }
            }
            else
            {
                using (HttpExtractRoutingScope.PushOneShot())
                {
                    outcome = await pipeline.TranscodeAsync(item.Url, policy, options, stoppingToken);
                }
            }

            if (outcome.Ok)
            {
                var result = new BatchItemResult(
                    item.Url,
                    true,
                    outcome.Markdown,
                    outcome.Backend,
                    outcome.TokensEstimated ?? 0,
                    null);
                var json = JsonSerializer.Serialize(result, BatchJsonContext.Default.BatchItemResult);
                store.MarkItemComplete(item.JobId, item.Seq, ok: true, resultJson: json, null, null);
            }
            else
            {
                var includeMarkdown = string.Equals(outcome.FailureCode, "response_truncated", StringComparison.Ordinal);
                var result = new BatchItemResult(
                    item.Url,
                    false,
                    includeMarkdown ? outcome.Markdown : null,
                    outcome.Backend,
                    outcome.TokensEstimated ?? 0,
                    new BatchFailureInfo(
                        outcome.FailureCode ?? "extraction_failed",
                        outcome.Message ?? "Extract failed."));
                var json = JsonSerializer.Serialize(result, BatchJsonContext.Default.BatchItemResult);
                store.MarkItemComplete(
                    item.JobId,
                    item.Seq,
                    ok: false,
                    resultJson: json,
                    outcome.FailureCode,
                    outcome.Message);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown — leave item running; next start may reclaim stale rows in a future slice.
        }
        catch (Exception ex)
        {
            store.MarkItemComplete(
                item.JobId,
                item.Seq,
                ok: false,
                resultJson: null,
                failureCode: "transcode_failed",
                failureMessage: ex.Message);
        }
        finally
        {
            _parallel.Release();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
