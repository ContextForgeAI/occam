using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

internal static class PlaybookLessonExporter
{
    public static JsonElement? ExportRedactedLessons(JsonElement lessons, Func<string?, bool> shouldRedactHost)
    {
        if (lessons.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var export = new List<JsonElement>();
        foreach (var lesson in lessons.EnumerateArray().Take(10))
        {
            if (lesson.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            export.Add(PlaybookJsonElementWriter.RedactLessonHost(lesson, shouldRedactHost));
        }

        return export.Count == 0 ? null : PlaybookJsonElementWriter.CreateArray(export);
    }
}
