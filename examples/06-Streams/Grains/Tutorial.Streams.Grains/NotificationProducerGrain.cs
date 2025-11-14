using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using OrleansX.Grains;

namespace Tutorial.Streams.Grains;

public class NotificationProducerGrain : StatelessGrainBase, INotificationProducerGrain
{
    private IAsyncStream<NotificationEvent>? _stream;

    public NotificationProducerGrain(ILogger<NotificationProducerGrain> logger)
        : base(logger)
    {
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("NotificationStream");
        _stream = streamProvider.GetStream<NotificationEvent>(
            StreamId.Create("Notifications", this.GetPrimaryKeyString()));

        Logger.LogInformation("Notification producer {ProducerId} activated",
            this.GetPrimaryKeyString());

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SendNotificationAsync(string userId, string message, NotificationType type)
    {
        var notification = new NotificationEvent
        {
            UserId = userId,
            Message = message,
            Type = type
            // EventId, Timestamp, EventType은 GrainEvent 베이스 클래스에서 자동 설정
        };

        await _stream!.OnNextAsync(notification);

        Logger.LogInformation("Notification sent: {Message} to user {UserId}, EventId: {EventId}",
            message, userId, notification.EventId);
    }

    public async Task SendBulkNotificationsAsync(List<NotificationEvent> notifications)
    {
        foreach (var notification in notifications)
        {
            await _stream!.OnNextAsync(notification);
        }

        Logger.LogInformation("Bulk notifications sent: {Count} notifications",
            notifications.Count);
    }
}
