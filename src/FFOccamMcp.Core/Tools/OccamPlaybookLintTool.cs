using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using OccamMcp.Core.Playbooks;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-13 — playbook lint: statically validate a playbook / genome JSON against the 1.x schema without
/// fetching anything. Returns a graded issue list (errors that break resolve/save, warnings that degrade
/// quality, info nudges) so an agent authoring a recipe in the heal loop — or an operator vetting a
/// community genome — can fix it before paying for a live verify. Pure and deterministic.
/// </summary>
[McpServerToolType]
public sealed class OccamPlaybookLintTool
{
    [McpServerTool(Name = "occam_playbook_lint"), Description("Statically validate a playbook/genome JSON against the 1.x schema (no network). Returns { grade: ready|usable|broken, agentReady, errors, warnings, infos, issues:[{severity, field, code, message}] }. Errors break resolve/save (missing schema_version/id/hosts/extract.contentSelectors); warnings degrade quality (bad backend, non-bare host, unrouted knowledge_schema class). Use before a live playbook_save. Param: playbook_json.")]
    public string Lint(
        [Description("The playbook / genome JSON to validate (a JSON object).")] string playbook_json,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var report = PlaybookLinter.Lint(playbook_json);
        return JsonSerializer.Serialize(report, OccamPlaybookLintJsonContext.Default.PlaybookLintReport);
    }
}

[JsonSerializable(typeof(PlaybookLintReport))]
[JsonSerializable(typeof(PlaybookLintIssue))]
[JsonSerializable(typeof(PlaybookLintIssue[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamPlaybookLintJsonContext : JsonSerializerContext;
