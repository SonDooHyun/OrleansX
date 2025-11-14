using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using OrleansX.Grains;

namespace Tutorial.Streams.Grains;

[GenerateSerializer]
public class NotificationConsumerState
{
    [Id(0)]
    public List<NotificationEvent> ReceivedNotifications { get; set; } = new();
}

public class NotificationConsumerGrain :
    StatefulGrainBase<NotificationConsumerState>,
    INotificationConsumerGrain
{
    private StreamSubscriptionHandle<NotificationEvent>? _subscriptionHandle;

    public NotificationConsumerGrain(
        [PersistentState("consumer")] IPersistentState<NotificationConsumerState> state,
        ILogger<NotificationConsumerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task StartListeningAsync()
    {
        if (_subscriptionHandle != null)
        {
            Logger.LogWarning("Already listening to notifications");
            return;
        }

        var streamProvider = this.GetStreamProvider("NotificationStream");
        var stream = streamProvider.GetStream<NotificationEvent>(
            StreamId.Create("Notifications", this.GetPrimaryKeyString()));

        _subscriptionHandle = await stream.SubscribeAsync(OnNotificationReceivedAsync);

        Logger.LogInformation("Consumer {ConsumerId} started listening",
            this.GetPrimaryKeyString());
    }

    public async Task StopListeningAsync()
    {
        if (_subscriptionHandle != null)
        {
            await _subscriptionHandle.UnsubscribeAsync();
            _subscriptionHandle = null;

            Logger.LogInformation("Consumer {ConsumerId} stopped listening",
                this.GetPrimaryKeyString());
        }
    }

    public Task<List<NotificationEvent>> GetReceivedNotificationsAsync()
    {
        return Task.FromResult(State.ReceivedNotifications.ToList());
    }

    private async Task OnNotificationReceivedAsync(
        NotificationEvent notification,
        StreamSequenceToken? token)
    {
        Logger.LogInformation(
            "Notification received: EventId={EventId}, Type={Type}, Message={Message}, User={UserId}, Timestamp={Timestamp}",
            notification.EventId, notification.Type, notification.Message,
            notification.UserId, notification.Timestamp);

        State.ReceivedNotifications.Add(notification);
        await SaveStateAsync();
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        if (_subscriptionHandle != null)
        {
            await _subscriptionHandle.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}
