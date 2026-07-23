using System.Diagnostics;
using System.Net;

namespace OccamMcp.Core.Probe;

internal static class HttpRedirectFollower
{
    private const int MaxRedirects = 10;

    public static async Task<RedirectFollowResult> FollowAsync(
        HttpClient client,
        string url,
        HttpCompletionOption completionOption,
        long startedTicks,
        CancellationToken cancellationToken)
    {
        var chain = new List<string>();
        var current = url;
        HttpResponseMessage? response = null;

        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                response?.Dispose();
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                response = await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (status is >= 300 and <= 399
                    && response.Headers.Location is not null
                    && hop < MaxRedirects)
                {
                    if (chain.Count == 0)
                    {
                        chain.Add(current);
                    }

                    current = ResolveLocation(current, response.Headers.Location);
                    chain.Add(current);
                    continue;
                }

                return RedirectFollowResult.Ok(
                    response,
                    chain.Count > 0 ? chain : null,
                    current,
                    ElapsedMs(startedTicks));
            }

            response?.Dispose();
            return RedirectFollowResult.Failed(url, "redirect_loop", startedTicks);
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }

    private static string ResolveLocation(string currentUrl, Uri location)
    {
        if (location.IsAbsoluteUri)
        {
            return location.ToString();
        }

        if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, location, out var resolved))
        {
            return resolved.ToString();
        }

        return location.ToString();
    }

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
}

internal sealed class RedirectFollowResult
{
    private RedirectFollowResult(
        HttpResponseMessage? response,
        IReadOnlyList<string>? redirectChain,
        string finalUrl,
        int latencyMs,
        string? failureCode)
    {
        Response = response;
        RedirectChain = redirectChain;
        FinalUrl = finalUrl;
        LatencyMs = latencyMs;
        FailureCode = failureCode;
    }

    public HttpResponseMessage? Response { get; }
    public IReadOnlyList<string>? RedirectChain { get; }
    public string FinalUrl { get; }
    public int LatencyMs { get; }
    public string? FailureCode { get; }

    public static RedirectFollowResult Failed(string url, string code, long startedTicks) =>
        new(null, null, url, ElapsedMs(startedTicks), code);

    public static RedirectFollowResult Ok(
        HttpResponseMessage response,
        IReadOnlyList<string>? redirectChain,
        string finalUrl,
        int latencyMs) =>
        new(response, redirectChain, finalUrl, latencyMs, null);

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
}
