using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Dataset;
using OccamMcp.Core.Lifecycle;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Watch;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Cli;

/// <summary>
/// Public offline verifier — the consumer-side surface that makes Receipt v1 real for a THIRD PARTY.
/// Two verbs, both network-free and self-contained (no MCP transport, no worker spawn):
/// <list type="bullet">
/// <item><c>keys export</c> — print this host's public key (PEM) so a consumer can pin it.</item>
/// <item><c>verify</c> — offline-verify a receipt / citation / dataset manifest / watch-history chain
/// against a pinned public key, using the exact same primitives the <c>occam_verify</c> MCP tool uses.</item>
/// </list>
/// Exit codes: 0 = verified, 1 = parsed but NOT verified (tamper / wrong key / drift), 2 = usage/IO error.
/// Verdict JSON goes to stdout; diagnostics to stderr.
/// </summary>
public static class OccamCliVerbs
{
    /// <summary>Dispatch a top-level verb. Returns false (verb not ours) so the caller falls through to the MCP host.</summary>
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
        {
            return false;
        }

        switch (args[0])
        {
            case "keys" when args.Length >= 2 && args[1] == "export":
                exitCode = KeysExport(ParseFlags(args, 2));
                return true;
            case "verify":
                exitCode = Verify(ParseFlags(args, 1));
                return true;
            case "install-browser":
                exitCode = InstallBrowser(ParseFlags(args, 1));
                return true;
            case "version-surface":
                exitCode = VersionSurface();
                return true;
            case "lifecycle":
                exitCode = LifecycleVerb(args);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// User-level browser provisioning. Downloads the Playwright chromium into the per-user cache
    /// (no root, no system libs) so the browser backend can launch. This is the exact command a
    /// <c>playwright_missing</c> failure's <c>fix.command</c> points at, so agents/scripts can act on
    /// the typed error without a human. Machine-readable marker on stdout, playwright's own chatter on
    /// stderr. Exit: 0 = browser ready, 1 = install failed, 2 = worker tree not found.
    /// </summary>
    private static int InstallBrowser(Dictionary<string, string> flags)
    {
        // A configured system browser (channel or explicit path) needs no download — nothing to install.
        var executablePath =
            Environment.GetEnvironmentVariable("OCCAM_BROWSER_EXECUTABLE_PATH")?.Trim();
        if (string.IsNullOrEmpty(executablePath))
        {
            executablePath = Environment.GetEnvironmentVariable("OCCAM_CHROME_PATH")?.Trim();
        }

        var channel = Environment.GetEnvironmentVariable("OCCAM_BROWSER_CHANNEL")?.Trim()?.ToLowerInvariant();
        var usesSystemBrowser = !string.IsNullOrEmpty(executablePath)
            || (!string.IsNullOrEmpty(channel) && channel != "chromium");
        if (usesSystemBrowser)
        {
            var which = !string.IsNullOrEmpty(executablePath) ? executablePath : $"channel '{channel}'";
            return EmitInstall(
                new CliInstallBrowserResult(true, "install_browser", "already_present", 0,
                    Message: $"using system browser ({which}); no download needed"),
                0);
        }

        var root = WorkerPaths.ResolveOccamHome();
        var browserDir = root is null ? null : Path.Combine(root, "workers", "browser-extract");
        if (browserDir is null || !Directory.Exists(browserDir))
        {
            return EmitInstall(
                new CliInstallBrowserResult(false, "install_browser", "worker_missing", 2,
                    BrowserExtractDir: browserDir,
                    Message: "cannot locate workers/browser-extract (set OCCAM_HOME to the occam install root)"),
                2);
        }

        Console.Error.WriteLine($"# occam install-browser: npx playwright install chromium (cwd {browserDir})");

        int childExit;
        try
        {
            childExit = RunPlaywrightInstall(browserDir);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return EmitInstall(
                new CliInstallBrowserResult(false, "install_browser", "failed", 1,
                    BrowserExtractDir: browserDir,
                    Message: $"could not launch npx (is Node.js on PATH?): {ex.Message}"),
                1);
        }

        if (childExit == 0)
        {
            return EmitInstall(
                new CliInstallBrowserResult(true, "install_browser", "installed", 0,
                    BrowserExtractDir: browserDir,
                    Message: "chromium is installed; the browser backend can launch"),
                0);
        }

        return EmitInstall(
            new CliInstallBrowserResult(false, "install_browser", "failed", childExit,
                BrowserExtractDir: browserDir,
                Message: $"playwright install exited {childExit}; see stderr above"),
            1);
    }

    /// <summary>Spawn <c>npx playwright install chromium</c> in the browser worker dir, forwarding all
    /// child output to our stderr (stdout stays reserved for the JSON marker). Returns the child exit code.</summary>
    private static int RunPlaywrightInstall(string browserDir)
    {
        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "npx",
            WorkingDirectory = browserDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (isWindows)
        {
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("npx");
        }
        psi.ArgumentList.Add("playwright");
        psi.ArgumentList.Add("install");
        psi.ArgumentList.Add("chromium");

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static int EmitInstall(CliInstallBrowserResult result, int exitCode)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result, OccamCliJsonContext.Default.CliInstallBrowserResult));
        return exitCode;
    }

    /// <summary>
    /// Deployment diagnostic: which binary is running and which product version it embeds.
    /// <c>schemaFingerprint</c> / <c>protocolVersion</c> require a live <c>tools/list</c> — see
    /// <c>scripts/check-public-mcp-contract.mjs</c> (composes the full surface JSON).
    /// </summary>
    private static int VersionSurface()
    {
        var asm = typeof(OccamCliVerbs).Assembly;
        var hostVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
        // Strip any "+<commit>" suffix some CI builds append — keep SemVer product id stable.
        var plus = hostVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            hostVersion = hostVersion[..plus];
        }

        // Native AOT single-file: Assembly.Location is empty — prefer the running process path.
        var assemblyPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            assemblyPath = AppContext.BaseDirectory;
        }
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            assemblyPath = "(unknown)";
        }

        var result = new CliVersionSurfaceResult(
            HostVersion: hostVersion,
            AssemblyPath: assemblyPath,
            PackageVersion: hostVersion,
            ProtocolVersion: null,
            SchemaFingerprint: null);
        Console.Out.WriteLine(JsonSerializer.Serialize(result, OccamCliJsonContext.Default.CliVersionSurfaceResult));
        return 0;
    }

    private static int KeysExport(Dictionary<string, string> flags)
    {
        var keysRoot = flags.GetValueOrDefault("keys-root");
        var signer = ReceiptSigner.LoadOrCreate(keysRoot);
        Console.Error.WriteLine($"# occam public key (keyId {signer.KeyId})");
        Console.Out.Write(signer.ExportPublicKeyPem());
        return 0;
    }

    private static int Verify(Dictionary<string, string> flags)
    {
        var mode = flags.GetValueOrDefault("mode", "receipt");
        var pubPath = flags.GetValueOrDefault("pubkey");
        if (pubPath is null)
        {
            return Usage("verify needs --pubkey <path> (export it with `occam keys export`).");
        }

        string publicKeyPem;
        try
        {
            publicKeyPem = File.ReadAllText(pubPath);
        }
        catch (IOException ex)
        {
            return Usage($"cannot read --pubkey: {ex.Message}");
        }

        return mode switch
        {
            "receipt" => VerifyReceipt(flags, publicKeyPem),
            "citation" => VerifyCitation(flags, publicKeyPem),
            "manifest" => VerifyManifest(flags, publicKeyPem),
            "history" => VerifyHistory(flags, publicKeyPem),
            _ => Usage($"unknown --mode '{mode}' (receipt | citation | manifest | history)."),
        };
    }

    private static int VerifyReceipt(Dictionary<string, string> flags, string publicKeyPem)
    {
        if (!TryReadInput(flags, "receipt", out var json, out var usageErr))
        {
            return usageErr;
        }

        if (!TryParseReceipt(json, out var envelope, out var anchor) || envelope is null)
        {
            return Emit(new CliVerifyResult(false, "receipt", "invalid_receipt", Message: "not valid receipt JSON"), 1);
        }

        string? markdown = null;
        if (flags.TryGetValue("markdown", out var mdPath))
        {
            try { markdown = File.ReadAllText(mdPath); }
            catch (IOException ex) { return Usage($"cannot read --markdown: {ex.Message}"); }
        }

        var offline = ReceiptVerifier.VerifyOffline(envelope, publicKeyPem, markdown);
        if (offline.Verdict == ReceiptVerification.InvalidReceipt)
        {
            return Emit(new CliVerifyResult(false, "receipt", "invalid_receipt", Message: "missing signature / unsupported version"), 1);
        }

        bool? anchorValid = null;
        string? anchorGenTime = null;
        if (anchor is not null)
        {
            var ta = TimeAnchorVerifier.Verify(anchor, envelope.Sig);
            anchorValid = ta.Valid;
            anchorGenTime = ta.GenTime;
        }

        // Verified only when the signature holds AND every supplied check passes (contentHash if
        // markdown given, time anchor if present).
        var verified = offline.SignatureValid
            && offline.ContentHashMatch != false
            && anchorValid != false;
        var verdict = !offline.SignatureValid ? "signature_invalid"
            : offline.ContentHashMatch == false ? "content_hash_mismatch"
            : anchorValid == false ? "time_anchor_invalid"
            : "verified";

        return Emit(
            new CliVerifyResult(verified, "receipt", verdict, offline.SignatureValid, offline.ContentHashMatch,
                KeyId: envelope.KeyId, TimeAnchorValid: anchorValid, TimeAnchorGenTime: anchorGenTime),
            verified ? 0 : 1);
    }

    private static int VerifyCitation(Dictionary<string, string> flags, string publicKeyPem)
    {
        if (!TryReadInput(flags, "receipt", out var json, out var usageErr))
        {
            return usageErr;
        }

        if (!TryParseReceipt(json, out var envelope, out _) || envelope?.BlockMerkleRoot is null)
        {
            return Emit(new CliVerifyResult(false, "citation", "invalid_receipt", Message: "receipt has no block root"), 1);
        }

        var blockText = flags.GetValueOrDefault("block-text");
        var proofPath = flags.GetValueOrDefault("proof");
        if (blockText is null || proofPath is null)
        {
            return Usage("citation mode needs --block-text <text> and --proof <path> (proof JSON from `occam_verify prove`).");
        }

        MerkleProofStep[]? proof;
        try
        {
            proof = JsonSerializer.Deserialize(File.ReadAllText(proofPath), OccamCliJsonContext.Default.MerkleProofStepArray);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Usage($"cannot read/parse --proof: {ex.Message}");
        }

        if (proof is null)
        {
            return Usage("--proof is not a valid array of {hash, siblingIsRight}.");
        }

        var offline = ReceiptVerifier.VerifyOffline(envelope, publicKeyPem);
        var leaf = Convert.ToHexString(MerkleTree.LeafHash(blockText, flags.GetValueOrDefault("block-selector"))).ToLowerInvariant();
        var membershipOk = MerkleTree.VerifyProof(leaf, proof, envelope.BlockMerkleRoot);
        var verified = offline.SignatureValid && membershipOk;
        var verdict = !offline.SignatureValid ? "signature_invalid" : membershipOk ? "citation_verified" : "citation_invalid";

        return Emit(new CliVerifyResult(verified, "citation", verdict, offline.SignatureValid, KeyId: envelope.KeyId), verified ? 0 : 1);
    }

    private static int VerifyManifest(Dictionary<string, string> flags, string publicKeyPem)
    {
        if (!TryReadInput(flags, "input", out var json, out var usageErr))
        {
            return usageErr;
        }

        OccamDatasetExportResponse? export;
        try
        {
            export = JsonSerializer.Deserialize(json, OccamDatasetJsonContext.Default.OccamDatasetExportResponse);
        }
        catch (JsonException)
        {
            export = null;
        }

        if (export?.Manifest is null || export.Rows is null)
        {
            return Emit(new CliVerifyResult(false, "manifest", "invalid_input", Message: "not a dataset_export response"), 1);
        }

        var m = export.Manifest;
        if (m.Sig is null)
        {
            return Emit(new CliVerifyResult(false, "manifest", "unsigned", Message: "manifest has no signature (OCCAM_RECEIPTS was off at export)"), 1);
        }

        var rows = export.Rows
            .Select(r => new DatasetRow(r.Url, r.FinalUrl, r.Ok, r.ContentHash, r.BlockMerkleRoot, r.FailureCode))
            .ToArray();

        var ok = DatasetManifestBuilder.Verify(rows, m.V, m.CreatedAt, m.ManifestRoot, m.KeyId ?? string.Empty, m.Alg, m.Sig, publicKeyPem);
        return Emit(
            new CliVerifyResult(ok, "manifest", ok ? "manifest_verified" : "manifest_invalid", KeyId: m.KeyId,
                Message: ok ? $"{rows.Length} rows bound under the signed root" : "rows do not reconstruct the signed root, or bad signature"),
            ok ? 0 : 1);
    }

    private static int VerifyHistory(Dictionary<string, string> flags, string publicKeyPem)
    {
        if (!TryReadInput(flags, "input", out var json, out var usageErr))
        {
            return usageErr;
        }

        WatchHistoryEntry[]? entries;
        try
        {
            var trimmed = json.TrimStart();
            entries = trimmed.StartsWith('[')
                ? JsonSerializer.Deserialize(json, OccamCliJsonContext.Default.WatchHistoryEntryArray)
                : JsonSerializer.Deserialize(json, OccamCliJsonContext.Default.CliHistoryInput)?.History;
        }
        catch (JsonException)
        {
            entries = null;
        }

        if (entries is null)
        {
            return Emit(new CliVerifyResult(false, "history", "invalid_input", Message: "needs a JSON array of entries or {history:[...]}"), 1);
        }

        var ok = WatchHistoryChain.Verify(entries, publicKeyPem);
        var keyId = entries.FirstOrDefault(e => e.Sig is not null)?.KeyId;
        return Emit(
            new CliVerifyResult(ok, "history", ok ? "history_verified" : "history_invalid", KeyId: keyId,
                Message: $"{entries.Length} entries"),
            ok ? 0 : 1);
    }

    /// <summary>Accept a full receipt object ({signed, blockLeaves, timeAnchor}) or a bare envelope.</summary>
    private static bool TryParseReceipt(string json, out ReceiptEnvelope? envelope, out ReceiptTimeAnchor? anchor)
    {
        envelope = null;
        anchor = null;
        try
        {
            var wrapper = JsonSerializer.Deserialize(json, OccamCliJsonContext.Default.CliReceiptInput);
            if (wrapper?.Signed is not null)
            {
                envelope = wrapper.Signed;
                anchor = wrapper.TimeAnchor;
                return true;
            }

            envelope = JsonSerializer.Deserialize(json, OccamCliJsonContext.Default.ReceiptEnvelope);
            return envelope is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadInput(Dictionary<string, string> flags, string flag, out string json, out int usageError)
    {
        json = string.Empty;
        usageError = 0;
        var path = flags.GetValueOrDefault(flag);
        if (path is null)
        {
            usageError = Usage($"verify needs --{flag} <path|-> (use '-' for stdin).");
            return false;
        }

        try
        {
            json = path == "-" ? Console.In.ReadToEnd() : File.ReadAllText(path);
            return true;
        }
        catch (IOException ex)
        {
            usageError = Usage($"cannot read --{flag}: {ex.Message}");
            return false;
        }
    }

    private static Dictionary<string, string> ParseFlags(string[] args, int start)
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = start; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
            flags[key] = value;
        }

        return flags;
    }

    private static int Emit(CliVerifyResult result, int exitCode)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result, OccamCliJsonContext.Default.CliVerifyResult));
        return exitCode;
    }

    private static int LifecycleVerb(string[] args)
    {
        if (args.Length < 2)
        {
            return Usage("lifecycle <self|diagnose> — read-only instance identity (INV-10).");
        }

        var adapter = new LocalLifecycleAdapter();
        return args[1] switch
        {
            "self" => EmitLifecycleSelf(adapter),
            "diagnose" => EmitLifecycleDiagnose(adapter, ParseFlags(args, 2)),
            _ => Usage("lifecycle <self|diagnose>"),
        };
    }

    private static int EmitLifecycleSelf(ILifecycleAdapter adapter)
    {
        var descriptor = adapter.DescribeSelfDescriptor();
        Console.Out.WriteLine(JsonSerializer.Serialize(
            new CliLifecycleSelfResult(true, descriptor),
            OccamCliJsonContext.Default.CliLifecycleSelfResult));
        return 0;
    }

    private static int EmitLifecycleDiagnose(ILifecycleAdapter adapter, Dictionary<string, string> flags)
    {
        // Peers are optional JSON array of HostIdentityDescriptor-compatible objects supplied by the
        // operator/host. Occam never scans/kills by process name from this verb.
        var peers = Array.Empty<HostIdentity>();
        if (flags.TryGetValue("peers", out var peersPath))
        {
            try
            {
                var json = File.ReadAllText(peersPath);
                var parsed = JsonSerializer.Deserialize(json, OccamCliJsonContext.Default.HostIdentityDescriptorArray)
                    ?? [];
                peers = parsed.Select(d => new HostIdentity(
                    new RuntimeId(d.RuntimeId),
                    d.Pid,
                    new ParentHostIdentity(d.ParentPid, d.ParentLabel),
                    new SessionId(d.SessionId),
                    new StartTimestamp(DateTimeOffset.Parse(d.StartedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind)),
                    new Ownership(
                        Enum.TryParse<OwnershipKind>(d.OwnershipKind, out var kind) ? kind : OwnershipKind.Unknown,
                        d.OwnerLabel),
                    d.OccamHome,
                    d.BinaryPath,
                    d.Version,
                    d.Transport)).ToArray();
            }
            catch (Exception ex) when (ex is IOException or JsonException or FormatException)
            {
                return Usage($"lifecycle diagnose --peers <json-file>: {ex.Message}");
            }
        }

        var diagnostics = adapter.Diagnose(peers);
        Console.Out.WriteLine(JsonSerializer.Serialize(
            new CliLifecycleDiagnoseResult(
                true,
                diagnostics.Self,
                diagnostics.ObservedPeers.ToArray(),
                diagnostics.OverlapWarnings.ToArray()),
            OccamCliJsonContext.Default.CliLifecycleDiagnoseResult));
        return 0;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"usage: {message}");
        return 2;
    }
}

/// <summary>Verdict of a CLI offline verification. <c>ok</c> mirrors the exit code (0 = true).</summary>
public sealed record CliVerifyResult(
    bool Ok,
    string Mode,
    string Verdict,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SignatureValid = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ContentHashMatch = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KeyId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? TimeAnchorValid = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TimeAnchorGenTime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

/// <summary>Marker for <c>occam install-browser</c>. <c>ok</c> mirrors the exit code (0 = browser ready);
/// <c>status</c> is one of installed | already_present | worker_missing | failed.</summary>
public sealed record CliInstallBrowserResult(
    bool Ok,
    string Action,
    string Status,
    int ExitCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BrowserExtractDir = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

/// <summary>Partial version surface from the host binary (no MCP handshake). Null protocol/fingerprint
/// fields are filled by <c>scripts/check-public-mcp-contract.mjs</c>.</summary>
public sealed record CliVersionSurfaceResult(
    string HostVersion,
    string AssemblyPath,
    string PackageVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProtocolVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SchemaFingerprint = null);

public sealed record CliLifecycleSelfResult(bool Ok, HostIdentityDescriptor Identity);

public sealed record CliLifecycleDiagnoseResult(
    bool Ok,
    HostIdentityDescriptor Self,
    HostIdentityDescriptor[] ObservedPeers,
    string[] OverlapWarnings);

/// <summary>Wrapper shapes the CLI parses (mirrors the MCP tool inputs, isolated so the verbs are self-contained).</summary>
public sealed record CliReceiptInput(ReceiptEnvelope? Signed, string[]? BlockLeaves, ReceiptTimeAnchor? TimeAnchor);

public sealed record CliHistoryInput(WatchHistoryEntry[]? History);

[JsonSerializable(typeof(CliVerifyResult))]
[JsonSerializable(typeof(CliInstallBrowserResult))]
[JsonSerializable(typeof(CliVersionSurfaceResult))]
[JsonSerializable(typeof(CliLifecycleSelfResult))]
[JsonSerializable(typeof(CliLifecycleDiagnoseResult))]
[JsonSerializable(typeof(HostIdentityDescriptor))]
[JsonSerializable(typeof(HostIdentityDescriptor[]))]
[JsonSerializable(typeof(CliReceiptInput))]
[JsonSerializable(typeof(CliHistoryInput))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptTimeAnchor))]
[JsonSerializable(typeof(MerkleProofStep[]))]
[JsonSerializable(typeof(WatchHistoryEntry[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamCliJsonContext : JsonSerializerContext;
