using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Session;

public sealed record SessionProfileInfo(string ProfileId, string[] HeadersApplied);

public sealed record FetchPreflightResult(
  bool Ok,
  string? FailureCode,
  string? FailureMessage,
  FetchHeadersScope? HeadersScope,
  SessionProfileInfo? Session,
  IReadOnlyDictionary<string, string>? MergedHeaders)
{
   public string? ActiveHeadersFile => HeadersScope is not null ? FetchHeadersScope.ActivePath : null;
   public string? ActiveStorageStatePath => HeadersScope is not null ? FetchHeadersScope.ActiveStorageStatePath : null;
}

public static class FetchPreflight
{
    public static FetchPreflightResult Prepare(string url, string? sessionProfile)
    {
        var privacy = PrivacyClassifier.Classify(url);
        if (privacy.BlockReason == ProbeFailureKind.InvalidArguments)
        {
            return Fail("invalid_url", "URL is not a valid absolute HTTP or HTTPS URL.");
        }

        if (privacy.IsPrivateHost && PrivacyClassifier.IsPrivateUrlBlocked())
        {
            return Fail("private_url_blocked", "Private or local URLs are blocked.");
        }

        SessionProfileResolveResult? session = null;
        string? storageStatePath = null;
        if (!string.IsNullOrWhiteSpace(sessionProfile))
        {
            session = SessionProfileHeaders.Resolve(sessionProfile);
            if (session.Status != SessionProfileStatus.Ok)
            {
                return Fail(
                    session.FailureCode!,
                    session.Status == SessionProfileStatus.InvalidId
                        ? "session_profile id is invalid."
                        : "session_profile file was not found or could not be read.");
            }

            storageStatePath = session.StorageStatePath;
        }

        FetchHeadersScope? scope = null;
        SessionProfileInfo? sessionInfo = null;
        var merged = RequestHeadersMerger.Merge(
            RequestHeadersMerger.ReadEnvHeaders(),
            session?.Headers);
        if (session is { Status: SessionProfileStatus.Ok, SanitizedId: not null })
        {
            sessionInfo = new SessionProfileInfo(
                session.SanitizedId,
                session.Headers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        if (merged.Count > 0 || !string.IsNullOrWhiteSpace(storageStatePath))
        {
            scope = FetchHeadersScope.Create(merged, storageStatePath);
        }

        return new FetchPreflightResult(true, null, null, scope, sessionInfo, merged.Count > 0 ? merged : null);
    }

    private static FetchPreflightResult Fail(string code, string message) =>
        new(false, code, message, null, null, null);
}
