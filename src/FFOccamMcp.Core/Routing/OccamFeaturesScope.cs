using System;
using System.Threading;

namespace OccamMcp.Core.Routing;

public sealed class OccamFeaturesScope : IDisposable
{
    private static readonly AsyncLocal<string?> CurrentFeatures = new();

    public static string? ActiveFeatures => CurrentFeatures.Value;

    private readonly string? _previousFeatures;

    private OccamFeaturesScope(string? features)
    {
        _previousFeatures = CurrentFeatures.Value;
        CurrentFeatures.Value = features;
    }

    public static OccamFeaturesScope Push(string? features)
    {
        return new OccamFeaturesScope(features);
    }

    public void Dispose()
    {
        CurrentFeatures.Value = _previousFeatures;
    }
}
