using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Search;
using OccamMcp.Core.Services;

namespace OccamMcp.L0Gate;

internal static class HttpProbeFetcherUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunUtf8ByteCap(assert);
        RunStreamingDeadline(assert);
        RunCallerCancellation(assert);
        RunSitemapTotalDeadline(assert);
        RunSearchProviderAsync(assert);
    }

    private static void RunUtf8ByteCap(Action<string, bool> assert)
    {
        var fetcher = CreateFetcher(_ => Response("ééé", "text/html; charset=utf-8"));
        var result = fetcher.FetchAsync(
            "https://example.com/utf8",
            timeoutMs: 2_000,
            maxBytes: 5,
            trackRedirects: false).GetAwaiter().GetResult();

        assert("probe byte cap counts source bytes", result.HtmlBytes == 5);
        assert("probe byte cap preserves complete utf8 prefix", result.HtmlSample?.StartsWith("éé", StringComparison.Ordinal) == true);
    }

    private static void RunStreamingDeadline(Action<string, bool> assert)
    {
        var fetcher = CreateFetcher(_ => Response("<html><body>slow</body></html>", delayMs: 2_000));
        var started = Stopwatch.StartNew();
        var result = fetcher.FetchAsync(
            "https://example.com/slow",
            timeoutMs: 150,
            trackRedirects: false).GetAwaiter().GetResult();

        assert("probe body streaming timeout is typed", result.FailureCode == "timeout");
        assert("probe body streaming timeout bounds wall time", started.ElapsedMilliseconds < 1_500);
    }

    private static void RunCallerCancellation(Action<string, bool> assert)
    {
        var fetcher = CreateFetcher(_ => Response("<html><body>cancel</body></html>", delayMs: 2_000));
        using var cancellation = new CancellationTokenSource(75);
        var cancelled = false;
        try
        {
            _ = fetcher.FetchAsync(
                "https://example.com/cancel",
                timeoutMs: 2_000,
                trackRedirects: false,
                cancellationToken: cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        assert("probe preserves caller cancellation", cancelled);
    }

    private static void RunSitemapTotalDeadline(Action<string, bool> assert)
    {
        var fetcher = CreateFetcher(request => request.RequestUri?.AbsolutePath switch
        {
            "/robots.txt" => Response("Sitemap: https://example.com/sitemap-1.xml", "text/plain"),
            "/sitemap-1.xml" => Response(
                "<sitemapindex><sitemap><loc>https://example.com/sitemap-2.xml</loc></sitemap></sitemapindex>",
                "application/xml",
                delayMs: 90),
            "/sitemap-2.xml" => Response(
                "<urlset><url><loc>https://example.com/docs</loc></url></urlset>",
                "application/xml",
                delayMs: 2_000),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var started = Stopwatch.StartNew();
        var result = SitemapDiscovery.DiscoverAsync(
            fetcher,
            "https://example.com/",
            maxLinks: 8,
            sameDomainOnly: true,
            timeoutMs: 250,
            robotsOnly: false).GetAwaiter().GetResult();

        assert("sitemap total deadline reports timeout", result.TimedOut);
        assert("sitemap total deadline includes response bodies", started.ElapsedMilliseconds < 1_500);
    }

    private static void RunSearchProviderAsync(Action<string, bool> assert)
    {
        var handler = new DelegateHandler(_ => Response(
            "{\"results\":[{\"title\":\"Result\",\"url\":\"https://example.com/doc\",\"content\":\"Snippet\"}]}",
            "application/json"));
        using var client = new HttpClient(handler, disposeHandler: false);
        var provider = new SearxngProvider();
        var outcome = provider.SearchAsync(
            client,
            "query",
            5,
            "https://search.example",
            null,
            CancellationToken.None).GetAwaiter().GetResult();
        assert("search provider async JSON parse", outcome.Ok && outcome.Results.Count == 1);

        var slowHandler = new DelegateHandler(_ => Response(
            "{\"results\":[]}",
            "application/json",
            delayMs: 2_000));
        using var slowClient = new HttpClient(slowHandler, disposeHandler: false);
        using var cancellation = new CancellationTokenSource(75);
        var cancelled = false;
        try
        {
            _ = provider.SearchAsync(
                slowClient,
                "query",
                5,
                "https://search.example",
                null,
                cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        assert("search provider preserves caller cancellation", cancelled);
    }

    private static HttpProbeFetcher CreateFetcher(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new TestHttpClientFactory(new DelegateHandler(responseFactory)));

    private static HttpResponseMessage Response(
        string body,
        string contentType = "text/html; charset=utf-8",
        int delayMs = 0)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        HttpContent content = delayMs > 0
            ? new StreamContent(new DelayedReadStream(bytes, delayMs))
            : new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }

    private sealed class DelayedReadStream(byte[] bytes, int delayMs) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => _offset; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            if (_offset >= bytes.Length)
            {
                return 0;
            }

            var count = Math.Min(buffer.Length, bytes.Length - _offset);
            bytes.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
