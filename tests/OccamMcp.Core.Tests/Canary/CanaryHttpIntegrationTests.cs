using System.Net;
using System.Text.Json;
using OccamMcp.Core.Canary;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Full-stack checks against a real Kestrel listener: fetch a probe document, pull the sentinel out
/// of the HTML the way an agent would, and adjudicate the claim over HTTP. Unit tests can prove the
/// state machine; only this proves the wiring, the headers and the status codes.
/// </summary>
public sealed class CanaryHttpIntegrationTests
{
    private static async Task<(CanaryProbeServerHost Host, CanaryService Service)> StartAsync(CanaryOptions? options = null)
    {
        var service = new CanaryService(options ?? new CanaryOptions());
        var host = new CanaryProbeServerHost(service, port: 0);
        await host.StartAsync();
        return (host, service);
    }

    [Fact]
    public async Task ProbeDocument_EmbedsASentinelThatVerifies()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            var sessionId = CanaryService.NewSessionId();

            var html = await client.GetStringAsync(host.ProbeUrl(sessionId));
            var sentinel = ExtractSentinel(html);

            Assert.NotNull(sentinel);
            var verdict = await GetVerdictAsync(client, host.VerifyUrl(sessionId, sentinel!));
            Assert.Equal(CanaryVerdictStrings.ReadVerified, verdict);
        }
    }

    [Fact]
    public async Task ProbeDocument_RefusesToBeCached()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();

            using var response = await client.GetAsync(host.ProbeUrl(CanaryService.NewSessionId()));

            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.True(response.Headers.CacheControl?.NoCache);
            Assert.True(response.Headers.CacheControl?.MustRevalidate);
            Assert.Contains("no-cache", response.Headers.Pragma.ToString(), StringComparison.Ordinal);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        }
    }

    [Fact]
    public async Task ProbeDocument_CarriesTheSentinelInAllThreePlacements()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            var sessionId = CanaryService.NewSessionId();

            var html = await client.GetStringAsync(host.ProbeUrl(sessionId));
            var sentinel = ExtractSentinel(html);

            Assert.NotNull(sentinel);
            Assert.Contains($"name=\"{CanaryPage.MetaName}\" content=\"{sentinel}\"", html, StringComparison.Ordinal);
            Assert.Contains($"<span style=\"display:none\" data-occam-sentinel=\"1\">{sentinel}</span>", html, StringComparison.Ordinal);
            // A readability-style extractor drops both of the above; the visible copy is what keeps
            // the canary measuring the agent rather than the extractor.
            Assert.Contains($"<code>{sentinel}</code>", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InventedClaim_IsReportedAsHallucinated()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            var sessionId = CanaryService.NewSessionId();
            await client.GetStringAsync(host.ProbeUrl(sessionId));

            var verdict = await GetVerdictAsync(
                client, host.VerifyUrl(sessionId, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));

            Assert.Equal(CanaryVerdictStrings.Hallucinated, verdict);
        }
    }

    [Fact]
    public async Task VerifyResponse_NeverEchoesASentinel()
    {
        // An oracle that returns the correct value on a wrong guess would hand out proof material.
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            var sessionId = CanaryService.NewSessionId();
            var html = await client.GetStringAsync(host.ProbeUrl(sessionId));
            var sentinel = ExtractSentinel(html)!;

            var json = await client.GetStringAsync(host.VerifyUrl(sessionId, "wrong-value"));

            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InvalidSessionId_IsRejectedWithBadRequest()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();

            using var response = await client.GetAsync($"{host.ListenUrl}/probe/canary/has%20space");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid_session_id", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExhaustedRateLimit_ReturnsTooManyRequestsWithRetryAfter()
    {
        var (host, service) = await StartAsync(new CanaryOptions
        {
            RateLimitRequestsPerWindow = 2,
            RateLimitWindowSeconds = 60,
        });
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            var url = host.ProbeUrl(CanaryService.NewSessionId());

            Assert.True((await client.GetAsync(url)).IsSuccessStatusCode);
            Assert.True((await client.GetAsync(url)).IsSuccessStatusCode);
            using var refused = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
            Assert.NotNull(refused.Headers.RetryAfter);
        }
    }

    [Fact]
    public async Task HealthEndpoint_ReportsTheProbeMode()
    {
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();

            var json = await client.GetStringAsync($"{host.ListenUrl}/health");

            Assert.Contains("canary-probe", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task NonLoopbackHostHeader_IsRefused()
    {
        // Mitigates DNS rebinding against a loopback-bound probe.
        var (host, service) = await StartAsync();
        await using (host)
        using (service)
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get, host.ProbeUrl(CanaryService.NewSessionId()));
            request.Headers.Host = "attacker.example";

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public void ConstructorArgumentsAreValidated()
    {
        using var service = new CanaryService(new CanaryOptions());

        Assert.Throws<ArgumentNullException>(() => new CanaryProbeServerHost(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanaryProbeServerHost(service, port: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanaryProbeServerHost(service, port: 70_000));
        Assert.Throws<ArgumentException>(() => new CanaryProbeServerHost(service, bindAddress: " "));
    }

    private static string? ExtractSentinel(string html)
    {
        var marker = $"name=\"{CanaryPage.MetaName}\" content=\"";
        var index = html.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = html.IndexOf('"', start);
        return end > start ? html[start..end] : null;
    }

    private static async Task<string?> GetVerdictAsync(HttpClient client, string url)
    {
        var json = await client.GetStringAsync(url);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("verdict").GetString();
    }
}
