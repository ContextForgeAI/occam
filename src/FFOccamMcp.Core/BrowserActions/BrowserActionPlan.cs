using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OccamMcp.Core.BrowserActions;

/// <summary>Strict pre-validation for <c>occam_browser_interact</c> MCP actions (mirrors worker).</summary>
public static class BrowserActionPlan
{
    public const int MaxActions = 16;
    public const int DefaultDeadlineMs = 90_000;

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "wait", "wait_selector", "wait_text", "click", "hover", "type", "press", "scroll",
    };

    public sealed record ValidatedAction(
        string Do,
        string? Selector,
        string? Text,
        string? Key,
        string? To,
        int? Ms,
        int? Px,
        int TimeoutMs);

    public sealed record ValidationResult(
        bool Ok,
        string? FailureCode,
        string? Message,
        IReadOnlyList<ValidatedAction>? Actions);

    public static ValidationResult Validate(JsonElement actionsElement)
    {
        if (actionsElement.ValueKind != JsonValueKind.Array)
        {
            return new ValidationResult(false, "invalid_arguments", "actions must be a JSON array.", null);
        }

        var len = actionsElement.GetArrayLength();
        if (len == 0)
        {
            return new ValidationResult(false, "invalid_arguments", "actions must not be empty.", null);
        }

        if (len > MaxActions)
        {
            return new ValidationResult(
                false,
                "invalid_arguments",
                $"actions length {len} exceeds max {MaxActions}.",
                null);
        }

        var list = new List<ValidatedAction>(len);
        var i = 0;
        foreach (var entry in actionsElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                return new ValidationResult(false, "invalid_arguments", $"actions[{i}] must be an object.", null);
            }

            if (entry.TryGetProperty("js", out _)
                || entry.TryGetProperty("js_before_wait", out _)
                || entry.TryGetProperty("evaluate", out _))
            {
                return new ValidationResult(
                    false,
                    "invalid_arguments",
                    $"actions[{i}] must not include js/evaluate fields.",
                    null);
            }

            if (!entry.TryGetProperty("do", out var doEl) || doEl.ValueKind != JsonValueKind.String)
            {
                return new ValidationResult(false, "invalid_arguments", $"actions[{i}].do is required.", null);
            }

            var doName = doEl.GetString()!.Trim().ToLowerInvariant();
            if (!Allowed.Contains(doName))
            {
                return new ValidationResult(
                    false,
                    "invalid_arguments",
                    $"actions[{i}].do=\"{doName}\" is not allowed.",
                    null);
            }

            var timeoutMs = ReadInt(entry, "timeout_ms", 8_000, 50, 60_000);
            string? selector = ReadString(entry, "selector");
            string? text = ReadString(entry, "text");
            string? key = ReadString(entry, "key");
            string? to = ReadString(entry, "to")?.ToLowerInvariant();
            int? ms = entry.TryGetProperty("ms", out _) ? ReadInt(entry, "ms", 500, 50, 60_000) : null;
            int? px = entry.TryGetProperty("px", out _) ? ReadInt(entry, "px", 400, 1, 10_000) : null;

            switch (doName)
            {
                case "wait":
                    list.Add(new ValidatedAction(doName, null, null, null, null, ms ?? 500, null, timeoutMs));
                    break;
                case "wait_selector":
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        return Fail(i, "selector is required for wait_selector");
                    }

                    list.Add(new ValidatedAction(doName, selector, null, null, null, null, null, timeoutMs));
                    break;
                case "wait_text":
                    if (string.IsNullOrEmpty(text))
                    {
                        return Fail(i, "text is required for wait_text");
                    }

                    list.Add(new ValidatedAction(doName, null, text, null, null, null, null, timeoutMs));
                    break;
                case "click":
                    if (string.IsNullOrWhiteSpace(selector) && string.IsNullOrEmpty(text))
                    {
                        return Fail(i, "click requires selector or text");
                    }

                    list.Add(new ValidatedAction(doName, selector, text, null, null, null, null, timeoutMs));
                    break;
                case "hover":
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        return Fail(i, "selector is required for hover");
                    }

                    list.Add(new ValidatedAction(doName, selector, null, null, null, null, null, timeoutMs));
                    break;
                case "type":
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        return Fail(i, "selector is required for type");
                    }

                    list.Add(new ValidatedAction(doName, selector, text ?? "", null, null, null, null, timeoutMs));
                    break;
                case "press":
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        return Fail(i, "key is required for press");
                    }

                    list.Add(new ValidatedAction(doName, null, null, key, null, null, null, timeoutMs));
                    break;
                case "scroll":
                    to = to is "top" or "bottom" or "down" ? to : "down";
                    list.Add(new ValidatedAction(doName, null, null, null, to, null, px ?? 400, timeoutMs));
                    break;
            }

            i++;
        }

        return new ValidationResult(true, null, null, list);
    }

    public static string PlanHash(IReadOnlyList<ValidatedAction> actions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var a in actions)
            {
                writer.WriteStartObject();
                writer.WriteString("do", a.Do);
                if (a.Selector is not null) writer.WriteString("selector", a.Selector);
                if (a.Do == "type")
                {
                    writer.WriteString("text", "***");
                    writer.WriteNumber("text_len", a.Text?.Length ?? 0);
                }
                else if (a.Text is not null && a.Do != "type")
                {
                    writer.WriteString("text", a.Text);
                }

                if (a.Key is not null) writer.WriteString("key", a.Key);
                if (a.To is not null) writer.WriteString("to", a.To);
                if (a.Ms is not null) writer.WriteNumber("ms", a.Ms.Value);
                if (a.Px is not null) writer.WriteNumber("px", a.Px.Value);
                writer.WriteNumber("timeout_ms", a.TimeoutMs);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        var hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string SerializeForWorker(IReadOnlyList<ValidatedAction> actions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var a in actions)
            {
                writer.WriteStartObject();
                writer.WriteString("do", a.Do);
                if (a.Selector is not null) writer.WriteString("selector", a.Selector);
                if (a.Text is not null) writer.WriteString("text", a.Text);
                if (a.Key is not null) writer.WriteString("key", a.Key);
                if (a.To is not null) writer.WriteString("to", a.To);
                if (a.Ms is not null) writer.WriteNumber("ms", a.Ms.Value);
                if (a.Px is not null) writer.WriteNumber("px", a.Px.Value);
                writer.WriteNumber("timeout_ms", a.TimeoutMs);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ValidationResult Fail(int index, string detail) =>
        new(false, "invalid_arguments", $"actions[{index}] {detail}.", null);

    private static string? ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int ReadInt(JsonElement entry, string name, int fallback, int min, int max)
    {
        if (!entry.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        var v = el.GetInt32();
        return Math.Clamp(v, min, max);
    }
}
