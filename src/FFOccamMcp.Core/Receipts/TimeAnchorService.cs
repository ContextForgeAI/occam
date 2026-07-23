using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// SI-15 producer: request an RFC3161 timestamp token from a configured TSA over a receipt's signature.
/// Opt-in and env-gated (<c>OCCAM_TIME_ANCHOR=1</c> + <c>OCCAM_TSA_URL</c>). The TSA URL is operator-
/// controlled (not user-supplied per request) and still SSRF-guarded, so this cannot be turned into an
/// arbitrary outbound probe. Fail-open: any error (off / network / timeout / malformed / private host)
/// returns null and the receipt ships without an anchor — a time anchor is a bonus, never a gate on
/// the extraction.
/// </summary>
public sealed class TimeAnchorService(IHttpClientFactory httpClientFactory)
{
    public bool IsEnabled() => Flag("OCCAM_TIME_ANCHOR") && TsaUrl() is not null;

    public ReceiptTimeAnchor? TryAnchor(string signatureBase64Url)
    {
        var tsaUrl = TsaUrl();
        if (tsaUrl is null || !Flag("OCCAM_TIME_ANCHOR"))
        {
            return null;
        }

        if (PrivacyClassifier.Classify(tsaUrl).IsPrivateHost)
        {
            return null; // never send a timestamp query to a private/internal host
        }

        try
        {
            var imprint = SHA256.HashData(Base64Url.Decode(signatureBase64Url));
            var request = Rfc3161TimestampRequest.CreateFromHash(
                imprint, HashAlgorithmName.SHA256, requestSignerCertificates: true);

            var timeoutMs = OccamMcp.Core.Configuration.OccamEnvironment.GetInt(
                "OCCAM_TSA_TIMEOUT_MS", defaultValue: 3_000, min: 500, max: 15_000);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            var client = httpClientFactory.CreateClient("receipts.timeAnchor");
            using var content = new ByteArrayContent(request.Encode());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, tsaUrl) { Content = content };
            httpRequest.Headers.Accept.ParseAdd("application/timestamp-reply");

            using var response = client.Send(httpRequest, HttpCompletionOption.ResponseContentRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = response.Content.ReadAsStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var token = request.ProcessResponse(ms.ToArray(), out _);

            // Only attach a token we ourselves confirm binds to our imprint.
            if (!token.VerifySignatureForHash(imprint, HashAlgorithmName.SHA256, out _))
            {
                return null;
            }

            var genTime = token.TokenInfo.Timestamp.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var der = token.AsSignedCms().Encode();
            return new ReceiptTimeAnchor(ReceiptTimeAnchor.TypeRfc3161, tsaUrl, Convert.ToBase64String(der), genTime);
        }
        catch
        {
            return null; // fail-open
        }
    }

    private static string? TsaUrl()
    {
        var u = Environment.GetEnvironmentVariable("OCCAM_TSA_URL");
        return string.IsNullOrWhiteSpace(u) ? null : u.Trim();
    }

    private static bool Flag(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return v == "1"
            || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "on", StringComparison.OrdinalIgnoreCase);
    }
}
