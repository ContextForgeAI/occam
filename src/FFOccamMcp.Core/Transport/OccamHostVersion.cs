using System.Reflection;

namespace OccamMcp.Core.Transport;

/// <summary>Host semver from <see cref="AssemblyInformationalVersionAttribute"/> (VERSION file via MSBuild).</summary>
internal static class OccamHostVersion
{
    private static readonly Lazy<string> CurrentLazy = new(() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0");

    public static string Current => CurrentLazy.Value;
}
