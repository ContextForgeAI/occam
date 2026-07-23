using OccamMcp.Core.Transport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;

namespace OccamMcp.L0Gate;

internal static class L2TransportUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunCliParse(assert);
        RunToolRegistry(assert);
        RunContentLengthFraming(assert);
        RunRemoteConfiguration(assert);
    }

    private static void RunRemoteConfiguration(Action<string, bool> assert)
    {
        var oldMetadata = Environment.GetEnvironmentVariable("OCCAM_JWT_METADATA_URI");
        var oldJwks = Environment.GetEnvironmentVariable("OCCAM_JWT_JWKS_URI");
        var oldMessageLimit = Environment.GetEnvironmentVariable(McpWebSocketLimits.MaxMessageBytesVariable);
        var certPath = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_JWT_METADATA_URI", null);
            Environment.SetEnvironmentVariable("OCCAM_JWT_JWKS_URI", null);

            var missingMetadata = OccamMcpCli.Parse([
                "--remote", "--tls-cert", certPath, "--jwt-issuer", "occam-mcp",
            ]);
            assert("transport remote requires key discovery", missingMetadata.FailureKind == "remote_requires_jwt_metadata");

            var remote = OccamMcpCli.Parse([
                "--remote", "--bind", "0.0.0.0", "--tls-cert", certPath,
                "--jwt-metadata-uri", "https://identity.example/.well-known/openid-configuration",
            ]);
            assert("transport remote public bind", remote.IsValid && remote.BindAddress == "0.0.0.0");
            assert("transport remote metadata", remote.RemoteAuth?.JwtMetadataUri?.StartsWith("https://", StringComparison.Ordinal) == true);

            var insecureMetadata = OccamMcpCli.Parse([
                "--remote", "--tls-cert", certPath,
                "--jwt-metadata-uri", "http://identity.example/.well-known/openid-configuration",
            ]);
            assert("transport remote rejects insecure metadata", insecureMetadata.FailureKind == "invalid_jwt_metadata_uri");

            var options = new JwtBearerOptions();
            var defaultEvents = new JwtBearerEvents();
            RemoteMcpTransport.ConfigureJwtBearerOptions(options, remote.RemoteAuth!);
            assert("transport remote validates signing key", options.TokenValidationParameters.ValidateIssuerSigningKey);
            assert("transport remote requires token expiry", options.TokenValidationParameters.RequireExpirationTime);
            assert("transport remote metadata configured", options.MetadataAddress == remote.RemoteAuth!.JwtMetadataUri);
            assert("transport remote no query-token hook", options.Events.OnMessageReceived.Method == defaultEvents.OnMessageReceived.Method);
            assert("transport remote ipv6 authority", RemoteMcpTransport.FormatListenHost("::") == "[::]");

            var queryContext = new DefaultHttpContext();
            queryContext.Request.QueryString = new QueryString("?token=must-not-be-accepted");
            assert("transport remote rejects query token", RemoteMcpTransport.HasForbiddenQueryToken(queryContext.Request));

            Environment.SetEnvironmentVariable(McpWebSocketLimits.MaxMessageBytesVariable, "65536");
            assert("transport websocket message cap", McpWebSocketLimits.ReadMaxMessageBytes() == 65536);

            using var binarySocket = new ScriptedWebSocket([
                new SocketFrame([1, 2, 3], WebSocketMessageType.Binary, true),
            ]);
            using var binaryInput = new WebSocketMcpInputStream(binarySocket);
            assert("transport websocket rejects binary", ThrowsInvalidData(() => binaryInput.ReadExactly(new byte[1], 0, 1)));

            using var oversizeSocket = new ScriptedWebSocket([
                new SocketFrame(new byte[40_000], WebSocketMessageType.Text, false),
                new SocketFrame(new byte[30_000], WebSocketMessageType.Text, true),
            ]);
            using var oversizeInput = new WebSocketMcpInputStream(oversizeSocket);
            assert("transport websocket rejects oversize", ThrowsInvalidData(() => oversizeInput.ReadExactly(new byte[1], 0, 1)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_JWT_METADATA_URI", oldMetadata);
            Environment.SetEnvironmentVariable("OCCAM_JWT_JWKS_URI", oldJwks);
            Environment.SetEnvironmentVariable(McpWebSocketLimits.MaxMessageBytesVariable, oldMessageLimit);
            File.Delete(certPath);
        }
    }

    private static bool ThrowsInvalidData(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static void RunCliParse(Action<string, bool> assert)
    {
        var stdio = OccamMcpCli.Parse([]);
        assert("transport cli default stdio", stdio.Mode == OccamMcpTransportMode.Stdio && stdio.IsValid);

        var ws = OccamMcpCli.Parse(["--mcp-server"]);
        assert("transport cli mcp-server", ws.Mode == OccamMcpTransportMode.WebSocket && ws.IsValid);
        assert("transport cli default port", ws.Port == OccamMcpCli.DefaultWebSocketPort);
        assert("transport cli default bind", ws.BindAddress == OccamMcpCli.DefaultBindAddress);

        var customPort = OccamMcpCli.Parse(["--mcp-server", "--port", "5051"]);
        assert("transport cli custom port", customPort.Port == 5051 && customPort.IsValid);

        var invalidPort = OccamMcpCli.Parse(["--mcp-server", "--port", "0"]);
        assert("transport cli invalid port", !invalidPort.IsValid);
        assert("transport cli invalid kind", invalidPort.FailureKind == "invalid_arguments");

        var publicBind = OccamMcpCli.Parse(["--mcp-server", "--bind", "0.0.0.0"]);
        assert("transport cli reject public bind", !publicBind.IsValid);
    }

    private static void RunToolRegistry(Action<string, bool> assert)
    {
        var names = OccamMcpServerRegistration.OccamToolNames;
        assert("transport tool count", names.Length == OccamMcpServerRegistration.OccamToolNames.Length);
        assert("transport tool includes occam_search", names.Contains("occam_search"));
        assert("transport tool includes occam_verify", names.Contains("occam_verify"));
        assert("transport tool includes occam_claim_check", names.Contains("occam_claim_check"));
        assert("transport tool includes occam_attest", names.Contains("occam_attest"));
        assert("transport tool includes occam_playbook_lint", names.Contains("occam_playbook_lint"));
        assert("transport tool includes occam_dataset_export", names.Contains("occam_dataset_export"));
        assert("transport tool prefix", names.All(name => name.StartsWith("occam_", StringComparison.Ordinal)));
    }

    private static void RunContentLengthFraming(Action<string, bool> assert)
    {
        const string json = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";
        var framed = McpContentLengthFraming.Frame(json);
        using var stream = new MemoryStream(framed);
        assert("transport framing extract", McpContentLengthFraming.TryExtractMessage(stream, out var roundTrip));
        assert("transport framing roundtrip", roundTrip == json);
    }
}

internal sealed record SocketFrame(byte[] Data, WebSocketMessageType MessageType, bool EndOfMessage);

internal sealed class ScriptedWebSocket(IEnumerable<SocketFrame> frames) : WebSocket
{
    private readonly Queue<SocketFrame> _frames = new(frames);
    private WebSocketState _state = WebSocketState.Open;

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
    public override WebSocketState State => _state;

    public override void Abort() => _state = WebSocketState.Aborted;

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Dispose() => _state = WebSocketState.Closed;

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (_frames.Count == 0)
        {
            return Task.FromResult(new WebSocketReceiveResult(
                0,
                WebSocketMessageType.Close,
                true,
                WebSocketCloseStatus.NormalClosure,
                null));
        }

        var frame = _frames.Dequeue();
        if (frame.Data.Length > buffer.Count)
        {
            throw new InvalidOperationException("Scripted frame exceeds receive buffer.");
        }

        frame.Data.CopyTo(buffer.Array!, buffer.Offset);
        return Task.FromResult(new WebSocketReceiveResult(
            frame.Data.Length,
            frame.MessageType,
            frame.EndOfMessage));
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
