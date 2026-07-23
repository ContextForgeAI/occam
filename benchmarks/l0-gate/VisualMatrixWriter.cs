using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.L0Gate;

[JsonSerializable(typeof(VisualMatrixIndexPayload))]
[JsonSerializable(typeof(VisualMatrixIndexCase))]
[JsonSerializable(typeof(List<VisualMatrixIndexCase>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class VisualMatrixWriterJsonContext : JsonSerializerContext;

internal sealed record VisualMatrixIndexCase(
    string Id,
    string Category,
    string CategoryLabel,
    string CaseDir,
    string Url,
    bool Pass,
    bool ExpectOk,
    string[] Failures,
    string? Notes,
    string PrimaryJson,
    string? Markdown,
    string? ProbeJson,
    string MetaJson,
    Dictionary<string, string> Files);

internal sealed record VisualMatrixIndexPayload(
    string RunId,
    string GeneratedAt,
    int CaseCount,
    int PassCount,
    List<VisualMatrixIndexCase> Cases);

internal static class VisualMatrixWriter
{
    public static string CreateRunDirectory()
    {
        var root = L0ArtifactWriter.ResolveArtifactsRoot();
        var runId = DateTimeOffset.Now.ToString("yyyy-MM-dd_HHmmss");
        var dir = Path.Combine(root, runId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void WriteCaseArtifacts(string runDir, VisualMatrixCaseResult result)
    {
        var casePath = Path.Combine(runDir, result.CaseDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(casePath);

        if (result.Case.Category.Equals("occam_probe", StringComparison.OrdinalIgnoreCase)
            && result.ProbeJson is not null)
        {
            WriteUtf8(Path.Combine(casePath, "response.json"), PrettyJson(result.ProbeJson));
        }
        else if (result.Case.Category.Equals("occam_transcode", StringComparison.OrdinalIgnoreCase)
                 && result.TranscodeJson is not null)
        {
            WriteUtf8(Path.Combine(casePath, "response.json"), PrettyJson(result.TranscodeJson));
            if (!string.IsNullOrWhiteSpace(result.Markdown))
            {
                WriteUtf8(Path.Combine(casePath, "output.md"), result.Markdown);
            }
        }
        else if (result.Case.Category.Equals("recipe_a", StringComparison.OrdinalIgnoreCase))
        {
            if (result.ProbeJson is not null)
            {
                WriteUtf8(Path.Combine(casePath, "01-probe.json"), PrettyJson(result.ProbeJson));
            }

            if (result.TranscodeJson is not null)
            {
                WriteUtf8(Path.Combine(casePath, "02-transcode.json"), PrettyJson(result.TranscodeJson));
            }

            if (!string.IsNullOrWhiteSpace(result.Markdown))
            {
                WriteUtf8(Path.Combine(casePath, "02-transcode.md"), result.Markdown);
            }
        }

        var meta = BuildMeta(result);
        WriteUtf8(
            Path.Combine(casePath, "meta.json"),
            JsonSerializer.Serialize(meta, VisualMatrixJsonContext.Default.VisualMatrixMetaDto));
    }

    public static string WriteMasterIndex(string runDir, IReadOnlyList<VisualMatrixCaseResult> results)
    {
        var runId = Path.GetFileName(runDir);
        var indexCases = results.Select(r =>
        {
            var primary = r.TranscodeJson ?? r.ProbeJson ?? "{}";
            var meta = BuildMeta(r);
            var metaJson = JsonSerializer.Serialize(meta, VisualMatrixJsonContext.Default.VisualMatrixMetaDto);
            return new VisualMatrixIndexCase(
                r.Case.Id,
                r.Case.Category,
                CategoryLabel(r.Case.Category),
                r.CaseDir.Replace('\\', '/'),
                r.Case.Url,
                r.Pass,
                r.Case.ExpectOk ?? true,
                r.Failures.ToArray(),
                r.Case.Notes,
                primary,
                r.Markdown,
                r.ProbeJson,
                metaJson,
                r.Files.ToDictionary(
                    kv => kv.Key,
                    kv => $"{r.CaseDir.Replace('\\', '/')}/{kv.Value}",
                    StringComparer.Ordinal));
        }).ToList();

        var payload = new VisualMatrixIndexPayload(
            runId,
            DateTimeOffset.Now.ToString("O"),
            results.Count,
            results.Count(r => r.Pass),
            indexCases);

        var casesJson = JsonSerializer.Serialize(payload, VisualMatrixWriterJsonContext.Default.VisualMatrixIndexPayload);
        var dataPath = Path.Combine(runDir, "visual-matrix-data.json");
        WriteUtf8(dataPath, casesJson);
        var html = BuildIndexHtml(runId, payload, casesJson);
        var indexPath = Path.Combine(runDir, "index.html");
        WriteUtf8(indexPath, html);
        return indexPath;
    }

    public static void WriteHowToRead(string runDir, int caseCount)
    {
        var templatePath = ResolveHowToTemplatePath();
        var runId = Path.GetFileName(runDir);
        var text = File.Exists(templatePath)
            ? File.ReadAllText(templatePath, Encoding.UTF8)
            : "# Visual QA\n\nOpen index.html in this folder.\n";

        text = text
            .Replace("{{RUN_ID}}", runId, StringComparison.Ordinal)
            .Replace("{{CASE_COUNT}}", caseCount.ToString(), StringComparison.Ordinal)
            .Replace("{{GENERATED_AT}}", DateTimeOffset.Now.ToString("u"), StringComparison.Ordinal);

        WriteUtf8(Path.Combine(runDir, "HOW-TO-READ.ru.md"), text);
    }

    public static void WriteLatestPointer(string runDir) =>
        L0ArtifactWriter.WriteLatestPointer(runDir);

    private static VisualMatrixMetaDto BuildMeta(VisualMatrixCaseResult result)
    {
        var parameters = new Dictionary<string, object?>();
        if (result.Case.TimeoutMs is not null)
        {
            parameters["timeout_ms"] = result.Case.TimeoutMs;
        }

        if (result.Case.IncludeSocialMeta is not null)
        {
            parameters["include_social_meta"] = result.Case.IncludeSocialMeta;
        }

        if (!string.IsNullOrWhiteSpace(result.Case.Backend))
        {
            parameters["backend_policy"] = result.Case.Backend;
        }

        if (result.Case.MaxTokens is not null)
        {
            parameters["max_tokens"] = result.Case.MaxTokens;
        }

        if (result.Case.FitMarkdown is not null)
        {
            parameters["fit_markdown"] = result.Case.FitMarkdown;
        }

        if (!string.IsNullOrWhiteSpace(result.Case.FocusQuery))
        {
            parameters["focus_query"] = result.Case.FocusQuery;
        }

        if (!string.IsNullOrWhiteSpace(result.Case.ContentSelectors))
        {
            parameters["content_selectors"] = result.Case.ContentSelectors;
        }

        return new VisualMatrixMetaDto
        {
            Id = result.Case.Id,
            Category = result.Case.Category,
            Url = result.Case.Url,
            Notes = result.Case.Notes,
            ExpectOk = result.Case.ExpectOk ?? true,
            Pass = result.Pass,
            Failures = result.Failures.ToArray(),
            Parameters = parameters,
            Files = result.Files.ToDictionary(
                kv => kv.Key,
                kv => $"{result.CaseDir.Replace('\\', '/')}/{kv.Value}",
                StringComparer.Ordinal),
        };
    }

    private static string ResolveHowToTemplatePath()
    {
        var home = Core.Workers.WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var p = Path.Combine(home, "corpora", "visual-matrix.HOW-TO-READ.md");
            if (File.Exists(p))
            {
                return p;
            }

            p = Path.Combine(home, "corpora", "visual-matrix.HOW-TO-READ.ru.md");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "corpora", "visual-matrix.HOW-TO-READ.md");
    }

    private static string CategoryLabel(string category) => category.ToLowerInvariant() switch
    {
        "occam_probe" => "occam_probe",
        "occam_transcode" => "occam_transcode",
        "recipe_a" => "recipe_a (probe → transcode)",
        _ => category,
    };

    private static string PrettyJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static void WriteUtf8(string path, string content) =>
        File.WriteAllText(path, content, Utf8NoBom);

    public static string RegenerateIndexFromRunDir(string runDir)
    {
        var dataPath = Path.Combine(runDir, "visual-matrix-data.json");
        if (!File.Exists(dataPath))
        {
            throw new FileNotFoundException("visual-matrix-data.json not found in run folder.", dataPath);
        }

        var json = File.ReadAllText(dataPath, Utf8NoBom).TrimStart('\uFEFF');
        var payload = JsonSerializer.Deserialize(json, VisualMatrixWriterJsonContext.Default.VisualMatrixIndexPayload)
            ?? throw new InvalidOperationException("Failed to deserialize visual-matrix-data.json.");
        var runId = Path.GetFileName(runDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var html = BuildIndexHtml(runId, payload, json);
        var indexPath = Path.Combine(runDir, "index.html");
        WriteUtf8(indexPath, html);
        return indexPath;
    }

    private static string BuildIndexHtml(string runId, VisualMatrixIndexPayload payload, string casesJson)
    {
        var safeJson = casesJson.Replace("<", "\\u003c", StringComparison.Ordinal);
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.AppendLine("<title>FF-Occam Visual Matrix</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/github-markdown-css@5.8.1/github-markdown-light.min.css\" crossorigin=\"anonymous\"/>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--bg:#0d1117;--panel:#161b22;--border:#30363d;--text:#e6edf3;--muted:#8b949e;--pass:#3fb950;--fail:#f85149;--accent:#58a6ff}");
        sb.AppendLine("*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:var(--bg);color:var(--text)}");
        sb.AppendLine(".layout{display:grid;grid-template-columns:280px 1fr;min-height:100vh}");
        sb.AppendLine("nav{background:var(--panel);border-right:1px solid var(--border);padding:1rem;overflow:auto;position:sticky;top:0;height:100vh}");
        sb.AppendLine("nav h1{font-size:1rem;margin:0 0 .5rem}nav .sub{font-size:.8rem;color:var(--muted);margin-bottom:1rem;line-height:1.4}");
        sb.AppendLine("nav h2{font-size:.75rem;text-transform:uppercase;color:var(--muted);margin:1rem 0 .35rem}");
        sb.AppendLine("nav a{display:block;color:var(--accent);text-decoration:none;font-size:.85rem;padding:.2rem 0}");
        sb.AppendLine("nav a.fail{color:var(--fail)} nav a.pass{color:var(--pass)}");
        sb.AppendLine("main{padding:1.25rem 1.5rem 2rem;overflow:auto}");
        sb.AppendLine(".case{background:var(--panel);border:1px solid var(--border);border-radius:10px;margin:0 0 1.25rem;overflow:hidden}");
        sb.AppendLine(".case.pass{border-left:4px solid var(--pass)}.case.fail{border-left:4px solid var(--fail)}");
        sb.AppendLine(".hdr{padding:1rem 1.1rem;border-bottom:1px solid var(--border)}");
        sb.AppendLine(".hdr h2{margin:0;font-size:1.05rem}.hdr .meta{margin:.4rem 0 0;font-size:.82rem;color:var(--muted);line-height:1.45}");
        sb.AppendLine(".notes{margin:.5rem 0 0;padding:.55rem .7rem;background:#1c2128;border-radius:6px;font-size:.82rem;color:#c9d1d9}");
        sb.AppendLine(".tabs{display:flex;gap:.35rem;padding:.5rem 1rem;border-bottom:1px solid var(--border);flex-wrap:wrap}");
        sb.AppendLine(".tab{border:1px solid var(--border);background:#21262d;color:var(--text);border-radius:6px;padding:.28rem .65rem;font-size:.78rem;cursor:pointer}");
        sb.AppendLine(".tab.active{background:var(--accent);border-color:var(--accent);color:#fff}");
        sb.AppendLine(".pane{padding:0}.pane.hidden{display:none}");
        sb.AppendLine(".markdown-body{background:#fff;color:#1f2328;min-height:120px;padding:1rem 1.25rem;overflow:visible}");
        sb.AppendLine(".json-pane{margin:0;padding:1rem 1.1rem;background:#0d1117;color:#c9d1d9;font-size:.78rem;line-height:1.45;white-space:pre-wrap;word-break:break-word;overflow:visible}");
        sb.AppendLine(".split{display:grid;grid-template-columns:1fr 1fr;gap:0;border-top:1px solid var(--border)}.split .json-pane{border-right:1px solid var(--border)}");
        sb.AppendLine(".file-links{font-size:.8rem;margin-top:.35rem}.file-links a{color:var(--accent);margin-right:.75rem}");
        sb.AppendLine("@media(max-width:900px){.layout{grid-template-columns:1fr}nav{position:relative;height:auto}.split{grid-template-columns:1fr}}");
        sb.AppendLine("</style></head><body><div class=\"layout\">");
        sb.AppendLine("<nav><h1>Visual Matrix</h1>");
        sb.AppendLine($"<p class=\"sub\">Run <code>{HtmlEnc(runId)}</code><br/>{payload.PassCount}/{payload.CaseCount} pass<br/><a href=\"HOW-TO-READ.ru.md\">HOW-TO-READ.ru.md</a></p>");
        sb.AppendLine("<div id=\"nav-links\"></div></nav><main><div id=\"cases\"></div></main></div>");
        sb.AppendLine($"<script type=\"application/json\" id=\"payload\">{safeJson}</script>");
        sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/marked@15.0.7/marked.min.js\" crossorigin=\"anonymous\"></script>");
        sb.AppendLine("<script>");
        sb.AppendLine("function esc(s){return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;');}");
        sb.AppendLine("function renderMd(md){ if(!md) return '<p><em>(empty)</em></p>'; if(typeof marked!=='undefined'&&marked.parse) return marked.parse(md); return '<pre class=\"json-pane\">'+esc(md)+'</pre>'; }");
        sb.AppendLine("(function(){");
        sb.AppendLine("try {");
        sb.AppendLine("if(typeof marked!=='undefined') marked.setOptions({ gfm: true, breaks: false });");
        sb.AppendLine("const data = JSON.parse(document.getElementById('payload').textContent);");
        sb.AppendLine("const cases = data.cases || data.Cases || [];");
        sb.AppendLine("const nav = document.getElementById('nav-links');");
        sb.AppendLine("const root = document.getElementById('cases');");
        sb.AppendLine("function prettyJson(raw){ try { return JSON.stringify(JSON.parse(raw), null, 2); } catch { return String(raw??''); } }");
        sb.AppendLine("function fileLinks(c){ if(!c.files) return ''; return Object.entries(c.files).map(([n,p])=>'<a href=\"'+esc(p)+'\" target=\"_blank\" rel=\"noreferrer\">'+esc(n)+'</a>').join(' '); }");
        sb.AppendLine("const byCat = {}; cases.forEach(c => { const cat = c.category || c.Category; if(!byCat[cat]) byCat[cat]=[]; byCat[cat].push(c); });");
        sb.AppendLine("Object.keys(byCat).forEach(cat => {");
        sb.AppendLine("  const label = byCat[cat][0].categoryLabel || byCat[cat][0].CategoryLabel || cat;");
        sb.AppendLine("  nav.innerHTML += '<h2>' + esc(label) + '</h2>';");
        sb.AppendLine("  byCat[cat].forEach(c => {");
        sb.AppendLine("    const id = c.id || c.Id; const pass = c.pass ?? c.Pass;");
        sb.AppendLine("    nav.innerHTML += '<a class=\"' + (pass?'pass':'fail') + '\" href=\"#case-' + esc(id) + '\">' + esc(id) + '</a>';");
        sb.AppendLine("  });");
        sb.AppendLine("});");
        sb.AppendLine("cases.forEach((c) => {");
        sb.AppendLine("  const id = c.id || c.Id; const pass = c.pass ?? c.Pass; const category = c.category || c.Category;");
        sb.AppendLine("  const categoryLabel = c.categoryLabel || c.CategoryLabel || category;");
        sb.AppendLine("  const url = c.url || c.Url; const expectOk = c.expectOk ?? c.ExpectOk;");
        sb.AppendLine("  const notes = c.notes || c.Notes; const failures = c.failures || c.Failures || [];");
        sb.AppendLine("  const primaryJson = c.primaryJson || c.PrimaryJson || '{}';");
        sb.AppendLine("  const probeJson = c.probeJson || c.ProbeJson; const markdown = c.markdown || c.Markdown;");
        sb.AppendLine("  const metaJson = c.metaJson || c.MetaJson || '{}';");
        sb.AppendLine("  const sec = document.createElement('section');");
        sb.AppendLine("  sec.className = 'case ' + (pass ? 'pass' : 'fail');");
        sb.AppendLine("  sec.id = 'case-' + id;");
        sb.AppendLine("  const note = notes ? '<div class=\"notes\"><strong>Notes:</strong> ' + esc(notes) + '</div>' : '';");
        sb.AppendLine("  const failLine = failures.length ? '<br/>Checks: ' + esc(failures.join('; ')) : '';");
        sb.AppendLine("  const files = fileLinks(c);");
        sb.AppendLine("  sec.innerHTML = `");
        sb.AppendLine("    <div class=\"hdr\"><h2>${esc(id)} &mdash; ${pass ? 'PASS' : 'FAIL'}</h2>");
        sb.AppendLine("    <p class=\"meta\">${esc(categoryLabel)}<br/>URL: <a href=\"${esc(url)}\" target=\"_blank\" rel=\"noreferrer\">${esc(url)}</a>");
        sb.AppendLine("    <br/>expect ok=${expectOk}${failLine}${note}");
        sb.AppendLine("    ${files ? '<div class=\"file-links\">Files: ' + files + '</div>' : ''}</p></div>`;");
        sb.AppendLine("  const tabs = document.createElement('div'); tabs.className = 'tabs';");
        sb.AppendLine("  const panes = document.createElement('div');");
        sb.AppendLine("  function addTab(name, build, active){");
        sb.AppendLine("    const btn = document.createElement('button'); btn.type='button'; btn.className='tab' + (active?' active':''); btn.textContent=name;");
        sb.AppendLine("    tabs.appendChild(btn); const pane = document.createElement('div'); pane.className='pane' + (active?'':' hidden'); build(pane); panes.appendChild(pane);");
        sb.AppendLine("    btn.onclick = () => { tabs.querySelectorAll('.tab').forEach(t=>t.classList.remove('active')); btn.classList.add('active');");
        sb.AppendLine("      panes.querySelectorAll('.pane').forEach((p,i)=>p.classList.toggle('hidden', panes.children[i]!==pane)); };");
        sb.AppendLine("  }");
        sb.AppendLine("  if (category === 'recipe_a') {");
        sb.AppendLine("    addTab('Probe JSON', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = prettyJson(probeJson); }, true);");
        sb.AppendLine("    addTab('Transcode JSON', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = prettyJson(primaryJson); });");
        sb.AppendLine("    addTab('Markdown Preview', p => { p.innerHTML = '<div class=\"markdown-body\"></div>'; const el=p.querySelector('.markdown-body'); el.innerHTML = renderMd(markdown); });");
        sb.AppendLine("    addTab('Markdown Source', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = markdown || ''; });");
        sb.AppendLine("  } else if (markdown) {");
        sb.AppendLine("    addTab('Markdown Preview', p => { p.innerHTML = '<div class=\"markdown-body\"></div>'; p.querySelector('.markdown-body').innerHTML = renderMd(markdown); }, true);");
        sb.AppendLine("    addTab('Markdown Source', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = markdown; });");
        sb.AppendLine("    addTab('JSON Response', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = prettyJson(primaryJson); });");
        sb.AppendLine("  } else {");
        sb.AppendLine("    addTab('JSON Response', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = prettyJson(primaryJson); }, true);");
        sb.AppendLine("  }");
        sb.AppendLine("  addTab('Meta', p => { p.innerHTML = '<pre class=\"json-pane\"></pre>'; p.querySelector('pre').textContent = prettyJson(metaJson); });");
        sb.AppendLine("  sec.appendChild(tabs); sec.appendChild(panes); root.appendChild(sec);");
        sb.AppendLine("});");
        sb.AppendLine("} catch(err) {");
        sb.AppendLine("  document.getElementById('cases').innerHTML = '<section class=\"case fail\"><div class=\"hdr\"><h2>Render error</h2><pre class=\"json-pane\">'+esc(err.message)+'\\n'+esc(err.stack)+'</pre></div></section>';");
        sb.AppendLine("}})();");
        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }

    private static string HtmlEnc(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
