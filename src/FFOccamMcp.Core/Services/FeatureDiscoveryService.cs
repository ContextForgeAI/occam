using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Services;

public sealed class FeatureDiscoveryService(WorkerPaths workerPaths)
{
    public bool IsBrowserAvailable()
    {
        if (string.IsNullOrWhiteSpace(workerPaths.BrowserExtractScript) || !File.Exists(workerPaths.BrowserExtractScript))
        {
            return false;
        }

        var browsersPath = PlaywrightEnvironment.ResolveDefaultBrowsersPath();
        if (string.IsNullOrWhiteSpace(browsersPath))
        {
            var envPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return PlaywrightEnvironment.HasChromiumInstall(envPath);
            }
            return false;
        }

        return true;
    }

    /// <summary>Budget for the provision-gate probe. It is pure logic (no browser launch), so anything
    /// slower than this means node itself is wedged — fall back rather than stall the request.</summary>
    private const int ProbeTimeoutMs = 10_000;

    /// <summary>
    /// The worker's own auto-provision answer, asked once and cached for the process lifetime: every input
    /// the probe reads is process-level env, which cannot change mid-run. Lazy, so the node spawn is only
    /// paid when someone actually asks — i.e. when no browser is installed (the rare cold path).
    /// </summary>
    private readonly Lazy<bool> _willAutoProvision = new(() => ProbeWorkerProvisionGate(workerPaths));

    /// <summary>
    /// True when the browser worker would auto-provision the missing chromium itself (branch 2 of the
    /// self-managing browser layer). The caller uses this to NOT downgrade a browser request to HTTP when
    /// <see cref="IsBrowserAvailable"/> is false: downgrading would preempt the on-launch provision that
    /// only fires when the browser path is actually attempted.
    /// <para>
    /// B6: this used to re-implement the worker's gate in C# and keep it in sync by hand. It now ASKS the
    /// worker (workers/browser-extract/lib/provision-gate.mjs), so the rule lives in exactly one place —
    /// the language that executes it — and the two can no longer drift.
    /// </para>
    /// </summary>
    public bool WillAutoProvisionBrowser()
    {
        if (string.IsNullOrWhiteSpace(workerPaths.BrowserExtractScript) || !File.Exists(workerPaths.BrowserExtractScript))
        {
            return false;
        }

        return _willAutoProvision.Value;
    }

    /// <summary>
    /// Spawns the worker's provision-gate probe and reads its one-line JSON verdict.
    /// <para>
    /// When the probe can't be run or doesn't answer, default to <c>true</c> (i.e. "assume it will
    /// provision", so the caller does NOT downgrade). That is the honest fallback: the browser attempt then
    /// surfaces the worker's own typed <c>playwright_missing</c> + fix instead of silently returning
    /// different content — and, unlike a C#-side guess, it does not reintroduce the mirror this removes.
    /// </para>
    /// </summary>
    private static bool ProbeWorkerProvisionGate(WorkerPaths workerPaths)
    {
        var script = ResolveProvisionGateScript(workerPaths);
        if (script is null)
        {
            Console.Error.WriteLine(
                "[occam.config] provision-gate probe not found next to the browser worker — assuming auto-provision is on.");
            return true;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = $"\"{script}\"",
                RedirectStandardOutput = true,
                // Leave stderr inherited (the diagnostics channel). Redirecting a pipe we never drain is the
                // deadlock class B2 fixed; the probe's stdout is one short line, far below the pipe buffer.
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("[occam.config] provision-gate probe failed to start — assuming auto-provision is on.");
                return true;
            }

            if (!process.WaitForExit(ProbeTimeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best-effort
                }

                Console.Error.WriteLine("[occam.config] provision-gate probe timed out — assuming auto-provision is on.");
                return true;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (string.IsNullOrWhiteSpace(line))
            {
                Console.Error.WriteLine("[occam.config] provision-gate probe printed no verdict — assuming auto-provision is on.");
                return true;
            }

            var payload = JsonSerializer.Deserialize(line, WorkerExtractJsonContext.Default.ProvisionGateProbeResponse);
            if (payload is null)
            {
                Console.Error.WriteLine("[occam.config] provision-gate probe verdict was unreadable — assuming auto-provision is on.");
                return true;
            }

            return payload.WillProvision;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[occam.config] provision-gate probe failed ({ex.GetType().Name}) — assuming auto-provision is on.");
            return true;
        }
    }

    /// <summary>Locates the probe next to the browser worker, so an OCCAM_BROWSER_EXTRACT_SCRIPT override
    /// still resolves the matching gate.</summary>
    private static string? ResolveProvisionGateScript(WorkerPaths workerPaths)
    {
        if (string.IsNullOrWhiteSpace(workerPaths.BrowserExtractScript))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(workerPaths.BrowserExtractScript));
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        var script = Path.Combine(dir, "lib", "provision-gate.mjs");
        return File.Exists(script) ? script : null;
    }
}
