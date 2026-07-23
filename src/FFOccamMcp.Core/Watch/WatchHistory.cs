using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Watch;

/// <summary>
/// SI-05: one entry in a page's signed change history. Appended only on a real event (first_seen,
/// changed). Each entry carries the hash of the previous SIGNED entry (<see cref="PrevEntryHash"/>),
/// so the entries form a tamper-evident chain: reorder / insert / drop / edit all break a link. The
/// detached <see cref="Sig"/> (ECDsa P-256, the Receipt v1 key) covers the canonical entry without the
/// signature; the chain link covers it WITH the signature, so the signatures are pinned too.
/// </summary>
public sealed record WatchHistoryEntry(
    int Seq,
    string ObservedAt,
    string Event,
    string ContentHash,
    string? BlockMerkleRoot,
    int? ContentDeltaTokens,
    string? PrevEntryHash,
    string KeyId,
    string Alg,
    string? Sig)
{
    public const string EventFirstSeen = "first_seen";
    public const string EventChanged = "changed";
}

/// <summary>
/// Fixed-field-order canonical bytes for a history entry (AOT-safe, immune to serializer drift) — the
/// same discipline as <see cref="ReceiptCanonicalizer"/>. Null optional fields are omitted so an
/// unsigned entry and a signed entry hash consistently.
/// </summary>
public static class WatchHistoryCanonicalizer
{
    public static byte[] CanonicalBytes(WatchHistoryEntry e, bool includeSig)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("seq", e.Seq);
            w.WriteString("observedAt", e.ObservedAt);
            w.WriteString("event", e.Event);
            w.WriteString("contentHash", e.ContentHash);
            if (e.BlockMerkleRoot is not null)
            {
                w.WriteString("blockMerkleRoot", e.BlockMerkleRoot);
            }

            if (e.ContentDeltaTokens is not null)
            {
                w.WriteNumber("contentDeltaTokens", e.ContentDeltaTokens.Value);
            }

            if (e.PrevEntryHash is not null)
            {
                w.WriteString("prevEntryHash", e.PrevEntryHash);
            }

            w.WriteString("keyId", e.KeyId);
            w.WriteString("alg", e.Alg);
            if (includeSig && e.Sig is not null)
            {
                w.WriteString("sig", e.Sig);
            }

            w.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}

/// <summary>
/// Append + verify for the signed watch-history chain (SI-05). The chain may be a WINDOW (the store
/// caps entries), so verification checks consecutive links rather than assuming entry 0 is genesis —
/// the genesis-null rule is enforced only when the first retained entry is <c>seq 0</c>.
/// </summary>
public static class WatchHistoryChain
{
    /// <summary>SHA-256 (sha256:hex) over the fully-signed canonical entry — the link the next entry points at.</summary>
    public static string EntryHash(WatchHistoryEntry entry) =>
        MerkleTree.HashPrefix
        + Convert.ToHexString(SHA256.HashData(WatchHistoryCanonicalizer.CanonicalBytes(entry, includeSig: true))).ToLowerInvariant();

    /// <summary>
    /// Build the next entry, chaining it to the last of <paramref name="existing"/> and signing it when a
    /// signer is supplied (receipts on). Pure given <paramref name="observedAt"/>.
    /// </summary>
    public static WatchHistoryEntry Append(
        IReadOnlyList<WatchHistoryEntry> existing,
        string @event,
        string contentHash,
        string? blockMerkleRoot,
        int? contentDeltaTokens,
        string observedAt,
        ReceiptSigner? signer)
    {
        var seq = existing.Count == 0 ? 0 : existing[^1].Seq + 1;
        var prevHash = existing.Count == 0 ? null : EntryHash(existing[^1]);
        var entry = new WatchHistoryEntry(
            seq,
            observedAt,
            @event,
            contentHash,
            blockMerkleRoot,
            contentDeltaTokens,
            prevHash,
            signer?.KeyId ?? string.Empty,
            signer is null ? string.Empty : ReceiptEnvelope.AlgEcdsaP256,
            Sig: null);

        if (signer is null)
        {
            return entry;
        }

        var sig = signer.SignDetached(WatchHistoryCanonicalizer.CanonicalBytes(entry, includeSig: false));
        return entry with { Sig = sig };
    }

    /// <summary>
    /// Verify a (possibly windowed) chain: consecutive seq, each link matches the prior entry's hash,
    /// genesis (seq 0) has a null prevEntryHash, and every signed entry's signature checks out against
    /// <paramref name="publicKeyPem"/>. Unsigned entries (receipts off) skip the signature check but
    /// still must chain. Empty chain is trivially valid.
    /// </summary>
    public static bool Verify(IReadOnlyList<WatchHistoryEntry> entries, string publicKeyPem)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            if (i == 0)
            {
                if (e.Seq == 0 && e.PrevEntryHash is not null)
                {
                    return false; // genesis must not point at anything
                }
            }
            else
            {
                var prior = entries[i - 1];
                if (e.Seq != prior.Seq + 1
                    || !string.Equals(e.PrevEntryHash, EntryHash(prior), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (e.Sig is not null
                && !ReceiptVerifier.VerifyDetached(WatchHistoryCanonicalizer.CanonicalBytes(e, includeSig: false), e.Sig, publicKeyPem))
            {
                return false;
            }
        }

        return true;
    }
}
