using System.Diagnostics;
using System.Net;
using System.Text;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Probe;

public sealed class HttpProbeFetcher(IHttpClientFactory httpClientFactory)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private const int DefaultMaxBytes = 256 * 1024;
    // Hard allocation ceiling. The default probe read is 256 KiB, but callers that legitimately need
    // more (sitemap discovery asks for 2 MiB) must be honoured — clamping their request down to the
    // default silently truncated large sitemaps to the first 256 KiB and dropped every URL past it.
    private const int AbsoluteMaxBytes = 4 * 1024 * 1024;
    internal const string RedirectTrackingClientName = "probe.redirectTracking";
    internal const string AutoRedirectClientName = "probe.autoRedirect";

    public async Task<ProbeFetchResult> FetchAsync(
        string url,
        int timeoutMs,
        int maxBytes = DefaultMaxBytes,
        bool trackRedirects = true,
        IReadOnlyDictionary<string, string>? requestHeaders = null,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 1, 120_000)));
            var clientName = trackRedirects ? RedirectTrackingClientName : AutoRedirectClientName;
            var client = _httpClientFactory.CreateClient(clientName);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(OccamFetchDefaults.UserAgent);
            ProbeHttpHeaders.Apply(client, requestHeaders);

            HttpResponseMessage response;
            IReadOnlyList<string>? redirectChain = null;
            string finalUrl;
            int latencyMs;

            if (trackRedirects)
            {
                var followed = await HttpRedirectFollower.FollowAsync(
                    client,
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    started,
                    timeoutCts.Token).ConfigureAwait(false);
                if (followed.FailureCode is not null)
                {
                    return new ProbeFetchResult
                    {
                        Ok = false,
                        RequestedUrl = url,
                        FinalUrl = followed.FinalUrl,
                        RedirectChain = followed.RedirectChain,
                        FailureCode = followed.FailureCode,
                        LatencyMs = followed.LatencyMs,
                    };
                }

                response = followed.Response!;
                redirectChain = followed.RedirectChain;
                finalUrl = followed.FinalUrl;
                latencyMs = followed.LatencyMs;
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token).ConfigureAwait(false);
                finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                latencyMs = ElapsedMs(started);
            }

            using (response)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;

                if (ContentFormatDetector.IsPdfContentType(contentType) || ContentFormatDetector.IsPdfUrl(finalUrl))
                {
                    return new ProbeFetchResult
                    {
                        Ok = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        ContentType = contentType ?? "application/pdf",
                        RequestedUrl = url,
                        FinalUrl = finalUrl,
                        RedirectChain = redirectChain,
                        IsPdf = true,
                        HasAuthenticationChallenge = response.Headers.WwwAuthenticate.Count > 0,
                        LatencyMs = latencyMs,
                    };
                }

                if (!IsHtml(contentType))
                {
                    return new ProbeFetchResult
                    {
                        Ok = false,
                        StatusCode = (int)response.StatusCode,
                        ContentType = contentType,
                        RequestedUrl = url,
                        FinalUrl = finalUrl,
                        RedirectChain = redirectChain,
                        FailureCode = "unsupported_content_type",
                        HasAuthenticationChallenge = response.Headers.WwwAuthenticate.Count > 0,
                        LatencyMs = latencyMs,
                    };
                }

                var byteLimit = Math.Clamp(maxBytes, 1, AbsoluteMaxBytes);
                var buffer = GC.AllocateUninitializedArray<byte>(byteLimit);
                var read = 0;
                await using (var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    while (read < byteLimit)
                    {
                        var count = await stream.ReadAsync(buffer.AsMemory(read, byteLimit - read), timeoutCts.Token)
                            .ConfigureAwait(false);
                        if (count == 0)
                        {
                            break;
                        }

                        read += count;
                    }
                }

                var html = DecodeHtml(response.Content.Headers.ContentType?.CharSet, buffer.AsSpan(0, read));

                return new ProbeFetchResult
                {
                    Ok = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    ContentType = contentType,
                    RequestedUrl = url,
                    FinalUrl = finalUrl,
                    RedirectChain = redirectChain,
                    HtmlBytes = read,
                    HtmlSample = html,
                    HasAuthenticationChallenge = response.Headers.WwwAuthenticate.Count > 0,
                    LatencyMs = ElapsedMs(started),
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(url, "timeout", started);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Failed(url, "http_404", started);
        }
        catch (HttpRequestException)
        {
            return Failed(url, "network_error", started);
        }
        catch (UriFormatException)
        {
            return Failed(url, "invalid_url", started);
        }
        catch
        {
            return Failed(url, "network_error", started);
        }
    }

    private static bool IsHtml(string? contentType) =>
        contentType is null
        || contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase);

    private static Encoding ResolveEncoding(string? charset, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        var normalized = charset?.Trim().Trim('"').ToLowerInvariant();
        return normalized switch
        {
            "utf-16" or "utf-16le" or "unicode" => Encoding.Unicode,
            "utf-16be" or "unicodefffe" => Encoding.BigEndianUnicode,
            "us-ascii" or "ascii" => Encoding.ASCII,
            "iso-8859-1" or "latin1" or "latin-1" => Encoding.Latin1,
            _ => Encoding.UTF8,
        };
    }

    private static string DecodeHtml(string? charset, ReadOnlySpan<byte> bytes)
    {
        var encoding = ResolveEncoding(charset, bytes);
        var bomLength = bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF
                ? 3
                : bytes.Length >= 2
                    && ((bytes[0] == 0xFF && bytes[1] == 0xFE)
                        || (bytes[0] == 0xFE && bytes[1] == 0xFF))
                    ? 2
                    : 0;
        return encoding.GetString(bytes[bomLength..]);
    }

    private static ProbeFetchResult Failed(string url, string code, long started) =>
        new()
        {
            Ok = false,
            RequestedUrl = url,
            FailureCode = code,
            LatencyMs = ElapsedMs(started),
        };

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
}
