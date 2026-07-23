using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Codecs;

/// <summary>One codec's result over a fixed view (ADR-0002 codec-benchmark mode).</summary>
public sealed record CodecBenchRow(
    string CodecId,
    string Version,
    KnowledgeCodecMode Mode,
    bool Deterministic,
    int Tokens,
    int Chars,
    int Utf8Bytes = 0,
    double EncodingDurationMs = 0,
    bool DeterministicOk = false,
    bool ValidOutputOk = false,
    IReadOnlyList<string>? SemanticStructures = null,
    bool? JsonParseable = null,
    bool? JsonSchemaOk = null,
    bool? IdsPreserved = null,
    bool? RefsPreserved = null,
    /// <summary>True when this codec's output carries Canonical ids (knowledge-json path).</summary>
    bool CarriesCanonicalIds = false);

/// <summary>Relative comparison of one codec against markdown-passthrough on the same view.</summary>
public sealed record CodecBenchComparison(
    string CodecId,
    KnowledgeCodecMode Mode,
    int Tokens,
    int PassthroughTokens,
    /// <summary>1 − codec/passthrough; positive ⇒ fewer tokens than passthrough.</summary>
    double? TokenReductionVsPassthrough,
    int Utf8Bytes,
    int PassthroughUtf8Bytes,
    bool DeterministicOk,
    bool ValidOutputOk,
    bool CarriesCanonicalIds,
    bool? JsonParseable,
    bool? IdsPreserved,
    bool? RefsPreserved);

/// <summary>Aggregate usefulness verdict for a codec across fixtures (R4 disposition input).</summary>
public enum CodecUsefulnessVerdict
{
    /// <summary>Live default / compatibility baseline.</summary>
    KeepAsDefault,
    /// <summary>Useful in a niche; keep registered; do not MCP-wire yet.</summary>
    KeepExperimental,
    /// <summary>No measured win for agent token economy; keep for tests/tooling only.</summary>
    KeepExperimentalNotForAgents,
    /// <summary>Harmful or redundant — candidate to drop from product consideration.</summary>
    DoNotPromote,
}

/// <summary>Per-codec disposition row after evaluating a fixture matrix.</summary>
public sealed record CodecDisposition(
    string CodecId,
    CodecUsefulnessVerdict Verdict,
    double? MedianTokenReductionVsPassthrough,
    double? MaxTokenReductionVsPassthrough,
    double? MinTokenReductionVsPassthrough,
    int FixturesMeasured,
    int FixturesCheaperThanPassthrough,
    int FixturesCarryingCanonical,
    string Rationale);

/// <summary>
/// ADR-0002 codec-benchmark harness: takes ONE fixed <see cref="MaterializedKnowledgeView"/> and encodes
/// it with every registered codec, so the comparison measures surface-encoding efficiency in isolation
/// — NOT materialization policy (which is already resolved in the view).
///
/// Token counts use <see cref="TokenEstimator"/> (<see cref="TokenEstimator.EstimatorId"/>) — a
/// heuristic, not an exact tokenizer. Downstream task accuracy is out of scope (external eval).
/// </summary>
public static class CodecBench
{
    public static IReadOnlyList<CodecBenchRow> Run(MaterializedKnowledgeView view, KnowledgeCodecRegistry registry)
    {
        var rows = new List<CodecBenchRow>();
        foreach (var descriptor in registry.Descriptors.OrderBy(d => d.CodecId, StringComparer.Ordinal))
        {
            if (!descriptor.CanEncode || !registry.TryGet(descriptor.CodecId, out var codec) || codec is null)
            {
                continue;
            }

            rows.Add(Measure(view, codec));
        }

        return [.. rows.OrderBy(r => r.Tokens).ThenBy(r => r.CodecId, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Plan once, then encode the same planned view with the supplied codecs (explicit order).
    /// </summary>
    public static IReadOnlyList<CodecBenchRow> RunFixedView(
        MaterializedKnowledgeView view,
        IEnumerable<IKnowledgeCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(view);
        var rows = new List<CodecBenchRow>();
        foreach (var codec in codecs)
        {
            rows.Add(Measure(view, codec));
        }

        return rows;
    }

    /// <summary>Compare every non-passthrough row to markdown-passthrough on the same view.</summary>
    public static IReadOnlyList<CodecBenchComparison> CompareToPassthrough(IReadOnlyList<CodecBenchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var pt = rows.FirstOrDefault(r =>
            string.Equals(r.CodecId, MarkdownPassthroughCodec.Id, StringComparison.OrdinalIgnoreCase));
        if (pt is null)
        {
            return [];
        }

        var list = new List<CodecBenchComparison>();
        foreach (var r in rows)
        {
            if (string.Equals(r.CodecId, pt.CodecId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double? reduction = pt.Tokens <= 0
                ? null
                : Math.Round(1.0 - (r.Tokens / (double)pt.Tokens), 4);
            list.Add(new CodecBenchComparison(
                r.CodecId,
                r.Mode,
                r.Tokens,
                pt.Tokens,
                reduction,
                r.Utf8Bytes,
                pt.Utf8Bytes,
                r.DeterministicOk,
                r.ValidOutputOk,
                r.CarriesCanonicalIds,
                r.JsonParseable,
                r.IdsPreserved,
                r.RefsPreserved));
        }

        return list;
    }

    /// <summary>
    /// Aggregate per-codec disposition across fixture comparison sets (R4).
    /// Does not invent model-accuracy claims — size/structure/determinism only.
    /// </summary>
    public static IReadOnlyList<CodecDisposition> EvaluateDispositions(
        IReadOnlyDictionary<string, IReadOnlyList<CodecBenchComparison>> comparisonsByFixture)
    {
        ArgumentNullException.ThrowIfNull(comparisonsByFixture);

        var byCodec = new Dictionary<string, List<(string Fixture, CodecBenchComparison Comp)>>(StringComparer.Ordinal);
        foreach (var (fixture, comps) in comparisonsByFixture)
        {
            foreach (var c in comps)
            {
                if (!byCodec.TryGetValue(c.CodecId, out var list))
                {
                    list = [];
                    byCodec[c.CodecId] = list;
                }

                list.Add((fixture, c));
            }
        }

        var dispositions = new List<CodecDisposition>
        {
            new(
                MarkdownPassthroughCodec.Id,
                CodecUsefulnessVerdict.KeepAsDefault,
                MedianTokenReductionVsPassthrough: 0,
                MaxTokenReductionVsPassthrough: 0,
                MinTokenReductionVsPassthrough: 0,
                FixturesMeasured: comparisonsByFixture.Count,
                FixturesCheaperThanPassthrough: 0,
                FixturesCarryingCanonical: 0,
                Rationale: "Live MCP compatibility baseline; receipt contentHash hashes this surface. Not optional."),
        };

        foreach (var (codecId, samples) in byCodec.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var reductions = samples
                .Select(s => s.Comp.TokenReductionVsPassthrough)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .OrderBy(x => x)
                .ToArray();
            double? median = reductions.Length == 0
                ? null
                : reductions.Length % 2 == 1
                    ? reductions[reductions.Length / 2]
                    : Math.Round((reductions[reductions.Length / 2 - 1] + reductions[reductions.Length / 2]) / 2.0, 4);
            double? max = reductions.Length == 0 ? null : reductions[^1];
            double? min = reductions.Length == 0 ? null : reductions[0];
            var cheaper = samples.Count(s => s.Comp.TokenReductionVsPassthrough is > 0.05);
            var carries = samples.Count(s => s.Comp.CarriesCanonicalIds);
            var alwaysLarger = reductions.Length > 0 && reductions.All(r => r < 0);
            var anyCanonicalWin = carries > 0
                && samples.Any(s => s.Comp.IdsPreserved == true && s.Comp.RefsPreserved == true);

            CodecUsefulnessVerdict verdict;
            string rationale;
            if (string.Equals(codecId, CompactMarkdownCodec.Id, StringComparison.OrdinalIgnoreCase))
            {
                verdict = CodecUsefulnessVerdict.KeepExperimental;
                rationale = cheaper > 0
                    ? $"Lossy IR→markdown renderer; median Δvs passthrough={median:0.00}. Wins on table-heavy IR fixtures; drops link hrefs / nesting. Do not MCP-wire without accuracy eval."
                    : "No consistent ≥5% token win vs passthrough on measured fixtures; still useful as IR-render experiment. Keep experimental.";
            }
            else if (string.Equals(codecId, JsonKnowledgeCodec.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (alwaysLarger || anyCanonicalWin)
                {
                    verdict = CodecUsefulnessVerdict.KeepExperimentalNotForAgents;
                    rationale = alwaysLarger
                        ? $"Always larger than passthrough on measured fixtures (median Δ={median:0.00}). Value is structural: Canonical ids/refs JSON for tooling — not agent token economy. Do not promote as default."
                        : "Preserves Canonical ids/refs in JSON; token cost usually higher than markdown. Tooling/test surface only until a measured agent win exists.";
                }
                else
                {
                    verdict = CodecUsefulnessVerdict.DoNotPromote;
                    rationale = "No token win and no measured Canonical-id benefit on fixtures — do not expand product surface.";
                }
            }
            else
            {
                verdict = cheaper > 0
                    ? CodecUsefulnessVerdict.KeepExperimental
                    : CodecUsefulnessVerdict.DoNotPromote;
                rationale = cheaper > 0
                    ? $"Measured token reduction on {cheaper}/{samples.Count} fixtures (median Δ={median:0.00}). Keep experimental pending accuracy eval."
                    : "No measured token reduction vs passthrough; do not promote.";
            }

            dispositions.Add(new CodecDisposition(
                codecId,
                verdict,
                median,
                max,
                min,
                samples.Count,
                cheaper,
                carries,
                rationale));
        }

        return dispositions;
    }

    public static string FormatEvaluationReport(
        IReadOnlyDictionary<string, IReadOnlyList<CodecBenchRow>> rowsByFixture,
        IReadOnlyList<CodecDisposition> dispositions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Codec usefulness evaluation (Occam 1.1 R4)");
        sb.AppendLine();
        sb.AppendLine($"Token estimator: `{TokenEstimator.EstimatorId}` (heuristic). No LLM judge / task accuracy in this harness.");
        sb.AppendLine("Axis: same planned view → codecs (planner already decided retention).");
        sb.AppendLine();

        foreach (var (fixture, rows) in rowsByFixture.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"## Fixture `{fixture}`");
            sb.AppendLine();
            sb.AppendLine("| codec | tokens≈ | chars | utf8 | Δvs passthrough | canonical ids | det |");
            sb.AppendLine("|---|---:|---:|---:|---:|---|---|");
            var pt = rows.FirstOrDefault(r =>
                string.Equals(r.CodecId, MarkdownPassthroughCodec.Id, StringComparison.OrdinalIgnoreCase));
            foreach (var r in rows.OrderBy(r => r.CodecId, StringComparer.Ordinal))
            {
                var delta = pt is null || pt.Tokens <= 0 || string.Equals(r.CodecId, pt.CodecId, StringComparison.OrdinalIgnoreCase)
                    ? "—"
                    : (1.0 - r.Tokens / (double)pt.Tokens).ToString("0.00");
                sb.AppendLine(
                    $"| {r.CodecId} | {r.Tokens} | {r.Chars} | {r.Utf8Bytes} | {delta} | {r.CarriesCanonicalIds} | {r.DeterministicOk} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Disposition");
        sb.AppendLine();
        sb.AppendLine("| codec | verdict | median Δ | cheaper fixtures | rationale |");
        sb.AppendLine("|---|---|---:|---:|---|");
        foreach (var d in dispositions)
        {
            var med = d.MedianTokenReductionVsPassthrough is double m ? m.ToString("0.00") : "—";
            sb.AppendLine($"| {d.CodecId} | {d.Verdict} | {med} | {d.FixturesCheaperThanPassthrough}/{d.FixturesMeasured} | {d.Rationale} |");
        }

        return sb.ToString();
    }

    private static CodecBenchRow Measure(MaterializedKnowledgeView view, IKnowledgeCodec codec)
    {
        var sw = Stopwatch.StartNew();
        var first = codec.Encode(view, KnowledgeCodecEncodeOptions.None);
        sw.Stop();
        var second = codec.Encode(view, KnowledgeCodecEncodeOptions.None);

        var surface = first.Surface ?? string.Empty;
        var deterministicOk = string.Equals(first.Surface, second.Surface, StringComparison.Ordinal)
            && string.Equals(first.CodecId, second.CodecId, StringComparison.Ordinal);
        var validOutputOk = !string.IsNullOrWhiteSpace(first.CodecId)
            && first.CodecId == codec.Descriptor.CodecId
            && surface.Length >= 0;

        bool? jsonParseable = null;
        bool? jsonSchemaOk = null;
        bool? idsPreserved = null;
        bool? refsPreserved = null;
        var carriesCanonical = false;

        if (string.Equals(codec.Descriptor.CodecId, JsonKnowledgeCodec.Id, StringComparison.OrdinalIgnoreCase))
        {
            jsonParseable = false;
            jsonSchemaOk = false;
            idsPreserved = false;
            refsPreserved = false;
            try
            {
                using var doc = JsonDocument.Parse(surface);
                jsonParseable = true;
                var root = doc.RootElement;
                jsonSchemaOk =
                    root.TryGetProperty("codec", out var codecProp)
                    && codecProp.GetString() == JsonKnowledgeCodec.Id
                    && root.TryGetProperty("version", out _)
                    && root.TryGetProperty("surface", out var surfaceEl)
                    && surfaceEl.TryGetProperty("mediaType", out _)
                    && surfaceEl.TryGetProperty("text", out _);

                idsPreserved = IdsMatch(view, root);
                refsPreserved = RefsMatch(view, root);
                carriesCanonical = view.HasCanonicalKnowledge
                    && idsPreserved == true
                    && refsPreserved == true;
            }
            catch (JsonException)
            {
                jsonParseable = false;
            }
        }

        return new CodecBenchRow(
            codec.Descriptor.CodecId,
            codec.Descriptor.Version,
            codec.Descriptor.Mode,
            codec.Descriptor.Deterministic,
            TokenEstimator.Estimate(surface),
            surface.Length,
            Encoding.UTF8.GetByteCount(surface),
            sw.Elapsed.TotalMilliseconds,
            deterministicOk,
            validOutputOk,
            DescribeStructures(view),
            jsonParseable,
            jsonSchemaOk,
            idsPreserved,
            refsPreserved,
            carriesCanonical);
    }

    private static IReadOnlyList<string> DescribeStructures(MaterializedKnowledgeView view)
    {
        var list = new List<string> { "surface" };
        if (view.Knowledge is { IsEmpty: false })
        {
            if (view.Knowledge.Blocks.Count > 0)
            {
                list.Add("blocks");
            }

            if (view.Knowledge.Tables.Count > 0)
            {
                list.Add("tables");
            }
        }

        if (view.SourceRefs is { Count: > 0 })
        {
            list.Add("sources");
        }

        if (view.EvidenceRefs is { Count: > 0 })
        {
            list.Add("evidence");
        }

        if (view.Claims is { Count: > 0 })
        {
            list.Add("claims");
        }

        if (view.Provenance is { Count: > 0 })
        {
            list.Add("provenance");
        }

        return list;
    }

    private static bool IdsMatch(MaterializedKnowledgeView view, JsonElement root)
    {
        if (view.SourceRefs is { Count: > 0 })
        {
            if (!root.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expected = view.SourceRefs.Select(s => s.Id.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var actual = sources.EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() : null)
                .Where(s => s is not null)
                .Cast<string>()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (view.Claims is { Count: > 0 })
        {
            if (!root.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expected = view.Claims.Select(c => c.Id.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var actual = claims.EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() : null)
                .Where(s => s is not null)
                .Cast<string>()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RefsMatch(MaterializedKnowledgeView view, JsonElement root)
    {
        if (view.Claims is not { Count: > 0 })
        {
            return true;
        }

        if (!root.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var claim in view.Claims)
        {
            var expected = claim.EvidenceRefs.Select(e => e.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var match = claims.EnumerateArray().FirstOrDefault(e =>
                e.TryGetProperty("id", out var id) && id.GetString() == claim.Id.Value);
            if (match.ValueKind != JsonValueKind.Object
                || !match.TryGetProperty("evidenceRefs", out var refs)
                || refs.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var actual = refs.EnumerateArray()
                .Select(r => r.GetString())
                .Where(s => s is not null)
                .Cast<string>()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
