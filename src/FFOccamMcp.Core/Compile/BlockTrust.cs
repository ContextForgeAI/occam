using System.Text.RegularExpressions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Compile;

/// <summary>
/// #4 trust-channels: tag each block with a machine-checkable <c>trust</c> channel so a consuming
/// harness can hard-isolate untrusted spans instead of trusting all extracted text equally. Two
/// signals, host-side (no worker change):
///   <c>"suspicious"</c> — the block text reads like an instruction to the reader/model (a prompt-
///     injection shape: "ignore previous instructions", "you are now", "system:"). Takes priority.
///   <c>"boilerplate"</c> — the block's <c>source_selector</c> sits in a non-content region
///     (nav/footer/aside/comment/sidebar/menu/related/promo) that leaked past readability.
/// Normal main content is left unset (null). This is a heuristic SIGNAL, not a guarantee — but,
/// unlike prose the model must judge itself, the tag is explicit and (once receipts cover blocks)
/// can be signed, so the isolation rule can't be argued away by the injected text itself.
/// </summary>
public static partial class BlockTrust
{
    public const string Suspicious = "suspicious";
    public const string Boilerplate = "boilerplate";

    public static void Annotate(IReadOnlyList<WorkerExtractBlockInfo> blocks)
    {
        foreach (var block in blocks)
        {
            var text = block.Text ?? string.Empty;
            if (text.Length > 0 && DirectiveRegex().IsMatch(text))
            {
                block.Trust = Suspicious;
            }
            else if (BoilerplateRegionRegex().IsMatch(block.SourceSelector ?? string.Empty))
            {
                block.Trust = Boilerplate;
            }
            // else: normal main content — leave null (omitted).
        }
    }

    // Injection-shape: text that addresses the reader/model or issues meta-instructions. High-signal,
    // conservative (aims to avoid flagging ordinary prose that merely uses "you"/"instructions").
    [GeneratedRegex(
        @"\b(ignore|disregard|forget)\b[^.\n]{0,40}\b(previous|above|prior|earlier|all)\b[^.\n]{0,20}\b(instruction|prompt|rule|context|message)s?\b"
        + @"|\byou\s+are\s+(now|a|an)\b[^.\n]{0,40}\b(assistant|ai|model|system|dan)\b"
        + @"|\b(system|assistant|developer)\s*:\s"
        + @"|\bnew\s+(instruction|rule|task)s?\b\s*:"
        + @"|\bas\s+an\s+ai\b"
        + @"|\boverride\b[^.\n]{0,20}\b(previous|prior|system|safety)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveRegex();

    // Non-content regions that occasionally leak into the extracted root.
    [GeneratedRegex(
        @"(^|[ >._#\-\[])(nav|navbar|footer|aside|sidebar|comment|comments|menu|related|promo|advert|advertisement|banner|cookie|newsletter|social|share)([ >._#\-\]:]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoilerplateRegionRegex();
}
