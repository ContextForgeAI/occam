using System.Text.Json;
using OccamMcp.Core.Json;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_MD_ESCAPES — wire markdown must not carry STJ printable unicode escapes
/// (\u003E / \u0022 / \u0027). HTML-sensitive &lt; and &amp; stay escaped (\u003C / \u0026).
/// </summary>
public static class MarkdownPrintableEscapesUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        const string markdown =
            """
            > blockquote with "quotes" and 'apostrophes'

            ```json
            {"key": "value", "flag": true}
            ```

            Inline `code > 0` and HTML fragment <span class="x">hi</span>.

            | Col | Val |
            | --- | --- |
            | a > b | "quoted" |

            And &amp; entity stays text.
            """;

        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
            markdown,
            "node_readability_turndown",
            [],
            Blocks:
            [
                new WorkerExtractBlockInfo { Type = "quote", Text = "> blockquote with \"quotes\" and 'apostrophes'" },
                new WorkerExtractBlockInfo { Type = "code", Text = "{\"key\": \"value\", \"flag\": true}" },
                new WorkerExtractBlockInfo { Type = "paragraph", Text = "Inline `code > 0` and HTML fragment <span class=\"x\">hi</span>." },
                new WorkerExtractBlockInfo { Type = "table", Text = "| Col | Val |\n| --- | --- |\n| a > b | \"quoted\" |" },
            ]);

        var raw = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("raw STJ escapes > as \\u003E", raw.Contains("\\u003E", StringComparison.Ordinal));
        assert("raw STJ escapes quote as \\u0022", raw.Contains("\\u0022", StringComparison.Ordinal));
        assert("raw STJ escapes apostrophe as \\u0027", raw.Contains("\\u0027", StringComparison.Ordinal));

        var wire = OccamJsonPrintableEscapes.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("wire blockquote keeps >", wire.Contains("> blockquote", StringComparison.Ordinal));
        assert("wire has no \\u003E", !wire.Contains("\\u003E", StringComparison.Ordinal));
        assert("wire has no \\u0027", !wire.Contains("\\u0027", StringComparison.Ordinal));
        assert("wire has no \\u0022", !wire.Contains("\\u0022", StringComparison.Ordinal));
        assert("wire JSON code fence keeps braces",
            wire.Contains("{\"key\":", StringComparison.Ordinal) || wire.Contains("{\\\"key\\\":", StringComparison.Ordinal));
        assert("wire inline code keeps >", wire.Contains("code > 0", StringComparison.Ordinal));
        assert("wire table cell keeps >", wire.Contains("a > b", StringComparison.Ordinal));
        assert("wire still escapes < as \\u003C", wire.Contains("\\u003C", StringComparison.Ordinal));
        assert("wire still escapes & as \\u0026", wire.Contains("\\u0026", StringComparison.Ordinal));

        var roundTrip = JsonSerializer.Deserialize(wire, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("printable escapes round-trip markdown", roundTrip?.Markdown == markdown);
        assert("printable escapes round-trip blockquote block",
            roundTrip?.Blocks is { Length: > 0 } blocks && blocks[0].Text == response.Blocks![0].Text);
        assert("printable escapes round-trip code block",
            roundTrip?.Blocks is { Length: > 1 } && roundTrip.Blocks[1].Text == response.Blocks![1].Text);
        assert("printable escapes round-trip html fragment block",
            roundTrip?.Blocks is { Length: > 2 } && roundTrip.Blocks[2].Text == response.Blocks![2].Text);
        assert("printable escapes round-trip table block",
            roundTrip?.Blocks is { Length: > 3 } && roundTrip.Blocks[3].Text == response.Blocks![3].Text);

        var twice = OccamJsonPrintableEscapes.Relax(wire);
        assert("printable escapes idempotent", twice == wire);

        Console.WriteLine("L_MD_ESCAPES_OK");
    }
}
