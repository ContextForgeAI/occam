using System.Threading.Tasks;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Workers;

/// <summary>
/// Unified browser concurrency limiter combining global max-parallel and pool slot gates.
/// Replaces the dual-semaphore pattern (BrowserConcurrencyGate + BrowserPoolManager._slotSemaphore).
/// </summary>
public sealed class BrowserConcurrencyLimiter : IDisposable
{
    private readonly SemaphoreSlim _globalGate;
    private readonly SemaphoreSlim _poolGate;
    private bool _disposed;

    public BrowserConcurrencyLimiter(BrowserPoolSettings settings)
    {
        var maxParallel = Math.Min(settings.MaxParallel, settings.PoolSize);
        _globalGate = new SemaphoreSlim(maxParallel, maxParallel);
        _poolGate = new SemaphoreSlim(settings.PoolSize, settings.PoolSize);
    }

    public async ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _globalGate.WaitAsync(cancellationToken);
        try
        {
            await _poolGate.WaitAsync(cancellationToken);
        }
        catch
        {
            _globalGate.Release();
            throw;
        }

        return new Releaser(this);
    }

    public static int ResolveMaxParallel()
        => OccamEnvironment.GetInt("OCCAM_BROWSER_MAX_PARALLEL", defaultValue: 2, min: 1, max: 16, fallback: "WT_BROWSER_MAX_PARALLEL");

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _globalGate.Dispose();
            _poolGate.Dispose();
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly BrowserConcurrencyLimiter _parent;
        private int _disposed;
        public Releaser(BrowserConcurrencyLimiter parent) => _parent = parent;

        // Idempotent: the extract runner can release the same slot from both its catch and
        // finally on cancellation. Releasing the semaphores twice would over-count the gates.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _parent._poolGate.Release();
            _parent._globalGate.Release();
        }
    }
}