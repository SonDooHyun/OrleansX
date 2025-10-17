using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace OrleansX.Grains.Utilities;

/// <summary>
/// Orleans Stream 헬퍼 클래스
/// </summary>
public class StreamHelper
{
    private readonly Grain _grain;

    public StreamHelper(Grain grain)
    {
        _grain = grain ?? throw new ArgumentNullException(nameof(grain));
    }

    /// <summary>
    /// Stream Provider를 가져옵니다.
    /// </summary>
    public IStreamProvider GetStreamProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        return _grain.GetStreamProvider(providerName);
    }

    /// <summary>
    /// Stream을 가져옵니다.
    /// </summary>
    public IAsyncStream<T> GetStream<T>(string providerName, StreamId streamId)
    {
        var provider = GetStreamProvider(providerName);
        return provider.GetStream<T>(streamId);
    }

    /// <summary>
    /// Stream을 가져옵니다.
    /// </summary>
    public IAsyncStream<T> GetStream<T>(string providerName, string streamNamespace, Guid streamKey)
    {
        var streamId = StreamId.Create(streamNamespace, streamKey);
        return GetStream<T>(providerName, streamId);
    }

    /// <summary>
    /// Stream을 구독합니다.
    /// </summary>
    public async Task<StreamSubscriptionHandle<T>> SubscribeAsync<T>(
        IAsyncStream<T> stream,
        Func<T, StreamSequenceToken?, Task> onNext,
        Func<Exception, Task>? onError = null,
        Func<Task>? onCompleted = null)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (onNext == null)
            throw new ArgumentNullException(nameof(onNext));

        var observer = new StreamObserver<T>(onNext, onError, onCompleted);
        return await stream.SubscribeAsync(observer);
    }

    /// <summary>
    /// Stream에 메시지를 발행합니다.
    /// </summary>
    public Task PublishAsync<T>(IAsyncStream<T> stream, T item)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        return stream.OnNextAsync(item);
    }

    private class StreamObserver<T> : IAsyncObserver<T>
    {
        private readonly Func<T, StreamSequenceToken?, Task> _onNext;
        private readonly Func<Exception, Task>? _onError;
        private readonly Func<Task>? _onCompleted;

        public StreamObserver(
            Func<T, StreamSequenceToken?, Task> onNext,
            Func<Exception, Task>? onError,
            Func<Task>? onCompleted)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public Task OnNextAsync(T item, StreamSequenceToken? token = null)
        {
            return _onNext(item, token);
        }

        public Task OnCompletedAsync()
        {
            return _onCompleted?.Invoke() ?? Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            return _onError?.Invoke(ex) ?? Task.CompletedTask;
        }
    }
}

