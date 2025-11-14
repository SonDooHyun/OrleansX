using Orleans;

namespace Tutorial.Streams.Grains;

public interface INotificationConsumerGrain : IGrainWithStringKey
{
    Task StartListeningAsync();
    Task StopListeningAsync();
    Task<List<NotificationEvent>> GetReceivedNotificationsAsync();
}
