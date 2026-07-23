namespace OccamMcp.Core.Playbooks;

public sealed record QualityAssessment(
    int Score,
    double NoiseLeakage,
    bool PassesGate,
    IReadOnlyList<string> Warnings);

public static class QualityGate
{
    public const int MinScore = 70;
    public const double MaxNoise = 0.12;

    public static QualityAssessment Evaluate(QualityInput input)
    {
        var retention = Clamp01(input.ContentRetention);
        var noise = Clamp01(input.NoiseLeakage);
        var structure = Clamp01(input.StructureFidelity);
        var completeness = Clamp01(input.PageCompleteness);
        var confidence = Clamp01(input.BackendConfidence);

        var score = (int)Math.Round(
            retention * 30
            + (1 - noise) * 25
            + structure * 20
            + completeness * 15
            + confidence * 10);

        score = Math.Clamp(score, 0, 100);
        var warnings = new List<string>();
        if (noise > MaxNoise)
        {
            warnings.Add("high_noise");
        }

        if (retention < 0.66)
        {
            warnings.Add("low_retention");
        }

        if (structure < 0.5)
        {
            warnings.Add("weak_structure");
        }

        var passesGate = score >= MinScore && noise <= MaxNoise;
        return new QualityAssessment(score, noise, passesGate, warnings);
    }

    public static QualityAssessment AssessExtraction(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new QualityAssessment(0, 1.0, false, ["empty"]);
        }

        var lower = markdown.ToLowerInvariant();
        string[] noiseTerms =
        [
            "cookie", "subscribe", "sign in", "accept all", "advertisement", "privacy policy",
            "table of contents", "related posts", "share this article", "sign up for free",
        ];

        var noiseHits = noiseTerms.Count(term => lower.Contains(term, StringComparison.Ordinal));
        var effectiveHits = Math.Max(0, noiseHits - 1);
        var noise = Math.Min(1.0, effectiveHits / 6.0);
        var headings = markdown.Split('\n').Count(line => line.StartsWith('#'));
        var structure = Math.Min(1.0, headings / 3.0);
        var retention = markdown.Length switch
        {
            >= 4000 => 0.95,
            >= 1500 => 0.85,
            >= 600 => 0.75,
            >= 200 => 0.65,
            _ => 0.35,
        };

        return Evaluate(new QualityInput
        {
            ContentRetention = retention,
            NoiseLeakage = noise,
            StructureFidelity = structure,
            PageCompleteness = retention,
            BackendConfidence = 0.85,
        });
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}

public sealed class QualityInput
{
    public double ContentRetention { get; init; }
    public double NoiseLeakage { get; init; }
    public double StructureFidelity { get; init; }
    public double PageCompleteness { get; init; }
    public double BackendConfidence { get; init; }
}
