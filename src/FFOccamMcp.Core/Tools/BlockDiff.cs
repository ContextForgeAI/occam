using System.Security.Cryptography;
using System.Text;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Tools;

/// <summary>
/// diff-codec: block-level delta. Each content block is hashed (type + normalized text — text is
/// already whitespace-normalized by the worker block pass). The agent sends prior block hashes
/// (`diff_against`); we return new/changed blocks (full content), removed hashes, and the current
/// hash list to store for next time. Whole-doc `if_none_match` is the cheap boolean gate; this is
/// the "what changed" delta.
/// </summary>
internal static class BlockDiff
{
    /// <summary>Stable short hash for a block (16 hex chars of SHA256 over type+text).</summary>
    public static string Hash(WorkerExtractBlockInfo block)
    {
        var bytes = Encoding.UTF8.GetBytes($"{block.Type}{block.Text}");
        return Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
    }

    public static OccamTranscodeDiffInfo Compute(
        IReadOnlyList<WorkerExtractBlockInfo> blocks,
        IReadOnlyList<string> priorHashes)
    {
        var prior = new HashSet<string>(priorHashes, StringComparer.OrdinalIgnoreCase);
        var current = new List<string>(blocks.Count);
        var currentSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = new List<OccamTranscodeDiffBlockInfo>();

        foreach (var b in blocks)
        {
            var h = Hash(b);
            current.Add(h);
            currentSet.Add(h);
            if (!prior.Contains(h))
            {
                added.Add(new OccamTranscodeDiffBlockInfo(h, b.Type, b.Text, b.SourceSelector));
            }
        }

        var removed = priorHashes
            .Where(h => !currentSet.Contains(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new OccamTranscodeDiffInfo([.. added], removed, [.. current]);
    }
}
