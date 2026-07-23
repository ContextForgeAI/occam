using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;

namespace OccamMcp.L0Gate;

internal sealed record L0CaseArtifact(
    string Id,
    string Url,
    string BackendPolicy,
    bool ExpectOk,
    bool Pass,
    IReadOnlyList<string> Failures,
    TranscodeOutcome Result,
    string MarkdownPath,
    string MetaPath);

[JsonSerializable(typeof(L0CaseArtifactDto))]
[JsonSerializable(typeof(VisualCasePayload))]
[JsonSerializable(typeof(List<VisualCasePayload>))]
internal partial class L0ArtifactJsonContext : JsonSerializerContext;

internal sealed class L0CaseArtifactDto
{
    public string Id { get; init; } = "";
    public string Url { get; init; } = "";
    public string BackendPolicy { get; init; } = "";
    public bool ExpectOk { get; init; }
    public bool Pass { get; init; }
    public string[] Failures { get; init; } = [];
    public bool Ok { get; init; }
    public string? FinalUrl { get; init; }
    public string? Backend { get; init; }
    public string? FailureCode { get; init; }
    public string? Message { get; init; }
    public int MarkdownChars { get; init; }
    public string MarkdownFile { get; init; } = "";
}

internal sealed record VisualCasePayload(
    string Id,
    string Url,
    string Backend,
    bool Pass,
    bool Ok,
    string[] Failures,
    string? FailureCode,
    string? Message,
    string MarkdownFile,
    string Markdown);

internal static class L0ArtifactWriter
{
    private const int ConsoleExcerptChars = 2_400;

    public static string CreateRunDirectory()
    {
        var root = ResolveArtifactsRoot();
        var runId = DateTimeOffset.Now.ToString("yyyy-MM-dd_HHmmss");
        var dir = Path.Combine(root, runId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ResolveArtifactsRoot()
    {
        var home = Core.Workers.WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            return Path.Combine(home, "artifacts", "l0-runs");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "l0-runs");
    }

    public static L0CaseArtifact WriteCase(
        string runDir,
        L0SmokeCase entry,
        TranscodeOutcome result,
        bool pass,
        IReadOnlyList<string> caseFailures)
    {
        var safeId = SanitizeFileName(entry.Id);
        var mdPath = Path.Combine(runDir, $"{safeId}.md");
        var metaPath = Path.Combine(runDir, $"{safeId}.meta.json");

        var markdown = result.Markdown ?? string.Empty;
        File.WriteAllText(mdPath, markdown, Encoding.UTF8);

        var dto = new L0CaseArtifactDto
        {
            Id = entry.Id,
            Url = entry.Url,
            BackendPolicy = entry.Backend ?? "http_then_browser",
            ExpectOk = entry.ExpectOk ?? true,
            Pass = pass,
            Failures = caseFailures.ToArray(),
            Ok = result.Ok,
            FinalUrl = result.FinalUrl,
            Backend = result.Backend,
            FailureCode = result.FailureCode,
            Message = result.Message,
            MarkdownChars = markdown.Length,
            MarkdownFile = Path.GetFileName(mdPath),
        };

        File.WriteAllText(
            metaPath,
            JsonSerializer.Serialize(dto, L0ArtifactJsonContext.Default.L0CaseArtifactDto),
            Encoding.UTF8);

        return new L0CaseArtifact(
            entry.Id,
            entry.Url,
            entry.Backend ?? "http_then_browser",
            entry.ExpectOk ?? true,
            pass,
            caseFailures,
            result,
            mdPath,
            metaPath);
    }

    public static void PrintConsoleExcerpt(L0CaseArtifact artifact)
    {
        var status = artifact.Pass ? "PASS" : "FAIL";
        Console.WriteLine();
        Console.WriteLine($"========== {artifact.Id} [{status}] ==========");
        Console.WriteLine($"url:     {artifact.Url}");
        Console.WriteLine($"backend: {artifact.Result.Backend ?? artifact.BackendPolicy}");
        Console.WriteLine($"ok:      {artifact.Result.Ok} (expected {artifact.ExpectOk})");
        if (!string.IsNullOrWhiteSpace(artifact.Result.FinalUrl))
        {
            Console.WriteLine($"final:   {artifact.Result.FinalUrl}");
        }

        if (artifact.Failures.Count > 0)
        {
            Console.WriteLine($"checks:  {string.Join("; ", artifact.Failures)}");
        }

        if (!string.IsNullOrWhiteSpace(artifact.Result.FailureCode))
        {
            Console.WriteLine($"failure: {artifact.Result.FailureCode} — {artifact.Result.Message}");
        }

        var md = artifact.Result.Markdown ?? string.Empty;
        if (md.Length > 0)
        {
            var excerpt = md.Length <= ConsoleExcerptChars
                ? md
                : md[..ConsoleExcerptChars] + "\n\n… [truncated in console; see .md file]";
            Console.WriteLine("--- markdown ---");
            Console.WriteLine(excerpt);
        }
        else
        {
            Console.WriteLine("--- markdown: (empty) ---");
        }

        Console.WriteLine($"--- file: {artifact.MarkdownPath} ---");
    }

    public static string WriteIndexHtml(string runDir, IReadOnlyList<L0CaseArtifact> artifacts)
    {
        var casePayload = artifacts.Select(a => new VisualCasePayload(
            a.Id,
            a.Url,
            a.Result.Backend ?? a.BackendPolicy,
            a.Pass,
            a.Result.Ok,
            a.Failures.ToArray(),
            a.Result.FailureCode,
            a.Result.Message,
            Path.GetFileName(a.MarkdownPath),
            a.Result.Markdown ?? string.Empty)).ToList();

        var casesJson = JsonSerializer.Serialize(casePayload, L0ArtifactJsonContext.Default.ListVisualCasePayload);
        var runLabel = HtmlEncode(Path.GetFileName(runDir));

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.AppendLine("<title>FFOccam L0 visual run</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/github-markdown-css@5.8.1/github-markdown-light.min.css\" crossorigin=\"anonymous\"/>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--bg:#f4f6f8;--card:#fff;--border:#d0d7de;--pass:#1a7f37;--fail:#cf222e;--muted:#57606a}");
        sb.AppendLine("body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;margin:0;background:var(--bg);color:#1f2328}");
        sb.AppendLine(".wrap{max-width:960px;margin:0 auto;padding:1.25rem 1.5rem 2rem}");
        sb.AppendLine("h1{font-size:1.35rem;margin:0 0 .25rem}.meta{color:var(--muted);font-size:.9rem;margin:0 0 1rem}");
        sb.AppendLine(".case{background:var(--card);border:1px solid var(--border);border-radius:10px;margin:1rem 0;overflow:hidden}");
        sb.AppendLine(".case.pass{border-left:4px solid var(--pass)}.case.fail{border-left:4px solid var(--fail)}");
        sb.AppendLine(".case-hdr{padding:.85rem 1rem;border-bottom:1px solid var(--border)}");
        sb.AppendLine(".case-hdr h2{margin:0;font-size:1.05rem}.case-hdr .sub{margin:.35rem 0 0;font-size:.85rem;color:var(--muted)}");
        sb.AppendLine(".tabs{display:flex;gap:.35rem;padding:.5rem 1rem;border-bottom:1px solid var(--border);background:#f6f8fa}");
        sb.AppendLine(".tab{border:1px solid var(--border);background:#fff;border-radius:6px;padding:.3rem .75rem;font-size:.82rem;cursor:pointer}");
        sb.AppendLine(".tab.active{background:#0969da;color:#fff;border-color:#0969da}");
        sb.AppendLine(".pane{padding:0}.pane.hidden{display:none}");
        sb.AppendLine(".markdown-body{box-sizing:border-box;min-height:120px;max-height:70vh;overflow:auto;padding:1rem 1.25rem}");
        sb.AppendLine(".markdown-body pre{background:#f6f8fa!important}");
        sb.AppendLine(".source{margin:0;padding:1rem 1.25rem;max-height:70vh;overflow:auto;background:#f6f8fa;font-size:.8rem;line-height:1.45;white-space:pre-wrap;word-break:break-word}");
        sb.AppendLine(".toolbar{display:flex;gap:.5rem;align-items:center;margin-bottom:1rem;flex-wrap:wrap}");
        sb.AppendLine(".toolbar button{border:1px solid var(--border);background:#fff;border-radius:6px;padding:.35rem .7rem;cursor:pointer;font-size:.82rem}");
        sb.AppendLine("a{color:#0969da}</style></head><body><div class=\"wrap\">");
        sb.AppendLine("<h1>FFOccam L0 — live extract</h1>");
        sb.AppendLine($"<p class=\"meta\">Run: <code>{runLabel}</code> · {artifacts.Count} cases · rendered with marked.js</p>");
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine("<button type=\"button\" id=\"btn-all-preview\">Все: Preview</button>");
        sb.AppendLine("<button type=\"button\" id=\"btn-all-source\">Все: Source</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div id=\"cases-root\"></div>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<script type=\"application/json\" id=\"cases-data\">{casesJson}</script>");
        sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/marked@15.0.7/marked.min.js\" crossorigin=\"anonymous\"></script>");
        sb.AppendLine("<script>");
        sb.AppendLine("marked.setOptions({ gfm: true, breaks: false });");
        sb.AppendLine("const cases = JSON.parse(document.getElementById('cases-data').textContent);");
        sb.AppendLine("const root = document.getElementById('cases-root');");
        sb.AppendLine("function esc(s){return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;');}");
        sb.AppendLine("cases.forEach((c, i) => {");
        sb.AppendLine("  const sec = document.createElement('section');");
        sb.AppendLine("  sec.className = 'case ' + (c.pass ? 'pass' : 'fail');");
        sb.AppendLine("  sec.dataset.caseIdx = String(i);");
        sb.AppendLine("  const failLine = c.failures?.length ? '<br/>Checks: ' + esc(c.failures.join('; ')) : '';");
        sb.AppendLine("  const errLine = c.failureCode ? '<br/>Failure: ' + esc(c.failureCode) + (c.message ? ' — ' + esc(c.message) : '') : '';");
        sb.AppendLine("  sec.innerHTML = `");
        sb.AppendLine("    <div class=\"case-hdr\">");
        sb.AppendLine("      <h2>${esc(c.id)} — ${c.pass ? 'PASS' : 'FAIL'}</h2>");
        sb.AppendLine("      <p class=\"sub\">URL: <a href=\"${esc(c.url)}\" target=\"_blank\" rel=\"noreferrer\">${esc(c.url)}</a><br/>");
        sb.AppendLine("      Backend: ${esc(c.backend)} · ok=${c.ok} · ${c.markdown.length} chars · <a href=\"${esc(c.markdownFile)}\">.md</a>${failLine}${errLine}</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"tabs\">");
        sb.AppendLine("      <button type=\"button\" class=\"tab active\" data-tab=\"preview\">Preview</button>");
        sb.AppendLine("      <button type=\"button\" class=\"tab\" data-tab=\"source\">Source</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"pane pane-preview\"><div class=\"markdown-body\"></div></div>");
        sb.AppendLine("    <div class=\"pane pane-source hidden\"><pre class=\"source\"></pre></div>`;");
        sb.AppendLine("  const preview = sec.querySelector('.markdown-body');");
        sb.AppendLine("  const source = sec.querySelector('.source');");
        sb.AppendLine("  preview.innerHTML = c.markdown ? marked.parse(c.markdown) : '<p><em>(empty)</em></p>';");
        sb.AppendLine("  source.textContent = c.markdown || '';");
        sb.AppendLine("  sec.querySelectorAll('.tab').forEach(btn => {");
        sb.AppendLine("    btn.addEventListener('click', () => {");
        sb.AppendLine("      const tab = btn.dataset.tab;");
        sb.AppendLine("      sec.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tab));");
        sb.AppendLine("      sec.querySelector('.pane-preview').classList.toggle('hidden', tab !== 'preview');");
        sb.AppendLine("      sec.querySelector('.pane-source').classList.toggle('hidden', tab !== 'source');");
        sb.AppendLine("    });");
        sb.AppendLine("  });");
        sb.AppendLine("  root.appendChild(sec);");
        sb.AppendLine("});");
        sb.AppendLine("function setAll(tab){document.querySelectorAll('.case').forEach(sec => {");
        sb.AppendLine("  sec.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tab));");
        sb.AppendLine("  sec.querySelector('.pane-preview').classList.toggle('hidden', tab !== 'preview');");
        sb.AppendLine("  sec.querySelector('.pane-source').classList.toggle('hidden', tab !== 'source');");
        sb.AppendLine("});}");
        sb.AppendLine("document.getElementById('btn-all-preview').onclick = () => setAll('preview');");
        sb.AppendLine("document.getElementById('btn-all-source').onclick = () => setAll('source');");
        sb.AppendLine("</script></body></html>");

        var indexPath = Path.Combine(runDir, "index.html");
        File.WriteAllText(indexPath, sb.ToString(), Encoding.UTF8);
        return indexPath;
    }

    public static void WriteLatestPointer(string runDir)
    {
        var root = ResolveArtifactsRoot();
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "LATEST.txt"), runDir, Encoding.UTF8);
    }

    private static string SanitizeFileName(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(id.Length);
        foreach (var ch in id)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return sb.Length > 0 ? sb.ToString() : "case";
    }

    private static string HtmlEncode(string text) =>
        text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
