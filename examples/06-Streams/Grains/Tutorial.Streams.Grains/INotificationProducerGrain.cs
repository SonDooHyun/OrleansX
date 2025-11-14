using Orleans;

namespace Tutorial.Streams.Grains;

public interface INotificationProducerGrain : IGrainWithStringKey
{
    Task SendNotificationAsync(string userId, string message, NotificationType type);
    Task SendBulkNotificationsAsync(List<NotificationEvent> notifications);
}
