using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

/// <summary>Parse community manifest.json and verify sha256 of playbook file bytes at load.</summary>
public static class CommunityManifest
{
    public static string ComputeSha256Hex(string utf8Content)
    {
        var normalized = utf8Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool FileSha256Matches(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64)
        {
            return false;
        }

        var json = File.ReadAllText(filePath);
        var actual = ComputeSha256Hex(json);
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, string>? TryLoadFileHashes(string communityDirectory)
    {
        var manifestPath = Path.Combine(communityDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            if (!root.TryGetProperty("playbooks", out var playbooks) || playbooks.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in playbooks.EnumerateArray())
            {
                if (!row.TryGetProperty("file", out var fileEl))
                {
                    continue;
                }

                var fileName = fileEl.GetString();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (!row.TryGetProperty("sha256", out var shaEl))
                {
                    continue;
                }

                var sha = shaEl.GetString();
                if (string.IsNullOrWhiteSpace(sha))
                {
                    continue;
                }

                map[fileName] = sha.Trim();
            }

            return map;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Returns UTF-8 file text when manifest row sha256 matches and hygiene passes.</summary>
    public static bool TryReadVerifiedPlaybook(
        string filePath,
        string fileName,
        IReadOnlyDictionary<string, string> fileHashes,
        out string? json)
    {
        json = null;
        if (!fileHashes.TryGetValue(fileName, out var expectedSha))
        {
            return false;
        }

        if (!File.Exists(filePath))
        {
            return false;
        }

        json = File.ReadAllText(filePath);
        if (!string.Equals(ComputeSha256Hex(json), expectedSha.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            json = null;
            return false;
        }

        if (PlaybookCommunityHygiene.ContainsForbiddenKeys(json))
        {
            json = null;
            return false;
        }

        return true;
    }
}
