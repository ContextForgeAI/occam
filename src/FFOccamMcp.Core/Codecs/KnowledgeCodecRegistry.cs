namespace OccamMcp.Core.Codecs;

/// <summary>
/// In-process registry of knowledge codecs (ADR-0001 §4 / master PR-E). Built-ins are registered via
/// DI; third-party codecs may be added only through <see cref="TryRegisterExtension"/> when
/// <see cref="KnowledgeCodecExtensionOptions.AllowOptInExtensions"/> is true.
///
/// <para>No assembly scanning, marketplace, signatures, or sandbox yet — those are deferred. The
/// guaranteed default id is configured at construction (defaults to
/// <see cref="MarkdownPassthroughCodec.Id"/> for compatibility).</para>
/// </summary>
public sealed class KnowledgeCodecRegistry
{
    private readonly Dictionary<string, IKnowledgeCodec> _byId;
    private readonly KnowledgeCodecExtensionOptions _extensionOptions;
    private readonly string _defaultCodecId;
    private readonly object _gate = new();

    public KnowledgeCodecRegistry(
        IEnumerable<IKnowledgeCodec> codecs,
        KnowledgeCodecExtensionOptions? extensionOptions = null,
        string? defaultCodecId = null)
    {
        _extensionOptions = extensionOptions ?? KnowledgeCodecExtensionOptions.Disabled;
        _defaultCodecId = string.IsNullOrWhiteSpace(defaultCodecId)
            ? MarkdownPassthroughCodec.Id
            : defaultCodecId.Trim();
        _byId = new Dictionary<string, IKnowledgeCodec>(StringComparer.OrdinalIgnoreCase);
        foreach (var codec in codecs)
        {
            ValidateDescriptor(codec.Descriptor, requireBuiltinOrExperimental: true);
            _byId[codec.Descriptor.CodecId] = codec;
        }

        if (!_byId.TryGetValue(_defaultCodecId, out var defaultCodec))
        {
            throw new InvalidOperationException(
                $"KnowledgeCodecRegistry requires the default codec '{_defaultCodecId}' to be registered.");
        }

        if (defaultCodec.Descriptor.Trust != KnowledgeCodecTrust.Builtin)
        {
            throw new InvalidOperationException(
                $"Default codec '{_defaultCodecId}' must declare Trust={nameof(KnowledgeCodecTrust.Builtin)}.");
        }
    }

    /// <summary>Configured guaranteed default codec id.</summary>
    public string DefaultCodecId => _defaultCodecId;

    /// <summary>The guaranteed built-in default.</summary>
    public IKnowledgeCodec Default => _byId[_defaultCodecId];

    public KnowledgeCodecExtensionOptions ExtensionOptions => _extensionOptions;

    /// <summary>
    /// Lookup by id. Empty/whitespace id does <b>not</b> select the default — use
    /// <see cref="KnowledgeCodecSelector"/> for selection rules. Unknown ids return false with a null codec
    /// (no silent fallback).
    /// </summary>
    public bool TryGet(string codecId, out IKnowledgeCodec? codec)
    {
        if (string.IsNullOrWhiteSpace(codecId))
        {
            codec = null;
            return false;
        }

        lock (_gate)
        {
            return _byId.TryGetValue(codecId, out codec);
        }
    }

    public bool IsRegistered(string codecId)
    {
        if (string.IsNullOrWhiteSpace(codecId))
        {
            return false;
        }

        lock (_gate)
        {
            return _byId.ContainsKey(codecId);
        }
    }

    /// <summary>Descriptors of every registered codec (for discovery / benchmark metadata).</summary>
    public IReadOnlyCollection<KnowledgeCodecDescriptor> Descriptors
    {
        get
        {
            lock (_gate)
            {
                return _byId.Values.Select(c => c.Descriptor).ToArray();
            }
        }
    }

    /// <summary>
    /// Explicit opt-in registration for <see cref="KnowledgeCodecTrust.OptInExtension"/> codecs.
    /// Fail-closed when extensions are disabled, trust is wrong, id collides, or the descriptor is invalid.
    /// Never replaces the configured default codec id.
    /// </summary>
    public bool TryRegisterExtension(IKnowledgeCodec codec, out string? failureCode)
    {
        ArgumentNullException.ThrowIfNull(codec);

        if (!_extensionOptions.AllowOptInExtensions)
        {
            failureCode = KnowledgeCodecFailureCodes.CodecExtensionNotAllowed;
            return false;
        }

        var d = codec.Descriptor;
        if (d.Trust != KnowledgeCodecTrust.OptInExtension)
        {
            failureCode = KnowledgeCodecFailureCodes.InvalidCodecDescriptor;
            return false;
        }

        if (!TryValidateDescriptor(d, out failureCode))
        {
            return false;
        }

        if (string.Equals(d.CodecId, _defaultCodecId, StringComparison.OrdinalIgnoreCase))
        {
            failureCode = KnowledgeCodecFailureCodes.CodecAlreadyRegistered;
            return false;
        }

        lock (_gate)
        {
            if (_byId.ContainsKey(d.CodecId))
            {
                failureCode = KnowledgeCodecFailureCodes.CodecAlreadyRegistered;
                return false;
            }

            if (_extensionOptions.AllowedExtensionIds is { Count: > 0 }
                && !_extensionOptions.AllowedExtensionIds.Contains(d.CodecId))
            {
                failureCode = KnowledgeCodecFailureCodes.CodecExtensionNotAllowed;
                return false;
            }

            _byId[d.CodecId] = codec;
        }

        failureCode = null;
        return true;
    }

    private static void ValidateDescriptor(KnowledgeCodecDescriptor d, bool requireBuiltinOrExperimental)
    {
        if (!TryValidateDescriptor(d, out var failure))
        {
            throw new ArgumentException(failure ?? KnowledgeCodecFailureCodes.InvalidCodecDescriptor);
        }

        if (requireBuiltinOrExperimental
            && d.Trust is not (KnowledgeCodecTrust.Builtin or KnowledgeCodecTrust.BuiltinExperimental))
        {
            throw new ArgumentException(
                $"DI/bootstrap codecs must be Builtin or BuiltinExperimental; got Trust={d.Trust} for '{d.CodecId}'.");
        }
    }

    private static bool TryValidateDescriptor(KnowledgeCodecDescriptor d, out string? failureCode)
    {
        if (string.IsNullOrWhiteSpace(d.CodecId)
            || string.IsNullOrWhiteSpace(d.Version)
            || string.IsNullOrWhiteSpace(d.SupportedIrVersion))
        {
            failureCode = KnowledgeCodecFailureCodes.InvalidCodecDescriptor;
            return false;
        }

        failureCode = null;
        return true;
    }
}

/// <summary>
/// Extension policy for the codec registry (master PR-E). Default is disabled — third-party codecs
/// never load implicitly. No marketplace / remote fetch.
/// </summary>
public sealed record KnowledgeCodecExtensionOptions(
    bool AllowOptInExtensions = false,
    IReadOnlySet<string>? AllowedExtensionIds = null)
{
    public static readonly KnowledgeCodecExtensionOptions Disabled = new();
}
