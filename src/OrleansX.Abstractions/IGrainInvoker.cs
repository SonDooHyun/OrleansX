using Orleans;

namespace OrleansX.Abstractions;

/// <summary>
/// Grain 호출을 위한 파사드 인터페이스
/// </summary>
public interface IGrainInvoker
{
    /// <summary>
    /// 지정된 키로 Grain을 가져옵니다.
    /// </summary>
    TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey;

    /// <summary>
    /// 지정된 키로 Grain을 가져옵니다.
    /// </summary>
    TGrain GetGrain<TGrain>(Guid key) where TGrain : IGrainWithGuidKey;

    /// <summary>
    /// 지정된 키로 Grain을 가져옵니다.
    /// </summary>
    TGrain GetGrain<TGrain>(long key) where TGrain : IGrainWithIntegerKey;

    /// <summary>
    /// 복합 키(Guid + 문자열)로 Grain을 가져옵니다.
    /// </summary>
    TGrain GetGrain<TGrain>(Guid key, string keyExtension) where TGrain : IGrainWithGuidCompoundKey;
}

