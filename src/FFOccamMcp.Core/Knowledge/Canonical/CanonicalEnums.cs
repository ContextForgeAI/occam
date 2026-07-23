namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>Acquisition channel / source shape. Not an ontology — a coarse adapter discriminator.</summary>
public enum SourceKind
{
    Unspecified = 0,
    WebPage = 1,
    Document = 2,
    PlainText = 3,
    Api = 4,
    Other = 99,
}

/// <summary>What kind of source fragment the evidence points at.</summary>
public enum EvidenceKind
{
    Unspecified = 0,
    ContentBlock = 1,
    Table = 2,
    Excerpt = 3,
    Media = 4,
    Other = 99,
}

/// <summary>
/// Extensible locator shape for <see cref="EvidenceLocator"/>. PR-A ships only the kinds the 0.9
/// extract path already produces; further kinds (page/section/table-cell/json-path/code-range) are
/// reserved without inventing serializers for them here.
/// </summary>
public enum EvidenceLocatorKind
{
    Unspecified = 0,
    /// <summary>CSS / DOM selector from the current worker extract (<c>source_selector</c>).</summary>
    SourceSelector = 1,
    TextSpan = 2,
    BlockId = 3,
    Page = 4,
    Section = 5,
    TableCell = 6,
    JsonPath = 7,
    CodeRange = 8,
    Custom = 99,
}

/// <summary>
/// Epistemic status of a statement — not a truth score. Distinguishes source-asserted text from
/// normalized facts and system inferences (ADR-0003 invariants 2/4).
/// </summary>
public enum ClaimKind
{
    SourceClaim = 0,
    ExtractedClaim = 1,
    NormalizedFact = 2,
    SystemInference = 3,
    Hypothesis = 4,
    UserAssertion = 5,
    Computed = 6,
}

/// <summary>Coarse semantic bucket for entities (and optional fact typing). Not a formal ontology.</summary>
public enum SemanticType
{
    Unspecified = 0,
    Person = 1,
    Organization = 2,
    Place = 3,
    Concept = 4,
    Event = 5,
    Artifact = 6,
    Other = 99,
}

/// <summary>
/// Validation lifecycle for a Fact/Relationship. <see cref="Supported"/> requires provenance
/// (constructor invariant). Absence of validation on a ClaimCandidate is normal.
/// </summary>
public enum ValidationState
{
    Unvalidated = 0,
    Supported = 1,
    Disputed = 2,
    Rejected = 3,
    Superseded = 4,
}

/// <summary>
/// Categorical confidence. <c>null</c> on records means "not computed" — never invent a score.
/// An extractor salience/score is NOT automatically confidence of truth.
/// </summary>
public enum ConfidenceLevel
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}
