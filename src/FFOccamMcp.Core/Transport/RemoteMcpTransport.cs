using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Remote MCP transport over WSS (WebSocket Secure) with JWT Bearer authentication.
/// TLS termination via Kestrel; JWT validation via standard ASP.NET Core middleware.
/// </summary>
public sealed class RemoteMcpTransport : IMcpTransport
{
    private readonly OccamMcpCli _cli;
    private readonly RemoteMcpAuthOptions _auth;
    private readonly int _maxSessions;
    private readonly SemaphoreSlim _sessionSlots;
    private WebApplication? _app;

    public RemoteMcpTransport(OccamMcpCli cli)
    {
        _cli = cli;
        _auth = cli.RemoteAuth ?? throw new InvalidOperationException("RemoteAuth options required for --remote mode.");
        _maxSessions = RemoteMcpAuthOptions.ReadMaxSessions();
        _sessionSlots = new SemaphoreSlim(_maxSessions);
    }

    public string ListenUrl => $"https://{FormatListenHost(_cli.BindAddress)}:{_cli.Port}/";

    public async IAsyncEnumerable<string> ReadRequestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // RemoteMcpTransport uses MCP SDK's built-in host per session.
        // This method is not used directly — sessions are handled by RunSingleSessionAsync.
        // Yield break to satisfy the interface.
        yield break;
    }

    public Task SendResponseAsync(string jsonResponse, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();

        // Kestrel HTTPS configuration
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Parse(_cli.BindAddress), _cli.Port, listenOptions =>
            {
                var cert = LoadCertificate(_auth);
                listenOptions.UseHttps(cert);
            });
        });

        builder.Logging.SetMinimumLevel(LogLevel.None);

        // Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearerOptions(options, _auth));

        builder.Services.AddAuthorization();

        // MCP server registration
        builder.Services.AddOccamMcpServer();

        var app = builder.Build();

        app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Map("/mcp", async context =>
        {
            if (HasForbiddenQueryToken(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(
                    new OccamMcp.Core.Tools.OccamTransportErrorResponse(
                        "query_token_forbidden",
                        "Send the access token in the Authorization: Bearer header, never in the URI query string"),
                    OccamMcp.Core.Tools.OccamTranscodeJsonContext.Default.OccamTransportErrorResponse);
                return;
            }

            // Require authenticated user
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.WWWAuthenticate = $"Bearer realm=\"occam-mcp\", error=\"invalid_token\"";
                await context.Response.WriteAsJsonAsync(
                    new OccamMcp.Core.Tools.OccamTransportErrorResponse("unauthorized", "Valid JWT Bearer token required"),
                    OccamMcp.Core.Tools.OccamTranscodeJsonContext.Default.OccamTransportErrorResponse);
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new OccamMcp.Core.Tools.OccamTransportErrorResponse("bad_request", "WebSocket upgrade required"),
                    OccamMcp.Core.Tools.OccamTranscodeJsonContext.Default.OccamTransportErrorResponse);
                return;
            }

            if (!_sessionSlots.Wait(0))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.RetryAfter = "1";
                await context.Response.WriteAsJsonAsync(
                    new OccamMcp.Core.Tools.OccamTransportErrorResponse(
                        "remote_capacity_exceeded",
                        $"Remote MCP session limit reached ({_maxSessions})"),
                    OccamMcp.Core.Tools.OccamTranscodeJsonContext.Default.OccamTransportErrorResponse);
                return;
            }

            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    cancellationToken);
                await RunSingleSessionAsync(socket, sessionCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                _sessionSlots.Release();
            }
        });

        // Health check endpoint (no auth required)
        app.Map("/health", () => Results.Ok(new { ok = true, mode = "remote", transport = "wss" }));

        _app = app;
        Console.Error.WriteLine($"remote_mcp_listening: {ListenUrl}");
        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    private static X509Certificate2 LoadCertificate(RemoteMcpAuthOptions auth)
    {
        var certPath = auth.TlsCertPath;

        if (certPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            certPath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
        {
            // PFX/P12 — requires password
            return string.IsNullOrEmpty(auth.TlsCertPassword)
                ? X509CertificateLoader.LoadPkcs12FromFile(certPath, null)
                : X509CertificateLoader.LoadPkcs12FromFile(certPath, auth.TlsCertPassword);
        }

        // PEM — load as X509Certificate2 (supports .crt, .pem, .cer)
        return X509Certificate2.CreateFromPemFile(certPath);
    }

    internal static void ConfigureJwtBearerOptions(JwtBearerOptions options, RemoteMcpAuthOptions auth)
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        if (!string.IsNullOrWhiteSpace(auth.JwtMetadataUri))
        {
            options.MetadataAddress = auth.JwtMetadataUri;
        }
        else
        {
            options.Authority = auth.JwtIssuer.TrimEnd('/');
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = auth.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = auth.JwtAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireSignedTokens = true,
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.Error.WriteLine($"auth_failed: {context.Exception.GetType().Name}");
                return Task.CompletedTask;
            },
        };
    }

    internal static string FormatListenHost(string bindAddress) =>
        bindAddress.Contains(':', StringComparison.Ordinal) ? $"[{bindAddress}]" : bindAddress;

    internal static bool HasForbiddenQueryToken(HttpRequest request) =>
        request.Query.ContainsKey("token") || request.Query.ContainsKey("access_token");

    private static async Task RunSingleSessionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var input = new WebSocketMcpInputStream(socket);
        using var output = new WebSocketMcpOutputStream(socket);
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Logging.SetMinimumLevel(LogLevel.None);
        hostBuilder.Services.AddOccamMcpServer().WithStreamServerTransport(input, output);
        using var host = hostBuilder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
