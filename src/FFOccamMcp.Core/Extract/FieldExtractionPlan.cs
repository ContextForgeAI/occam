namespace OccamMcp.Core.Extract;

public sealed class FieldSpec
{
    public required string Selector { get; init; }
    public string Attribute { get; init; } = "text";
    public bool Multiple { get; init; }
    public int? Divide { get; init; }
}

public sealed class FieldExtractionPlan
{
    public required IReadOnlyDictionary<string, FieldSpec> Fields { get; init; }
    public string? BaseSelector { get; init; }
    public bool RowMode => !string.IsNullOrWhiteSpace(BaseSelector);
    public IReadOnlyList<string> Required => Fields.Keys.ToList();
}
