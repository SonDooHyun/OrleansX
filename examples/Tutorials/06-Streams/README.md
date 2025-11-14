# Tutorial 06: Streams

## 개요

Orleans Streams는 비동기 데이터 스트림을 처리하는 기능입니다. Pub/Sub 패턴을 지원하며, 실시간 이벤트 처리에 적합합니다.

## 언제 사용하나요?

- 실시간 알림
- 이벤트 소싱
- 로그 집계
- 채팅 메시지
- IoT 센서 데이터

## 기본 개념

### Stream Provider
스트림 저장소 (메모리, Azure Event Hub, Kafka 등)

### Stream Namespace
스트림을 그룹화하는 논리적 단위

### Stream Id
개별 스트림 식별자

## 예제: 실시간 알림 시스템

### 1. 이벤트 모델 정의

```csharp
using Orleans;
using OrleansX.Abstractions.Events;

namespace OrleansX.Tutorials.Streams;

// GrainEvent를 상속받아 이벤트 기본 필드 활용
// - EventId: 이벤트 고유 ID (자동 생성)
// - Timestamp: 이벤트 발생 시간 (자동 설정)
// - EventType: 이벤트 타입 (명시적 설정)
// - CorrelationId: 분산 추적용 상관관계 ID (옵션)
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
        // EventType 명시적 설정 (디버깅/로깅에 유용)
        EventType = nameof(NotificationEvent);
    }
}

// 💡 GrainEvent를 상속받으면:
// 1. EventId: 개별 이벤트 고유 식별자 (자동 생성)
// 2. Timestamp: 이벤트 발생 시간 (자동 설정)
// 3. EventType: 이벤트 타입 (명시적 설정)
// 4. CorrelationId: 여러 관련 이벤트를 하나의 작업으로 묶어서 추적
//    - 예: 주문 생성 → 결제 → 배송 이벤트들을 주문 ID로 연결
//    - 예: 사용자 요청 → 여러 Grain 호출 → 응답을 요청 ID로 추적

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
```

### 2. Producer Grain (발행자)

```csharp
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using OrleansX.Grains;

namespace OrleansX.Tutorials.Streams;

public interface INotificationProducerGrain : IGrainWithStringKey
{
    Task SendNotificationAsync(string userId, string message, NotificationType type);
    Task SendBulkNotificationsAsync(List<NotificationEvent> notifications);
}

public class NotificationProducerGrain : StatelessGrainBase, INotificationProducerGrain
{
    private IAsyncStream<NotificationEvent>? _stream;

    public NotificationProducerGrain(ILogger<NotificationProducerGrain> logger)
        : base(logger)
    {
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Stream 획득
        var streamProvider = this.GetStreamProvider("NotificationStream");
        _stream = streamProvider.GetStream<NotificationEvent>(
            streamNamespace: "Notifications",
            streamId: this.GetPrimaryKeyString());

        Logger.LogInformation("Notification producer {ProducerId} activated",
            this.GetPrimaryKeyString());

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SendNotificationAsync(
        string userId,
        string message,
        NotificationType type)
    {
        var notification = new NotificationEvent
        {
            UserId = userId,
            Message = message,
            Type = type
            // EventId, Timestamp, EventType은 GrainEvent에서 자동 설정
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
```

### 3. Consumer Grain (구독자)

```csharp
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using OrleansX.Grains;

namespace OrleansX.Tutorials.Streams;

public interface INotificationConsumerGrain : IGrainWithStringKey
{
    Task StartListeningAsync();
    Task StopListeningAsync();
    Task<List<NotificationEvent>> GetReceivedNotificationsAsync();
}

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
            streamNamespace: "Notifications",
            streamId: this.GetPrimaryKeyString());

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
            "Notification received: {Type} - {Message} for user {UserId}",
            notification.Type, notification.Message, notification.UserId);

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
```

### 4. StreamHelper 사용

```csharp
using Orleans.Streams;
using OrleansX.Grains.Utilities;

namespace OrleansX.Tutorials.Streams;

public class StreamService
{
    private readonly IStreamProvider _streamProvider;

    public StreamService(IClusterClient client)
    {
        _streamProvider = client.GetStreamProvider("NotificationStream");
    }

    public async Task PublishNotificationAsync(
        string streamId,
        NotificationEvent notification)
    {
        // StreamHelper를 사용한 간편한 발행
        await StreamHelper.PublishAsync(
            _streamProvider,
            "Notifications",
            streamId,
            notification);
    }

    public async Task SubscribeToNotificationsAsync(
        string streamId,
        Func<NotificationEvent, Task> handler)
    {
        // StreamHelper를 사용한 간편한 구독
        await StreamHelper.SubscribeAsync(
            _streamProvider,
            "Notifications",
            streamId,
            async (notification, token) =>
            {
                await handler(notification);
            });
    }
}
```

## 고급 패턴

### 1. Implicit Subscription

```csharp
using Orleans.Streams;

namespace OrleansX.Tutorials.Streams;

[ImplicitStreamSubscription("Notifications")]
public class AutoSubscriberGrain : Grain, IGrainWithStringKey
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 자동으로 구독됨
        var streamProvider = this.GetStreamProvider("NotificationStream");
        var stream = streamProvider.GetStream<NotificationEvent>(
            streamNamespace: "Notifications",
            streamId: this.GetPrimaryKeyString());

        var subscriptions = await stream.GetAllSubscriptionHandles();

        if (!subscriptions.Any())
        {
            await stream.SubscribeAsync(OnNotificationAsync);
        }
        else
        {
            await subscriptions.First().ResumeAsync(OnNotificationAsync);
        }

        await base.OnActivateAsync(cancellationToken);
    }

    private Task OnNotificationAsync(
        NotificationEvent notification,
        StreamSequenceToken? token)
    {
        // 알림 처리
        return Task.CompletedTask;
    }
}
```

### 2. Stream Filtering

```csharp
namespace OrleansX.Tutorials.Streams;

public class FilteredConsumerGrain : Grain
{
    public async Task SubscribeToErrorsOnlyAsync()
    {
        var streamProvider = this.GetStreamProvider("NotificationStream");
        var stream = streamProvider.GetStream<NotificationEvent>(
            streamNamespace: "Notifications",
            streamId: "all-notifications");

        await stream.SubscribeAsync(async (notification, token) =>
        {
            // 에러 타입만 처리
            if (notification.Type == NotificationType.Error)
            {
                await HandleErrorNotificationAsync(notification);
            }
        });
    }

    private Task HandleErrorNotificationAsync(NotificationEvent notification)
    {
        // 에러 알림 처리
        return Task.CompletedTask;
    }
}
```

### 3. Stream Batching

```csharp
namespace OrleansX.Tutorials.Streams;

public class BatchConsumerGrain : Grain
{
    private readonly List<NotificationEvent> _batch = new();
    private const int BatchSize = 10;

    public async Task SubscribeWithBatchingAsync()
    {
        var streamProvider = this.GetStreamProvider("NotificationStream");
        var stream = streamProvider.GetStream<NotificationEvent>(
            streamNamespace: "Notifications",
            streamId: this.GetPrimaryKeyString());

        await stream.SubscribeAsync(async (notification, token) =>
        {
            _batch.Add(notification);

            if (_batch.Count >= BatchSize)
            {
                await ProcessBatchAsync(_batch.ToList());
                _batch.Clear();
            }
        });
    }

    private Task ProcessBatchAsync(List<NotificationEvent> batch)
    {
        // 배치 처리
        Console.WriteLine($"Processing batch of {batch.Count} notifications");
        return Task.CompletedTask;
    }
}
```

## 실행 방법

### Silo 구성 (메모리 스트림)

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(options =>
        {
            options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);

            // 메모리 스트림 프로바이더 추가
            options.AddMemoryStreams("NotificationStream");

            options.AddMemoryGrainStorage("consumer");
        });
    });

await builder.Build().RunAsync();
```

### 클라이언트에서 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.Streams;

public class NotificationService
{
    private readonly IGrainInvoker _invoker;

    public NotificationService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task DemoStreamingAsync()
    {
        var streamId = "user-notifications";

        // Consumer 시작
        var consumer = _invoker.GetGrain<INotificationConsumerGrain>(streamId);
        await consumer.StartListeningAsync();
        Console.WriteLine("Consumer started listening...");

        // Producer에서 메시지 발행
        var producer = _invoker.GetGrain<INotificationProducerGrain>(streamId);

        await producer.SendNotificationAsync(
            "user-123",
            "Welcome to OrleansX!",
            NotificationType.Info);

        await producer.SendNotificationAsync(
            "user-123",
            "Your order has been shipped",
            NotificationType.Success);

        await producer.SendNotificationAsync(
            "user-123",
            "Payment failed",
            NotificationType.Error);

        // 잠시 대기 후 수신된 알림 확인
        await Task.Delay(2000);

        var received = await consumer.GetReceivedNotificationsAsync();
        Console.WriteLine($"\nReceived {received.Count} notifications:");
        foreach (var notification in received)
        {
            Console.WriteLine($"  [{notification.Type}] {notification.Message}");
        }

        // Consumer 중지
        await consumer.StopListeningAsync();
    }
}
```

## 실행 예제

```bash
# Silo 실행
cd Tutorials/06-Streams/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/06-Streams/Client
dotnet run
```

## 예상 출력

```
Consumer started listening...

Received 3 notifications:
  [Info] Welcome to OrleansX!
  [Success] Your order has been shipped
  [Error] Payment failed
```

## Stream Provider 비교

| Provider | 용도 | 지속성 | 처리량 |
|----------|------|--------|--------|
| Memory | 개발/테스트 | 없음 | 높음 |
| Azure Event Hub | 프로덕션 | 있음 | 매우 높음 |
| Azure Queue | 간단한 큐 | 있음 | 중간 |
| Kafka | 프로덕션 | 있음 | 매우 높음 |

## CorrelationId 사용 예제

`CorrelationId`는 여러 이벤트를 하나의 비즈니스 작업으로 묶어서 추적할 때 사용합니다.

### 시나리오: 주문 처리 플로우

주문 생성부터 배송까지 여러 이벤트가 발생하는데, 이들을 주문 ID로 연결하여 추적합니다.

```csharp
// 주문 관련 이벤트들
[GenerateSerializer]
public class OrderCreatedEvent : GrainEvent
{
    [Id(0)] public string OrderId { get; set; } = string.Empty;
    [Id(1)] public decimal Amount { get; set; }

    public OrderCreatedEvent(string orderId)
    {
        EventType = nameof(OrderCreatedEvent);
        CorrelationId = orderId; // 주문 ID로 이벤트 연결
    }
}

[GenerateSerializer]
public class PaymentProcessedEvent : GrainEvent
{
    [Id(0)] public string OrderId { get; set; } = string.Empty;
    [Id(1)] public string TransactionId { get; set; } = string.Empty;

    public PaymentProcessedEvent(string orderId)
    {
        EventType = nameof(PaymentProcessedEvent);
        CorrelationId = orderId; // 같은 주문 ID 사용
    }
}

[GenerateSerializer]
public class ShippingStartedEvent : GrainEvent
{
    [Id(0)] public string OrderId { get; set; } = string.Empty;
    [Id(1)] public string TrackingNumber { get; set; } = string.Empty;

    public ShippingStartedEvent(string orderId)
    {
        EventType = nameof(ShippingStartedEvent);
        CorrelationId = orderId; // 같은 주문 ID 사용
    }
}

// 주문 처리 Grain
public class OrderProcessingGrain : StatelessGrainBase, IOrderProcessingGrain
{
    private IAsyncStream<GrainEvent>? _stream;

    public OrderProcessingGrain(ILogger<OrderProcessingGrain> logger) : base(logger) { }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("OrderStream");
        _stream = streamProvider.GetStream<GrainEvent>(
            StreamId.Create("Orders", this.GetPrimaryKeyString()));
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ProcessOrderAsync(string orderId, decimal amount)
    {
        // 1. 주문 생성 이벤트
        var orderCreated = new OrderCreatedEvent(orderId)
        {
            OrderId = orderId,
            Amount = amount
        };
        await _stream!.OnNextAsync(orderCreated);
        Logger.LogInformation("Order created: {OrderId}, CorrelationId: {CorrelationId}",
            orderId, orderCreated.CorrelationId);

        // 2. 결제 처리 이벤트
        var paymentProcessed = new PaymentProcessedEvent(orderId)
        {
            OrderId = orderId,
            TransactionId = Guid.NewGuid().ToString()
        };
        await _stream!.OnNextAsync(paymentProcessed);
        Logger.LogInformation("Payment processed: {OrderId}, CorrelationId: {CorrelationId}",
            orderId, paymentProcessed.CorrelationId);

        // 3. 배송 시작 이벤트
        var shippingStarted = new ShippingStartedEvent(orderId)
        {
            OrderId = orderId,
            TrackingNumber = $"TRK-{Guid.NewGuid().ToString()[..8]}"
        };
        await _stream!.OnNextAsync(shippingStarted);
        Logger.LogInformation("Shipping started: {OrderId}, CorrelationId: {CorrelationId}",
            orderId, shippingStarted.CorrelationId);
    }
}

// 모니터링 Grain - CorrelationId로 관련 이벤트 추적
public class OrderMonitoringGrain : StatefulGrainBase<OrderMonitoringState>, IOrderMonitoringGrain
{
    public OrderMonitoringGrain(
        [PersistentState("monitoring")] IPersistentState<OrderMonitoringState> state,
        ILogger<OrderMonitoringGrain> logger)
        : base(state, logger)
    {
    }

    public async Task StartMonitoringAsync()
    {
        var streamProvider = this.GetStreamProvider("OrderStream");
        var stream = streamProvider.GetStream<GrainEvent>(
            StreamId.Create("Orders", this.GetPrimaryKeyString()));

        await stream.SubscribeAsync(async (evt, token) =>
        {
            // CorrelationId로 같은 주문의 이벤트들을 그룹화
            var orderId = evt.CorrelationId;
            if (orderId != null)
            {
                if (!State.OrderTimeline.ContainsKey(orderId))
                {
                    State.OrderTimeline[orderId] = new List<string>();
                }

                State.OrderTimeline[orderId].Add(
                    $"[{evt.Timestamp:HH:mm:ss}] {evt.EventType} (EventId: {evt.EventId})");

                await SaveStateAsync();

                Logger.LogInformation(
                    "Event tracked for Order {OrderId}: {EventType} - Total events: {Count}",
                    orderId, evt.EventType, State.OrderTimeline[orderId].Count);
            }
        });
    }

    public Task<List<string>> GetOrderTimelineAsync(string orderId)
    {
        return Task.FromResult(
            State.OrderTimeline.TryGetValue(orderId, out var timeline)
                ? timeline
                : new List<string>());
    }
}

[GenerateSerializer]
public class OrderMonitoringState
{
    [Id(0)]
    public Dictionary<string, List<string>> OrderTimeline { get; set; } = new();
}
```

### 로그 출력 예제

```
[14:30:15] Order created: order-123, CorrelationId: order-123
[14:30:15] Payment processed: order-123, CorrelationId: order-123
[14:30:16] Shipping started: order-123, CorrelationId: order-123

// 모니터링 조회 시
GetOrderTimeline("order-123"):
  [14:30:15] OrderCreatedEvent (EventId: abc-def-123)
  [14:30:15] PaymentProcessedEvent (EventId: ghi-jkl-456)
  [14:30:16] ShippingStartedEvent (EventId: mno-pqr-789)
```

### CorrelationId의 주요 사용 사례

1. **주문/결제 플로우**: 주문 생성 → 결제 → 재고 차감 → 배송
2. **사용자 요청 추적**: API 요청 → 여러 Grain 호출 → 응답
3. **게임 매칭**: 매칭 요청 → 파티 생성 → 룸 생성 → 게임 시작
4. **이벤트 소싱**: 같은 Aggregate의 모든 이벤트를 Aggregate ID로 연결

### 실전 팁

```csharp
// ✅ Good: HTTP 요청 ID를 CorrelationId로 사용
app.MapPost("/orders", async (CreateOrderRequest req, HttpContext ctx, IGrainInvoker invoker) =>
{
    var correlationId = ctx.TraceIdentifier; // 또는 커스텀 헤더에서 가져오기

    var orderGrain = invoker.GetGrain<IOrderProcessingGrain>("processor");
    await orderGrain.ProcessOrderAsync(req.OrderId, req.Amount);

    // 모든 이벤트가 같은 CorrelationId를 가지므로 추적 가능
    return Results.Ok(new { CorrelationId = correlationId });
});

// ✅ Good: 분산 추적 도구와 연동 (OpenTelemetry 등)
var activity = Activity.Current;
var correlationId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString();
event.CorrelationId = correlationId;
```

## Best Practices

### 1. Stream Namespace 활용
```csharp
// ✅ 논리적 그룹화
var userStream = provider.GetStream<Event>("Users", userId);
var orderStream = provider.GetStream<Event>("Orders", orderId);

// ❌ 모든 스트림을 하나의 namespace에
var stream = provider.GetStream<Event>("Default", id);
```

### 2. 구독 해제
```csharp
// ✅ OnDeactivate에서 구독 해제
public override async Task OnDeactivateAsync(...)
{
    if (_handle != null)
        await _handle.UnsubscribeAsync();
}

// ❌ 구독 해제 안 함 → 메모리 누수
```

### 3. 에러 처리
```csharp
// ✅ 에러 처리
await stream.SubscribeAsync(async (item, token) =>
{
    try
    {
        await ProcessAsync(item);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Stream processing failed");
        // 재시도 또는 Dead Letter Queue로 전송
    }
});
```

## 다음 단계

축하합니다! 모든 OrleansX 튜토리얼을 완료하셨습니다.

### 추가 학습 자료
- Orleans 공식 문서: https://learn.microsoft.com/orleans
- OrleansX GitHub: https://github.com/your-repo/orleansx
- 실전 예제 프로젝트 참고

### 프로덕션 체크리스트
- [ ] 적절한 Clustering 전략 선택 (AdoNet, Azure, Consul)
- [ ] Persistence 설정 (SQL Server, Azure Storage)
- [ ] Stream Provider 설정 (Event Hub, Kafka)
- [ ] 모니터링 및 로깅 설정
- [ ] 성능 테스트 및 튜닝
