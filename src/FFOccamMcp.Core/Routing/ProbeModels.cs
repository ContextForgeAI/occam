namespace OccamMcp.Core.Routing;

public enum PrivacyMode
{
    LocalPublic,
    LocalPrivate,
    BlockedByPolicy,
}

public enum ProbeFailureKind
{
    None,
    InvalidArguments,
}

public sealed class ProbeSignals
{
    public string PageClass { get; init; } = "unknown";
    public bool RequiresJavascript { get; init; }
    public bool SpaShell { get; init; }
    public bool LikelyCookieConsent { get; init; }
    public bool LikelyChallenge { get; init; }
    public bool LikelyLoginRequired { get; init; }
    public bool LikelyPaywall { get; init; }
    public double VisibleTextRatio { get; init; }
    public int HtmlBytes { get; init; }
    public bool HasTables { get; init; }
    public bool HasLlmsTxtLink { get; init; }
}

public sealed class PrivacyClassification
{
    public required PrivacyMode Mode { get; init; }
    public bool IsPrivateHost { get; init; }
    public ProbeFailureKind? BlockReason { get; init; }
}
