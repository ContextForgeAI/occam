using OccamMcp.Core.Claims;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Attest;

public interface IAttestService
{
    ValueTask<OccamAttestResponse> AttestAsync(
        IReadOnlyList<OccamAttestClaimInput> claims,
        OccamBackendPolicy policy,
        string? sessionProfile,
        CancellationToken cancellationToken);
}

/// <summary>
/// SI-11: three-layer attestation — (1) BM25 retrieval of candidate blocks via claim-check,
/// (2) semantic support classification (fail-closed), (3) Merkle proof of block existence only.
/// Retrieval score never decides support; proof never decides support.
/// </summary>
public sealed class AttestService(IClaimCheckService claimCheckService) : IAttestService
{
    private const int RetrievalTopK = 3;

    public async ValueTask<OccamAttestResponse> AttestAsync(
        IReadOnlyList<OccamAttestClaimInput> claims,
        OccamBackendPolicy policy,
        string? sessionProfile,
        CancellationToken cancellationToken)
    {
        var perClaim = new OccamAttestClaimResult[claims.Count];
        for (var i = 0; i < claims.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            perClaim[i] = await CheckOneAsync(claims[i], policy, sessionProfile, cancellationToken);
        }

        var counts = AttestClassifier.Summarize(perClaim);
        return new OccamAttestResponse(
            Ok: true,
            ClaimsTotal: perClaim.Length,
            Supported: counts.Supported,
            Contradicted: counts.Contradicted,
            Related: counts.Related,
            Unsupported: counts.Unsupported,
            Unknown: counts.Unknown,
            Grounded: counts.Supported,
            UnsupportedTotal: counts.UnsupportedTotal,
            PerClaim: perClaim,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }

    private async ValueTask<OccamAttestClaimResult> CheckOneAsync(
        OccamAttestClaimInput input,
        OccamBackendPolicy policy,
        string? sessionProfile,
        CancellationToken cancellationToken)
    {
        var url = (input.SourceUrl ?? string.Empty).Trim();
        var claim = (input.Claim ?? string.Empty).Trim();

        if (url.Length == 0 || claim.Length == 0)
        {
            return Result(claim, url, AttestStatus.Unknown, reason: "invalid_arguments",
                receipt: null, root: null, match: null);
        }

        // Layer 1 — retrieval only (top-K blocks). Never treat found/score as support.
        var (success, failure) = await claimCheckService.CheckAsync(
            url, claim, policy, sessionProfile, maxMatches: RetrievalTopK, cancellationToken);

        if (failure is not null)
        {
            // Fail-closed: cannot verify → unknown (not unsupported-as-refuted).
            return Result(claim, url, AttestStatus.Unknown, reason: failure.Failure.Code,
                receipt: failure.Receipt, root: null, match: null);
        }

        var matches = success!.Matches ?? [];
        var blockTexts = matches.Select(m => m.Text ?? string.Empty).ToList();
        // proven==true on found:false means complete leaf set → absence is complete.
        // When matches exist, retrieval is "complete enough" for semantic judgment.
        var retrievalComplete = matches.Length > 0 || success.Proven != false;

        // Layer 2 — semantic classifier (independent of score / proof).
        var status = ClaimSemanticClassifier.Classify(claim, blockTexts, retrievalComplete);

        // Layer 3 — attach existence proof for the top retrieved block when present.
        // Proof proves the block was in the signed extract; it does NOT affirm the claim.
        var top = matches.Length > 0 ? matches[0] : null;
        var reason = status switch
        {
            AttestStatus.Unsupported when matches.Length == 0 => "no_matching_block",
            AttestStatus.Unsupported => "no_semantic_support",
            AttestStatus.Related => "related_not_supported",
            AttestStatus.Contradicted => "contradicted_by_source",
            AttestStatus.Unknown => "insufficient_confidence",
            _ => null,
        };

        return Result(claim, url, status, reason, success.Receipt, success.BlockMerkleRoot, top);
    }

    private static OccamAttestClaimResult Result(
        string claim,
        string url,
        string status,
        string? reason,
        Receipts.ReceiptEnvelope? receipt,
        string? root,
        OccamClaimMatchInfo? match) =>
        new(
            Claim: claim,
            SourceUrl: url,
            Status: status,
            Grounded: AttestStatus.IsGroundedAlias(status),
            BlockIndex: match?.BlockIndex,
            Text: match?.Text,
            Score: match?.Score,
            Leaf: match?.Leaf,
            Proof: match?.Proof,
            BlockMerkleRoot: root,
            Receipt: receipt,
            Reason: reason);
}

/// <summary>
/// Pure, network-free attestation aggregation — gate-testable. Grounded is solely status=supported.
/// </summary>
public static class AttestClassifier
{
    public sealed record StatusCounts(
        int Supported,
        int Contradicted,
        int Related,
        int Unsupported,
        int Unknown)
    {
        public int UnsupportedTotal => Contradicted + Related + Unsupported + Unknown;
    }

    public static bool IsGrounded(string status) => AttestStatus.IsGroundedAlias(status);

    public static StatusCounts Summarize(IReadOnlyList<OccamAttestClaimResult> perClaim)
    {
        var supported = 0;
        var contradicted = 0;
        var related = 0;
        var unsupported = 0;
        var unknown = 0;

        for (var i = 0; i < perClaim.Count; i++)
        {
            switch (perClaim[i].Status)
            {
                case AttestStatus.Supported:
                    supported++;
                    break;
                case AttestStatus.Contradicted:
                    contradicted++;
                    break;
                case AttestStatus.Related:
                    related++;
                    break;
                case AttestStatus.Unsupported:
                    unsupported++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        return new StatusCounts(supported, contradicted, related, unsupported, unknown);
    }
}
