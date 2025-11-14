using Orleans;
using OrleansX.Abstractions.Events;

namespace Tutorial.Streams.Grains;

[GenerateSerializer]
public class NotificationEvent : GrainEvent
{
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    public string Message { get; set; } = string.Empty;

    [Id(2)]
    public NotificationType Type { get; set; }

    public NotificationEvent()
    {
        EventType = nameof(NotificationEvent);
    }
}

[GenerateSerializer]
public enum NotificationType
{
    [Id(0)]
    Info,

    [Id(1)]
    Warning,

    [Id(2)]
    Error,

    [Id(3)]
    Success
}
