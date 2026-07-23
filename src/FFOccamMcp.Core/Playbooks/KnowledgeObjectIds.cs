using System.Security.Cryptography;
using System.Text;

namespace OccamMcp.Core.Playbooks;

public static class KnowledgeObjectIds
{
    public const string CompileProfile = "knowledge";
    public const string CompilerRevision = "0.8.6-pb4b-extract";

    public static string ComputeKoId(string url) =>
        ComputeKoId(url, CompileProfile, CompilerRevision);

    public static string ComputeKoId(
        string url,
        string compileProfile,
        string compilerRev)
    {
        var normalized = NormalizeUrl(url);
        var payload = $"{normalized}|{compileProfile}|{compilerRev}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim().ToLowerInvariant();
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty, Query = string.Empty };
        return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
    }
}
