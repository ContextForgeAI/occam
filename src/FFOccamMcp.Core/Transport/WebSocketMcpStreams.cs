using System.Net.WebSockets;
using System.Text;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Transport;

internal sealed class WebSocketMcpInputStream : Stream
{
    private readonly WebSocket _socket;
    private readonly byte[] _receiveBuffer = new byte[64 * 1024];
    private readonly Queue<byte> _pending = new();
    private readonly int _maxMessageBytes;
    private bool _endOfStream;

    public WebSocketMcpInputStream(WebSocket socket)
    {
        _socket = socket;
        _maxMessageBytes = McpWebSocketLimits.ReadMaxMessageBytes();
    }

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        while (_pending.Count < count && !_endOfStream)
        {
            if (!await FillNextFrameAsync(cancellationToken).ConfigureAwait(false))
            {
                _endOfStream = true;
                break;
            }
        }

        var read = 0;
        while (read < count && _pending.Count > 0)
        {
            buffer[offset + read] = _pending.Dequeue();
            read++;
        }

        return read;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private async Task<bool> FillNextFrameAsync(CancellationToken cancellationToken)
    {
        var json = await ReceiveTextFrameAsync(cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return false;
        }

        foreach (var b in McpContentLengthFraming.Frame(json))
        {
            _pending.Enqueue(b);
        }

        return true;
    }

    private async Task<string?> ReceiveTextFrameAsync(CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(_receiveBuffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidDataException("MCP WebSocket transport accepts text messages only.");
            }

            if (memory.Length + result.Count > _maxMessageBytes)
            {
                throw new InvalidDataException(
                    $"MCP WebSocket message exceeds {_maxMessageBytes} bytes.");
            }

            memory.Write(_receiveBuffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(memory.ToArray());
    }
}

internal static class McpWebSocketLimits
{
    public const string MaxMessageBytesVariable = "OCCAM_MCP_MAX_MESSAGE_BYTES";

    public static int ReadMaxMessageBytes() =>
        OccamEnvironment.GetInt(
            MaxMessageBytesVariable,
            defaultValue: 4 * 1024 * 1024,
            min: 64 * 1024,
            max: 16 * 1024 * 1024);
}

internal sealed class WebSocketMcpOutputStream : Stream
{
    private readonly WebSocket _socket;
    private readonly MemoryStream _accumulator = new();

    public WebSocketMcpOutputStream(WebSocket socket) => _socket = socket;

    public override bool CanRead => false;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _accumulator.Write(buffer, offset, count);
        while (McpContentLengthFraming.TryExtractMessage(_accumulator, out var json))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
