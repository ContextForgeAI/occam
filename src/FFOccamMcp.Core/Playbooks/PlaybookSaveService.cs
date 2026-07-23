using System.Text;
using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

public sealed class PlaybookSaveService(
    PlaybookSaveVerifier verifier,
    PlaybookSeedResolver seedResolver,
    OccamMcp.Core.Receipts.ReceiptSigner signer)
{
    public async ValueTask<PlaybookSaveResult> SaveAsync(PlaybookSaveRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            return Fail(request.Url, "invalid_url", "URL is not absolute.");
        }

        if (string.IsNullOrWhiteSpace(request.PlaybookJson))
        {
            return Fail(request.Url, "playbook_schema_invalid", "playbook_json is required.");
        }

        if (PlaybookCommunityHygiene.ContainsForbiddenKeys(request.PlaybookJson))
        {
            return Fail(request.Url, "playbook_save_rejected", "playbook_json contains forbidden secret keys.");
        }

        var document = PlaybookDocument.TryParse(request.PlaybookJson);
        if (document is null)
        {
            return Fail(request.Url, "playbook_schema_invalid", "playbook_json is not a valid site playbook (schema_version 1.x).");
        }

        var localRoot = PlaybookPaths.ResolveLocalRoot();
        Directory.CreateDirectory(localRoot);

        var targetPath = Path.GetFullPath(Path.Combine(localRoot, $"{document.Id}.playbook.json"));
        var localRootFull = Path.GetFullPath(localRoot);
        if (!targetPath.StartsWith(localRootFull, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(request.Url, "playbook_save_rejected", "Refusing to write outside local playbook tier.");
        }

        if (IsBundledSeedPath(targetPath))
        {
            return Fail(request.Url, "playbook_save_rejected", "Refusing to overwrite bundled seed playbooks.");
        }

        var verifyUrl = string.IsNullOrWhiteSpace(request.VerifyUrl) ? request.Url : request.VerifyUrl!;
        PlaybookVerifyMetrics? verifyMetrics = null;
        if (request.Verify)
        {
            var verifyResult = await verifier.VerifyAsync(verifyUrl, request.PlaybookJson, document);
            if (!verifyResult.Ok)
            {
                return new PlaybookSaveResult(
                    false,
                    request.Url,
                    document.Id,
                    null,
                    verifyResult.Metrics,
                    false,
                    verifyResult.FailureCode ?? "playbook_verify_failed",
                    verifyResult.Message);
            }

            verifyMetrics = verifyResult.Metrics;
        }

        var jsonToWrite = request.PlaybookJson;
        var lessonAppended = false;
        if (!string.IsNullOrWhiteSpace(request.LessonNote))
        {
            jsonToWrite = PlaybookDocument.AppendLesson(
                jsonToWrite,
                request.LessonNote,
                request.FailureReason,
                verifyMetrics?.Score,
                request.HostId);
            lessonAppended = true;
        }

        // SI-08 (local foundation): sign the saved playbook so it is self-authenticating — it
        // carries the author's keyId, a signature, and the verify-gate proof. Self-verifiable now;
        // the basis for a future signed registry + reputation (distribution deferred until nodes exist).
        var signedJson = PlaybookSignature.BuildSignedJson(
            jsonToWrite,
            verifyMetrics?.Score,
            verifyMetrics?.PassesGate ?? false,
            verifyMetrics?.NoiseLeakage,
            signer);

        File.WriteAllText(targetPath, FormatJson(signedJson));
        seedResolver.ClearCacheForTests();

        return new PlaybookSaveResult(
            true,
            request.Url,
            document.Id,
            targetPath,
            verifyMetrics,
            lessonAppended,
            null,
            request.Verify ? "Saved after verify gate pass." : null,
            SignedKeyId: signer.KeyId);
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                doc.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return json;
        }
    }

    private static bool IsBundledSeedPath(string targetPath)
    {
        var normalized = targetPath.Replace('\\', '/');
        return normalized.Contains("/profiles/playbooks/seeds/", StringComparison.OrdinalIgnoreCase);
    }

    private static PlaybookSaveResult Fail(string url, string code, string message) =>
        new(false, url, null, null, null, false, code, message);
}
