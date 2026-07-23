using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Transport;

/// <summary>
/// TLS + JWT configuration for Remote MCP transport.
/// Values are read from CLI args first, then env vars as fallback.
/// </summary>
/// <param name="TlsCertPath">Path to PFX/PEM certificate file.</param>
/// <param name="TlsCertPassword">PFX password (null for PEM).</param>
/// <param name="JwtIssuer">Expected JWT issuer (iss claim).</param>
/// <param name="JwtAudience">Expected JWT audience (aud claim).</param>
/// <param name="JwtMetadataUri">OpenID Connect metadata URI. When omitted, discovery starts from an HTTPS issuer URI.</param>
public sealed record RemoteMcpAuthOptions(
    string TlsCertPath,
    string? TlsCertPassword,
    string JwtIssuer,
    string JwtAudience,
    string? JwtMetadataUri = null)
{
    public const string MaxSessionsVariable = "OCCAM_REMOTE_MAX_SESSIONS";

    public static RemoteMcpAuthOptions FromEnvironment()
    {
        var certPath = Environment.GetEnvironmentVariable("OCCAM_TLS_CERT_PATH");
        var certPassword = Environment.GetEnvironmentVariable("OCCAM_TLS_CERT_PASSWORD");
        var issuer = Environment.GetEnvironmentVariable("OCCAM_JWT_ISSUER") ?? "occam-mcp";
        var audience = Environment.GetEnvironmentVariable("OCCAM_JWT_AUDIENCE") ?? "occam-mcp";
        var metadataUri = Environment.GetEnvironmentVariable("OCCAM_JWT_METADATA_URI")
            ?? Environment.GetEnvironmentVariable("OCCAM_JWT_JWKS_URI");

        return new RemoteMcpAuthOptions(
            TlsCertPath: certPath ?? string.Empty,
            TlsCertPassword: certPassword,
            JwtIssuer: issuer,
            JwtAudience: audience,
            JwtMetadataUri: metadataUri);
    }

    public bool HasCert => !string.IsNullOrEmpty(TlsCertPath) && File.Exists(TlsCertPath);

    public static int ReadMaxSessions() =>
        OccamEnvironment.GetInt(MaxSessionsVariable, defaultValue: 4, min: 1, max: 32);
}
