using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2MediaRefsUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunMapper(assert);
        RunWorkerJson(assert);
    }

    private static void RunMapper(Action<string, bool> assert)
    {
        var mapped = MediaRefMapper.Map(
        [
            new WorkerMediaRefInfo
            {
                Url = "https://example.com/a.png",
                Kind = "image",
                Alt = "diagram",
                ContextHeading = "## Architecture",
                SelectorHint = "img",
            },
            new WorkerMediaRefInfo
            {
                Url = "",
                Kind = "image",
            },
        ]);

        assert("media refs mapper count", mapped.Count == 1);
        assert("media refs mapper url", mapped[0].Url.Contains("a.png", StringComparison.Ordinal));
        assert("media refs mapper kind", mapped[0].Kind == "image");
        assert("media refs mapper heading", mapped[0].ContextHeading == "## Architecture");
    }

    private static void RunWorkerJson(Action<string, bool> assert)
    {
        const string json = """
            {
              "ok": true,
              "backend": "node_readability_turndown",
              "markdown": "# Test",
              "media_refs": [
                {
                  "url": "https://kubernetes.io/assets/pod.png",
                  "kind": "image",
                  "alt": "Pod",
                  "context_heading": "## Pods",
                  "selector_hint": "img"
                }
              ],
              "latency_ms": 12
            }
            """;

        var payload = JsonSerializer.Deserialize(json, WorkerExtractJsonContext.Default.WorkerExtractResponse);
        assert("media refs worker json ok", payload?.Ok == true);
        var refs = MediaRefMapper.Map(payload?.MediaRefs);
        assert("media refs worker json mapped", refs.Count == 1);
        assert("media refs worker json alt", refs[0].Alt == "Pod");
    }
}
