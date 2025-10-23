using Orleans;
using OrleansX.Abstractions;

namespace OrleansX.Client;

/// <summary>
/// Grain 호출을 위한 파사드 구현
/// </summary>
public class GrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;

    public GrainInvoker(IClusterClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        return _client.GetGrain<TGrain>(key);
    }

    /// <inheritdoc/>
    public TGrain GetGrain<TGrain>(Guid key) where TGrain : IGrainWithGuidKey
    {
        if (key == Guid.Empty)
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return _client.GetGrain<TGrain>(key);
    }

    /// <inheritdoc/>
    public TGrain GetGrain<TGrain>(long key) where TGrain : IGrainWithIntegerKey
    {
        return _client.GetGrain<TGrain>(key);
    }

    /// <inheritdoc/>
    public TGrain GetGrain<TGrain>(Guid key, string keyExtension) where TGrain : IGrainWithGuidCompoundKey
    {
        if (key == Guid.Empty)
            throw new ArgumentException("Key cannot be empty", nameof(key));
        if (string.IsNullOrWhiteSpace(keyExtension))
            throw new ArgumentException("Key extension cannot be null or empty", nameof(keyExtension));

        return _client.GetGrain<TGrain>(key, keyExtension);
    }
}



