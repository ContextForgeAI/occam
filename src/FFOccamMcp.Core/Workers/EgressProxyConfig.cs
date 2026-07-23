using System.Diagnostics;
using OccamMcp.Core.Services;

namespace OccamMcp.Core.Workers;

/// <summary>
/// Compiles OCCAM_* proxy env for worker spawns only — Core never performs proxied HTTP.
/// </summary>
public static class EgressProxyConfig
{
    public const string HttpProxyVar = "OCCAM_HTTP_PROXY";
    public const string HttpsProxyVar = "OCCAM_HTTPS_PROXY";
    public const string NoProxyVar = "OCCAM_NO_PROXY";

    public static EgressProxySettings ReadFromEnvironment() =>
        new(
            TrimEnv(HttpProxyVar),
            TrimEnv(HttpsProxyVar),
            TrimEnv(NoProxyVar));

    public static void ApplyTo(ProcessStartInfo psi)
    {
        if (psi.UseShellExecute)
        {
            return;
        }

        ReadFromEnvironment().ApplyTo(psi);
    }

    /// <summary>
    /// Applies egress env for one worker spawn: rotated proxy when the pool is configured,
    /// otherwise static <see cref="ReadFromEnvironment"/>.
    /// </summary>
    public static void ApplyForSpawn(ProcessStartInfo psi, IProxyRotationService proxyRotation)
    {
        if (psi.UseShellExecute)
        {
            return;
        }

        var staticSettings = ReadFromEnvironment();
        if (proxyRotation.IsConfigured)
        {
            var rotated = proxyRotation.AcquireNext();
            if (rotated is { } proxy)
            {
                staticSettings.ApplyTo(psi, proxy.ProxyUrl);
                return;
            }
        }

        staticSettings.ApplyTo(psi);
    }

    public static bool IsValidProxyUrl(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(proxyUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" or "socks5" && !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static string RedactCredentials(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(proxyUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return "(invalid proxy url)";
        }

        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return uri.ToString();
        }

        var builder = new UriBuilder(uri)
        {
            UserName = "***",
            Password = "***",
        };

        return builder.Uri.ToString();
    }

    private static string? TrimEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public readonly record struct EgressProxySettings(string? HttpProxy, string? HttpsProxy, string? NoProxy)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(HttpProxy) || !string.IsNullOrWhiteSpace(HttpsProxy);

    public void ApplyTo(ProcessStartInfo psi)
    {
        SetIfPresent(psi, EgressProxyConfig.HttpProxyVar, HttpProxy);
        SetIfPresent(psi, EgressProxyConfig.HttpsProxyVar, HttpsProxy);
        SetIfPresent(psi, EgressProxyConfig.NoProxyVar, NoProxy);
    }

    /// <summary>Per-spawn override — sets both HTTP and HTTPS proxy vars to the same URL.</summary>
    public void ApplyTo(ProcessStartInfo psi, string? httpProxyOverride)
    {
        if (!string.IsNullOrWhiteSpace(httpProxyOverride)
            && EgressProxyConfig.IsValidProxyUrl(httpProxyOverride))
        {
            SetIfPresent(psi, EgressProxyConfig.HttpProxyVar, httpProxyOverride);
            SetIfPresent(psi, EgressProxyConfig.HttpsProxyVar, httpProxyOverride);
            SetIfPresent(psi, EgressProxyConfig.NoProxyVar, NoProxy);
            return;
        }

        ApplyTo(psi);
    }

    private static void SetIfPresent(ProcessStartInfo psi, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            psi.Environment[name] = value;
        }
    }
}
