namespace OccamMcp.Core.Playbooks;

/// <summary>Thread-local draft playbook overlay path for save-time dry-run verify.</summary>
public sealed class PlaybookVerifyScope : IDisposable
{
    private static readonly AsyncLocal<string?> CurrentPath = new();
    private static readonly AsyncLocal<bool> CurrentStrict = new();
    private static readonly AsyncLocal<string?> CurrentJson = new();

    public static string? ActivePath => CurrentPath.Value;

    /// <summary>A3: the raw genome JSON of the active overlay, sent inline to the browser daemon so the
    /// warm pool applies it without the temp-file/shared-fs coupling the one-shot CLI path needs.</summary>
    public static string? ActiveJson => CurrentJson.Value;

    /// <summary>
    /// True when the active overlay is a save-verify draft (selector-only, no Readability fallback).
    /// False for an auto-resolved genome overlay, which only supplies selectors/postMarkdown.
    /// </summary>
    public static bool ActiveStrict => CurrentStrict.Value;

    private readonly string? _previousPath;
    private readonly bool _previousStrict;
    private readonly string? _previousJson;
    private readonly string? _tempFile;

    private PlaybookVerifyScope(string tempFile, string playbookJson, bool strict)
    {
        _tempFile = tempFile;
        _previousPath = CurrentPath.Value;
        _previousStrict = CurrentStrict.Value;
        _previousJson = CurrentJson.Value;
        CurrentPath.Value = tempFile;
        CurrentStrict.Value = strict;
        CurrentJson.Value = playbookJson;
    }

    public static PlaybookVerifyScope Push(string playbookJson, bool strict = true)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"occam-playbook-verify-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempFile, playbookJson);
        return new PlaybookVerifyScope(tempFile, playbookJson, strict);
    }

    public void Dispose()
    {
        CurrentPath.Value = _previousPath;
        CurrentStrict.Value = _previousStrict;
        CurrentJson.Value = _previousJson;
        if (_tempFile is null)
        {
            return;
        }

        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
