using System.Text;

namespace OccamMcp.Core.Transport;

internal static class McpContentLengthFraming
{
    public static byte[] Frame(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var header = Encoding.UTF8.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        var framed = new byte[header.Length + payload.Length];
        header.CopyTo(framed, 0);
        payload.CopyTo(framed, header.Length);
        return framed;
    }

    public static bool TryExtractMessage(MemoryStream accumulator, out string json)
    {
        json = "";
        if (accumulator.Length < 16)
        {
            return false;
        }

        var bytes = accumulator.ToArray();
        var headerEnd = IndexOf(bytes, "\r\n\r\n"u8);
        if (headerEnd < 0)
        {
            return false;
        }

        var header = Encoding.UTF8.GetString(bytes, 0, headerEnd);
        if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lengthText = header["Content-Length:".Length..].Trim();
        if (!int.TryParse(lengthText, out var contentLength) || contentLength < 0)
        {
            return false;
        }

        var bodyStart = headerEnd + 4;
        if (bytes.Length < bodyStart + contentLength)
        {
            return false;
        }

        json = Encoding.UTF8.GetString(bytes, bodyStart, contentLength);
        var remaining = bytes.Length - (bodyStart + contentLength);
        accumulator.SetLength(0);
        if (remaining > 0)
        {
            accumulator.Write(bytes, bodyStart + contentLength, remaining);
        }

        return true;
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length > haystack.Length)
        {
            return -1;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }
}
