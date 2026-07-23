namespace OccamMcp.Core.Playbooks;

public sealed record PlaybookResolveOptions(
    string UrlOrHost,
    string SchemaVersion = "1.0",
    bool IncludeLessons = false,
    bool FetchSiteGenome = false)
{
    public bool ShouldFetchSiteGenome()
    {
        if (FetchSiteGenome)
        {
            return true;
        }

        var env = Environment.GetEnvironmentVariable("OCCAM_SITE_GENOME_FETCH");
        return string.Equals(env, "1", StringComparison.Ordinal)
            || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record GenomeFetchMetadata(
    bool Ok,
    string WellKnownUrl,
    string? FailureCode,
    bool CacheHit = false);
