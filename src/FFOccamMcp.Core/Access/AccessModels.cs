namespace OccamMcp.Core.Access;

public enum AccessDisposition
{
    Open,
    Restricted,
    Unknown,
}

public enum AccessEvidenceStage
{
    Prefetch,
    Dom,
    Extracted,
    Combined,
}

/// <summary>Bounded, non-sensitive signals used by every access decision.</summary>
public sealed record AccessEvidence(
    int StatusCode = 0,
    bool HasAuthenticationChallenge = false,
    bool RedirectedToLogin = false,
    bool PasswordField = false,
    bool IdentityField = false,
    bool LoginFormAction = false,
    bool LoginHeading = false,
    bool BlockingOverlay = false,
    bool HasUsableContent = false,
    bool AuthenticationTerminology = false,
    AccessEvidenceStage Stage = AccessEvidenceStage.Combined)
{
    public bool HasBlockingIdentityUi =>
        PasswordField
        && (IdentityField || LoginFormAction || LoginHeading)
        && !HasUsableContent;
}

public sealed record AccessAssessment(
    AccessDisposition Disposition,
    double Confidence,
    AccessEvidenceStage Stage,
    IReadOnlyList<string> EvidenceCodes,
    string RecommendedAction)
{
    public bool RequiresLogin => Disposition == AccessDisposition.Restricted;
}
