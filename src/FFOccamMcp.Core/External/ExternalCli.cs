using System.Diagnostics;
using System.Text;

namespace OccamMcp.Core.External;

/// <summary>
/// Minimal spawn helper for optional local CLIs (Donsetch, OCR bins). No shell interpolation.
/// </summary>
internal static class ExternalCli
{
    public static string? ResolveBinary(string envVar, string defaultFileName)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar)?.Trim();
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        return FindOnPath(defaultFileName);
    }

    public static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = OperatingSystem.IsWindows()
            ? new[] { fileName, fileName + ".exe", fileName + ".cmd", fileName + ".bat" }
            : new[] { fileName };

        foreach (var dir in parts)
        {
            foreach (var name in candidates)
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };
        foreach (var a in args)
        {
            proc.StartInfo.ArgumentList.Add(a);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        if (!proc.Start())
        {
            return (-1, "", "failed to start process");
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Math.Clamp(timeoutMs, 1_000, 300_000));
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
            }
            catch
            {
                /* ignore */
            }

            return (-2, stdout.ToString(), "timeout");
        }

        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
