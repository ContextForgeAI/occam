namespace OccamMcp.Core.Services;

/// <summary>Round-robin <see cref="IProxyRotationService"/> over a static proxy list.</summary>
public sealed class RoundRobinProxyRotationService : IProxyRotationService
{
    private readonly string[] _proxies;
    private int _index;

    public RoundRobinProxyRotationService()
        : this(ProxyListParser.LoadFromEnvironment())
    {
    }

    internal RoundRobinProxyRotationService(IReadOnlyList<string> proxyUrls)
    {
        _proxies = proxyUrls.Count == 0 ? [] : proxyUrls.ToArray();
    }

    public bool IsConfigured => _proxies.Length > 0;

    public int Count => _proxies.Length;

    public RotatedProxy? AcquireNext()
    {
        if (_proxies.Length == 0)
        {
            return null;
        }

        var slot = Interlocked.Increment(ref _index);
        var proxy = _proxies[(slot - 1) % _proxies.Length];
        return new RotatedProxy(proxy);
    }
}
