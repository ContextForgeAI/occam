namespace OccamMcp.Core.Probe;

public static class ProbeHttpHeaders
{
    public static void Apply(HttpClient client, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return;
        }

        foreach (var (name, value) in headers)
        {
            if (string.Equals(name, "User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(value);
                continue;
            }

            if (!client.DefaultRequestHeaders.TryAddWithoutValidation(name, value))
            {
                client.DefaultRequestHeaders.Remove(name);
                client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
            }
        }
    }
}
