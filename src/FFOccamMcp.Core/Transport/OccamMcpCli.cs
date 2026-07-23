using System.Net;

namespace OccamMcp.Core.Transport;

public enum OccamMcpTransportMode
{
    Stdio,
    WebSocket,
    Remote,
    BatchServer,
}

public sealed class OccamMcpCli
{
    public const int DefaultWebSocketPort = 5050;
    public const int DefaultBatchServerPort = 5051;
    public const int DefaultRemotePort = 8443;
    public const string DefaultBindAddress = "127.0.0.1";

    public OccamMcpTransportMode Mode { get; init; } = OccamMcpTransportMode.Stdio;

    public int Port { get; init; } = DefaultWebSocketPort;

    public string BindAddress { get; init; } = DefaultBindAddress;

    public RemoteMcpAuthOptions? RemoteAuth { get; init; }

    public string? FailureKind { get; private set; }

    public bool IsValid => FailureKind is null;

    public bool ShowHelp { get; private set; }

    public static OccamMcpCli Parse(string[] args)
    {
        if (args.Any(static a => a is "-h" or "--help" or "-help" or "/help" or "/?"))
        {
            return new OccamMcpCli { ShowHelp = true };
        }

        var mode = OccamMcpTransportMode.Stdio;
        var port = DefaultWebSocketPort;
        var bind = DefaultBindAddress;
        string? failure = null;

        // Remote auth options (CLI args override env vars)
        string? tlsCertPath = null;
        string? tlsCertPassword = null;
        string? jwtIssuer = null;
        string? jwtAudience = null;
        string? jwtMetadataUri = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--batch-server" or "-batch-server")
            {
                mode = OccamMcpTransportMode.BatchServer;
                port = DefaultBatchServerPort;
                continue;
            }

            if (arg is "--mcp-server" or "-mcp-server")
            {
                mode = OccamMcpTransportMode.WebSocket;
                continue;
            }

            if (arg is "--remote")
            {
                mode = OccamMcpTransportMode.Remote;
                port = DefaultRemotePort;
                continue;
            }

            if (arg is "--port" or "-port")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out var parsed))
                {
                    failure = "invalid_arguments";
                    break;
                }

                port = parsed;
                continue;
            }

            if (arg is "--bind" or "-bind")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[++i]))
                {
                    failure = "invalid_arguments";
                    break;
                }

                bind = args[i];
                continue;
            }

            if (arg is "--tls-cert")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[++i]))
                {
                    failure = "invalid_arguments";
                    break;
                }

                tlsCertPath = args[i];
                continue;
            }

            if (arg is "--tls-password")
            {
                if (i + 1 >= args.Length)
                {
                    failure = "invalid_arguments";
                    break;
                }

                tlsCertPassword = args[i + 1];
                i++;
                continue;
            }

            if (arg is "--jwt-issuer")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[++i]))
                {
                    failure = "invalid_arguments";
                    break;
                }

                jwtIssuer = args[i];
                continue;
            }

            if (arg is "--jwt-audience")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[++i]))
                {
                    failure = "invalid_arguments";
                    break;
                }

                jwtAudience = args[i];
                continue;
            }

            if (arg is "--jwt-metadata-uri" or "--jwt-jwks-uri")
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[++i]))
                {
                    failure = "invalid_arguments";
                    break;
                }

                jwtMetadataUri = args[i];
                continue;
            }
        }

        if (failure is null && mode is OccamMcpTransportMode.WebSocket or OccamMcpTransportMode.BatchServer or OccamMcpTransportMode.Remote)
        {
            if (port is < 1 or > 65535)
            {
                failure = "invalid_arguments";
            }
            else if (mode is OccamMcpTransportMode.WebSocket or OccamMcpTransportMode.BatchServer
                && !string.Equals(bind, DefaultBindAddress, StringComparison.Ordinal))
            {
                failure = "invalid_arguments";
            }
            else if (mode == OccamMcpTransportMode.Remote && !IPAddress.TryParse(bind, out _))
            {
                failure = "invalid_arguments";
            }
        }

        // Validate remote mode requirements
        RemoteMcpAuthOptions? remoteAuth = null;
        if (failure is null && mode == OccamMcpTransportMode.Remote)
        {
            // CLI args take precedence, then env vars
            var certPath = tlsCertPath ?? Environment.GetEnvironmentVariable("OCCAM_TLS_CERT_PATH");
            var certPassword = tlsCertPassword ?? Environment.GetEnvironmentVariable("OCCAM_TLS_CERT_PASSWORD");
            var issuer = jwtIssuer ?? Environment.GetEnvironmentVariable("OCCAM_JWT_ISSUER") ?? "occam-mcp";
            var audience = jwtAudience ?? Environment.GetEnvironmentVariable("OCCAM_JWT_AUDIENCE") ?? "occam-mcp";
            var metadata = jwtMetadataUri
                ?? Environment.GetEnvironmentVariable("OCCAM_JWT_METADATA_URI")
                ?? Environment.GetEnvironmentVariable("OCCAM_JWT_JWKS_URI");

            if (string.IsNullOrWhiteSpace(certPath))
            {
                failure = "remote_requires_tls_cert";
            }
            else if (!File.Exists(certPath))
            {
                failure = "tls_cert_not_found";
            }
            else if (metadata is not null && !IsHttpsUri(metadata))
            {
                failure = "invalid_jwt_metadata_uri";
            }
            else if (metadata is null && !IsHttpsUri(issuer))
            {
                failure = "remote_requires_jwt_metadata";
            }
            else
            {
                remoteAuth = new RemoteMcpAuthOptions(certPath, certPassword, issuer, audience, metadata);
            }
        }

        return new OccamMcpCli
        {
            Mode = mode,
            Port = port,
            BindAddress = bind,
            RemoteAuth = remoteAuth,
            FailureKind = failure,
        };
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine(
            $"FF-Occam MCP host — {OccamToolProfile.GetExposedToolNames().Length} occam_* tools (profile {OccamToolProfile.Resolve()}), stdio default.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  OccamMcp.Core                    MCP over stdio (default)");
        writer.WriteLine("  OccamMcp.Core --mcp-server       WebSocket on 127.0.0.1:5050");
        writer.WriteLine("  OccamMcp.Core --mcp-server --port N");
        writer.WriteLine("  OccamMcp.Core --remote --tls-cert PATH --jwt-metadata-uri URI");
        writer.WriteLine("  OccamMcp.Core --remote --port N");
        writer.WriteLine("  OccamMcp.Core --batch-server     Batch HTTP on 127.0.0.1:5051");
        writer.WriteLine("  OccamMcp.Core --batch-server --port N");
        writer.WriteLine();
        writer.WriteLine("Offline verifier verbs (no transport — verify receipts without the host):");
        writer.WriteLine("  OccamMcp.Core keys export [--keys-root DIR]        Print this host's public key (PEM)");
        writer.WriteLine("  OccamMcp.Core verify --receipt F --pubkey F       Verify a receipt (+ --markdown F)");
        writer.WriteLine("  OccamMcp.Core verify --mode citation --receipt F --pubkey F --block-text T --proof F");
        writer.WriteLine("  OccamMcp.Core verify --mode manifest --input F --pubkey F   Verify a dataset_export");
        writer.WriteLine("  OccamMcp.Core verify --mode history  --input F --pubkey F   Verify a watch chain");
        writer.WriteLine("    (verify exit codes: 0 verified · 1 not verified · 2 usage; '-' reads stdin)");
        writer.WriteLine();
        writer.WriteLine("Setup verbs:");
        writer.WriteLine("  OccamMcp.Core install-browser                     User-level chromium for the browser backend");
        writer.WriteLine("    (exit: 0 browser ready · 1 install failed · 2 worker tree not found; JSON marker on stdout)");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -h, --help              Show this help (stderr, exit 0)");
        writer.WriteLine("  --mcp-server            Listen for MCP JSON-RPC over WebSocket");
        writer.WriteLine("  --remote                Listen for MCP over WSS (TLS + JWT auth)");
        writer.WriteLine("  --batch-server          Listen for batch submit API (experimental)");
        writer.WriteLine("  --port N                Port override (WS 5050, remote 8443, batch 5051)");
        writer.WriteLine("  --bind ADDR             Local modes: 127.0.0.1 only; remote: numeric IP (for example 0.0.0.0)");
        writer.WriteLine("  --tls-cert PATH         Path to TLS certificate (PFX or PEM)");
        writer.WriteLine("  --tls-password PASS     PFX password (omit for PEM)");
        writer.WriteLine("  --jwt-issuer ISSUER     JWT issuer (iss claim)");
        writer.WriteLine("  --jwt-audience AUD      JWT audience (aud claim)");
        writer.WriteLine("  --jwt-metadata-uri URI  HTTPS OpenID Connect metadata endpoint (or HTTPS issuer discovery)");
        writer.WriteLine();
        writer.WriteLine("Environment: OCCAM_HOME (required), OCCAM_BANNER, OCCAM_LOG — see docs/configuration.md");
        writer.WriteLine("Remote: OCCAM_TLS_CERT_PATH, OCCAM_TLS_CERT_PASSWORD, OCCAM_JWT_ISSUER, OCCAM_JWT_AUDIENCE, OCCAM_JWT_METADATA_URI, OCCAM_REMOTE_MAX_SESSIONS");
    }

    private static bool IsHttpsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
