namespace OccamMcp.Core.Playbooks;

public sealed record PlaybookHealRequest(
    string Url,
    string FailureReason,
    string? SessionProfile = null,
    // 600 (the skeleton cap) not 400: on content-heavy pages that render nav/sidebar before main
    // (e.g. MDN) a 400-node cap could be exhausted before the DFS reaches the main-content element,
    // yielding mainCandidates=0 non-deterministically — the flaky L3 K1 heal-capture. 600 lets the
    // walk reach main reliably (verified 8/8 on the MDN pilot).
    int MaxSkeletonNodes = 600);

public sealed record PlaybookHealResult(
    bool Ok,
    string Url,
    string FailureReason,
    DomSkeletonPayload? DomSkeleton,
    PlaybookHealAnchors? Anchors,
    PlaybookHealAgentHints? AgentHints,
    string? FailureCode,
    string? Message,
    int LatencyMs);

public sealed record DomSkeletonPayload(
    DomSkeletonNode Root,
    DomSkeletonStats Stats);

public sealed record DomSkeletonStats(
    int NodeCount,
    int MaxDepth,
    int InteractiveCount);

public sealed record DomSkeletonNode(
    string Tag,
    string? Id,
    IReadOnlyList<string>? Class,
    string? Role,
    string? TestId,
    string? Aria,
    string? Text,
    bool Interactive,
    IReadOnlyList<DomSkeletonNode>? Children);

public sealed record PlaybookHealAnchors(
    IReadOnlyList<string> Landmarks,
    IReadOnlyList<string> DataTestIds,
    IReadOnlyList<MainCandidateAnchor> MainCandidates);

public sealed record MainCandidateAnchor(
    string Selector,
    string? TextAnchor,
    double Score);

public sealed record PlaybookHealAgentHints(
    string SuggestedNext,
    IReadOnlyList<string> DoNot,
    int MaxVerifyRetries = PlaybookHealPolicy.MaxVerifyRetries);

public sealed record PlaybookSaveRequest(
    string Url,
    string PlaybookJson,
    bool Verify = true,
    string? VerifyUrl = null,
    string? LessonNote = null,
    string? FailureReason = null,
    string? HostId = null);

public sealed record PlaybookVerifyMetrics(
    int Score,
    double NoiseLeakage,
    bool PassesGate,
    int ContentLength);

public sealed record PlaybookSaveResult(
    bool Ok,
    string Url,
    string? PlaybookId,
    string? WrittenPath,
    PlaybookVerifyMetrics? Verify,
    bool LessonAppended,
    string? FailureCode,
    string? Message,
    // SI-08: the key id the saved playbook was signed with (null on failure).
    string? SignedKeyId = null);
