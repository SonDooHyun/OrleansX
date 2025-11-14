# OrleansX Framework

Orleans 기반의 프로덕션 레디 분산 시스템 프레임워크입니다. 재사용 가능한 Grain 베이스 클래스와 고급 기능을 제공하여 게임, 실시간 애플리케이션, IoT 등 다양한 도메인에서 빠르게 개발할 수 있습니다.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/download)
[![Orleans](https://img.shields.io/badge/Orleans-9.2.1-purple)](https://github.com/dotnet/orleans)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## 📋 목차

- [소개](#-소개)
- [📚 상세 문서](#-상세-문서)
- [주요 기능](#-주요-기능)
- [프로젝트 구조](#-프로젝트-구조)
- [빠른 시작](#-빠른-시작)
- [상세 가이드](#-상세-가이드)
- [튜토리얼](#-튜토리얼)
- [기술 스택](#-기술-스택)
- [베스트 프랙티스](#-베스트-프랙티스)

---

## 🎯 소개

### Orleans란?

Orleans는 Microsoft에서 개발한 **Virtual Actor Model** 기반의 분산 시스템 프레임워크입니다.

#### Virtual Actor Model의 핵심 개념

1. **Grain (가상 액터)**
   - 상태와 행동을 캡슐화한 분산 객체
   - 고유한 ID로 식별
   - 단일 스레드 실행으로 동시성 문제 자동 해결
   - 필요할 때 자동으로 활성화/비활성화

2. **위치 투명성**
   - Grain이 어느 서버에 있든 동일한 방식으로 호출
   - 프레임워크가 자동으로 라우팅 처리

3. **수평 확장**
   - 서버 추가만으로 자동 부하 분산
   - 손쉬운 스케일 아웃

4. **내결함성**
   - Grain 상태를 영구 저장소에 저장
   - 서버 장애 시 다른 서버에서 자동 복구

### OrleansX의 차별점

OrleansX는 Orleans를 더 쉽게 사용할 수 있도록 다음과 같은 기능을 제공합니다:

- ✅ **즉시 사용 가능한 Grain 베이스 클래스** - 보일러플레이트 코드 제거
- ✅ **분산 트랜잭션(ACID) 지원** - 금융, 재고 등 중요한 데이터 처리
- ✅ **워커 Grain 패턴** - 백그라운드 작업 자동화
- ✅ **재시도 및 멱등성 내장** - 신뢰성 있는 클라이언트 SDK
- ✅ **표준화된 설정 패턴** - Fluent API로 간편한 구성
- ✅ **통합 테스트 키트** - xUnit 기반 테스트 지원

---

## 📚 상세 문서

OrleansX의 구현 세부사항과 고급 사용법에 대한 심층 가이드는 **[docs](/docs)** 디렉토리에서 확인할 수 있습니다.

### 📖 작성된 문서 목록

#### Client SDK ✅
- [GrainInvoker](/docs/Client/GrainInvoker.md) - Grain 호출 래퍼 및 Facade 패턴 (600+ 줄)
- [재시도 정책 (Retry Policy)](/docs/Client/RetryPolicy.md) - 자동 재시도 메커니즘 (지수 백오프) (550+ 줄)
- [멱등성 (Idempotency)](/docs/Client/Idempotency.md) - 중복 요청 방지 패턴 (500+ 줄)

#### 고급 주제 ✅
- [C# Record 타입](/docs/Advanced/CSharpRecords.md) - 위치 기반 레코드 문법 설명 (550+ 줄)
- [의존성 주입 (DI)](/docs/Advanced/DependencyInjection.md) - AddSingleton 패턴 이해 (500+ 줄)
- [AsyncLocal](/docs/Advanced/AsyncLocal.md) - 비동기 컨텍스트 데이터 전달 (450+ 줄)
- [WorkerExecutor 내부 구조](/docs/Advanced/WorkerExecutor.md) - WorkerExecutor 심층 분석 (550+ 줄)

> 💡 **Tip**: 각 문서는 500~600줄 분량의 상세한 설명, 실전 예제, 디자인 패턴 분석을 포함하고 있습니다.
>
> 📖 **튜토리얼**: 실전 사용법은 [examples/Tutorials](/examples/Tutorials/README.md)를 참고하세요.

---

## ✨ 주요 기능

### 1. 다양한 Grain 베이스 클래스

| 클래스 | 설명 | 사용 시나리오 |
|--------|------|--------------|
| **StatelessGrainBase** | 상태가 없는 Grain | 유틸리티, 계산 로직, API 프록시 |
| **StatefulGrainBase&lt;TState&gt;** | 영속 상태를 가진 Grain | 사용자 정보, 게임 상태 관리 |
| **TransactionalGrainBase&lt;TState&gt;** | 트랜잭션 상태를 가진 Grain | 금융 거래, 재고 관리, 결제 |
| **StatelessWorkerGrainBase** | 상태 없는 워커 Grain | 헬스체크, 경량 백그라운드 작업 |
| **StatefulWorkerGrainBase&lt;TState&gt;** | 상태를 가진 워커 Grain | 데이터 정리, 통계 집계, 스케줄링 |

#### StatefulGrainBase - 영속 상태 관리

```csharp
using OrleansX.Grains;
using Orleans.Runtime;

[GenerateSerializer]
public class PlayerState
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public int Level { get; set; }
    [Id(2)] public int Gold { get; set; }
}

public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task AddGoldAsync(int amount)
    {
        await UpdateStateAsync(state =>
        {
            state.Gold += amount;
        });
    }
}
```

**제공 메서드:**
- `State` - 현재 상태 접근
- `UpdateStateAsync(Action<TState>)` - 상태 업데이트 및 저장
- `SaveStateAsync()` - 상태 저장
- `ReadStateAsync()` - 상태 새로고침
- `ClearStateAsync()` - 상태 삭제

#### TransactionalGrainBase - ACID 트랜잭션

```csharp
using OrleansX.Grains;
using Orleans.Transactions.Abstractions;

[GenerateSerializer]
public class AccountState
{
    [Id(0)] public string AccountNumber { get; set; } = string.Empty;
    [Id(1)] public decimal Balance { get; set; }
}

public class AccountGrain : TransactionalGrainBase<AccountState>, IAccountGrain
{
    public AccountGrain(
        [TransactionalState("account")] ITransactionalState<AccountState> state,
        ILogger<AccountGrain> logger)
        : base(state, logger)
    {
    }

    [Transaction(TransactionOption.Join)]
    public async Task WithdrawAsync(decimal amount)
    {
        await UpdateStateAsync(state =>
        {
            if (state.Balance < amount)
                throw new InvalidOperationException("잔액 부족");
            state.Balance -= amount;
        });
    }

    [Transaction(TransactionOption.Join)]
    public async Task DepositAsync(decimal amount)
    {
        await UpdateStateAsync(state => state.Balance += amount);
    }
}

// 여러 Grain 간 트랜잭션
public class TransferGrain : Grain, ITransferGrain
{
    [Transaction(TransactionOption.Create)]
    public async Task<bool> TransferAsync(string fromAccount, string toAccount, decimal amount)
    {
        var from = GrainFactory.GetGrain<IAccountGrain>(fromAccount);
        var to = GrainFactory.GetGrain<IAccountGrain>(toAccount);

        // 원자적으로 처리 (All-or-Nothing)
        await from.WithdrawAsync(amount);
        await to.DepositAsync(amount);

        return true;
    }
}
```

**제공 메서드:**
- `GetStateAsync()` - 트랜잭션 내에서 읽기 전용 상태 조회
- `UpdateStateAsync(Action<TState>)` - 트랜잭션 내에서 상태 업데이트
- `UpdateStateAsync<TResult>(Func<TState, TResult>)` - 상태 업데이트 후 결과 반환
- `ReadStateAsync<TResult>(Func<TState, TResult>)` - 읽기 전용 조회 후 결과 반환

**트랜잭션 옵션:**
- `TransactionOption.Create` - 새 트랜잭션 생성
- `TransactionOption.Join` - 기존 트랜잭션에 참여
- `TransactionOption.CreateOrJoin` - 있으면 참여, 없으면 생성
- `TransactionOption.Suppress` - 트랜잭션 없이 실행
- `TransactionOption.NotAllowed` - 트랜잭션 컨텍스트 불허

#### WorkerGrainBase - 백그라운드 작업

```csharp
using OrleansX.Grains;
using Orleans.Runtime;

[GenerateSerializer]
public class CleanupWorkerState
{
    [Id(0)] public DateTime LastCleanupTime { get; set; }
    [Id(1)] public int TotalCleaned { get; set; }
}

public class CleanupWorkerGrain : StatefulWorkerGrainBase<CleanupWorkerState>, IWorkerGrain
{
    public CleanupWorkerGrain(
        [PersistentState("cleanup")] IPersistentState<CleanupWorkerState> state,
        ILogger<CleanupWorkerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task StartAsync()
    {
        // 5분마다 실행 (첫 실행은 10초 후)
        await StartTimerAsync(
            dueTime: TimeSpan.FromSeconds(10),
            period: TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("정리 작업 시작...");

        var deletedCount = await CleanupOldDataAsync();

        await UpdateStateAsync(state =>
        {
            state.LastCleanupTime = DateTime.UtcNow;
            state.TotalCleaned += deletedCount;
        });
    }

    protected override async Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "정리 작업 실패");
        await base.OnErrorAsync(exception);
    }

    private Task<int> CleanupOldDataAsync()
    {
        // 실제 정리 로직
        return Task.FromResult(42);
    }
}
```

**제공 메서드:**
- `StartTimerAsync(TimeSpan dueTime, TimeSpan period)` - Timer 시작
- `StopAsync()` - 워커 중지
- `GetStatusAsync()` - 실행 상태 조회
- `ResetStatisticsAsync()` - 통계 초기화

**훅 메서드:**
- `ExecuteWorkAsync()` - 실제 작업 구현 (필수)
- `OnBeforeExecuteAsync()` - 작업 전처리
- `OnAfterExecuteAsync()` - 작업 후처리
- `OnErrorAsync(Exception)` - 에러 처리

**워커 상태 정보:**
- `IsRunning` - 실행 중 여부
- `LastExecutionTime` - 마지막 실행 시간
- `NextExecutionTime` - 다음 실행 예정 시간
- `SuccessCount` - 성공 횟수
- `FailureCount` - 실패 횟수
- `SuccessRate` - 성공률 (%)

### 2. 고급 클라이언트 SDK

```csharp
using OrleansX.Client.Extensions;
using OrleansX.Abstractions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrleansXClient(new OrleansClientOptions
{
    ClusterId = "my-cluster",
    ServiceId = "my-service",
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        BaseDelayMs = 200,
        MaxDelayMs = 10000
    }
});

var app = builder.Build();

app.MapGet("/player/{id}/gold", async (string id, IGrainInvoker invoker) =>
{
    var player = invoker.GetGrain<IPlayerGrain>(id);
    return await player.GetGoldAsync();
});

app.Run();
```

**클라이언트 기능:**
- `IGrainInvoker` - Grain 호출 래퍼
- `IRetryPolicy` - 재시도 정책
- `IIdempotencyKeyProvider` - 멱등성 키 관리
- 자동 재시도 및 Circuit Breaker
- 연결 관리 및 자동 재연결

### 3. 표준화된 Silo 설정

```csharp
using Orleans.Hosting;
using OrleansX.Silo.Hosting.Extensions;
using OrleansX.Abstractions.Options;

var builder = Host.CreateDefaultBuilder(args);

builder.UseOrleans((context, siloBuilder) =>
{
    siloBuilder.UseOrleansXDefaults(opts =>
    {
        opts.WithCluster("game-cluster", "game-service")
            .WithPorts(siloPort: 11111, gatewayPort: 30000)
            .WithClustering(ClusteringOptions.Localhost())
            .WithPersistence(PersistenceOptions.Memory())
            .WithStreams(StreamsOptions.Memory("Default"))
            .WithTransactions(TransactionOptions.Memory());
    });
});

var host = builder.Build();
await host.RunAsync();
```

**Clustering 옵션:**
```csharp
ClusteringOptions.Localhost()
ClusteringOptions.AdoNet("System.Data.SqlClient", connectionString)
ClusteringOptions.Redis(connectionString)
```

**Persistence 옵션:**
```csharp
PersistenceOptions.Memory()
PersistenceOptions.AdoNet("System.Data.SqlClient", connectionString)
PersistenceOptions.Redis(connectionString)
```

**Streams 옵션:**
```csharp
StreamsOptions.Memory("StreamProvider")
StreamsOptions.Kafka(bootstrapServers, "StreamProvider")
StreamsOptions.EventHubs(connectionString, "StreamProvider")
```

**Transaction 옵션:**
```csharp
TransactionOptions.Memory()
TransactionOptions.AzureStorage(connectionString)
TransactionOptions.AdoNet("System.Data.SqlClient", connectionString)
```

### 4. 이벤트 베이스 클래스

```csharp
using OrleansX.Abstractions.Events;
using Orleans;

// GrainEvent를 상속받아 표준화된 이벤트 생성
[GenerateSerializer]
public class OrderCreatedEvent : GrainEvent
{
    [Id(0)] public string OrderId { get; set; } = string.Empty;
    [Id(1)] public decimal Amount { get; set; }

    public OrderCreatedEvent()
    {
        EventType = nameof(OrderCreatedEvent);
    }
}

// 사용 예
var evt = new OrderCreatedEvent
{
    OrderId = "order-123",
    Amount = 1000m,
    CorrelationId = "request-456"  // 여러 이벤트를 하나의 작업으로 묶어서 추적
};
// EventId와 Timestamp는 자동 생성/설정됨
```

**GrainEvent 제공 필드:**
- `EventId` - 이벤트 고유 식별자 (자동 생성)
- `Timestamp` - 이벤트 발생 시간 (자동 설정)
- `EventType` - 이벤트 타입 (명시적 설정 권장)
- `CorrelationId` - 분산 추적용 상관관계 ID (선택)

**CorrelationId 사용 사례:**
- **전자상거래**: 주문 생성 → 결제 → 배송 이벤트를 주문 ID로 연결
- **API 추적**: HTTP 요청 → 여러 Grain 호출 → 응답을 요청 ID로 추적
- **게임 매칭**: 매칭 요청 → 파티 생성 → 룸 생성 → 게임 시작을 매칭 ID로 연결
- **게임 전투**: 전투 시작 → 스킬 사용 → 데미지 → 전투 종료를 전투 ID로 연결
- **퀘스트**: 퀘스트 수락 → 진행 → 완료 → 보상 지급을 퀘스트 ID로 연결

### 5. 통합 테스트 키트

```csharp
using OrleansX.TestKit;
using Xunit;

[Collection("OrleansXCluster")]
public class PlayerGrainTests
{
    private readonly OrleansXTestClusterFixture _fixture;

    public PlayerGrainTests(OrleansXTestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_Add_Gold()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>("player1");

        // Act
        await grain.AddGoldAsync(100);
        var gold = await grain.GetGoldAsync();

        // Assert
        Assert.Equal(100, gold);
    }
}
```

---

## 📁 프로젝트 구조

```
OrleansX/
├── src/                                    # 라이브러리 소스 코드
│   ├── OrleansX.Abstractions/             # 공용 인터페이스 및 옵션
│   │   ├── Events/
│   │   │   └── GrainEvent.cs             # 이벤트 베이스 클래스
│   │   ├── Options/
│   │   │   ├── OrleansClientOptions.cs
│   │   │   └── OrleansXSiloOptions.cs
│   │   ├── IGrainInvoker.cs
│   │   ├── IRetryPolicy.cs
│   │   ├── IIdempotencyKeyProvider.cs
│   │   └── IWorkerGrain.cs
│   │
│   ├── OrleansX.Grains/                   # Grain 베이스 클래스
│   │   ├── StatelessGrainBase.cs
│   │   ├── StatefulGrainBase.cs
│   │   ├── TransactionalGrainBase.cs
│   │   ├── StatelessWorkerGrainBase.cs
│   │   ├── StatefulWorkerGrainBase.cs
│   │   ├── Internal/
│   │   │   └── WorkerExecutor.cs          # Worker 공통 로직
│   │   └── Utilities/
│   │       └── StreamHelper.cs
│   │
│   ├── OrleansX.Client/                   # 클라이언트 SDK
│   │   ├── GrainInvoker.cs
│   │   ├── Retry/
│   │   │   └── ExponentialRetryPolicy.cs
│   │   ├── Idempotency/
│   │   │   └── AsyncLocalIdempotencyKeyProvider.cs
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── OrleansX.Silo.Hosting/             # Silo 호스팅 확장
│   │   └── Extensions/
│   │       ├── SiloBuilderExtensions.cs
│   │       └── SimplifiedHostingExtensions.cs
│   │
│   └── OrleansX.TestKit/                  # 테스트 유틸리티
│       └── OrleansXTestClusterFixture.cs
│
├── examples/                               # 튜토리얼 예제
│   ├── Tutorials/
│   │   ├── 01-StatelessGrain/             # Stateless Grain 튜토리얼
│   │   ├── 02-StatefulGrain/              # Stateful Grain 튜토리얼
│   │   ├── 03-TransactionalGrain/         # Transactional Grain 튜토리얼
│   │   ├── 04-WorkerGrains/               # Worker Grains 튜토리얼
│   │   ├── 05-ClientSDK/                  # Client SDK 튜토리얼
│   │   ├── 06-Streams/                    # Streams 튜토리얼
│   │   └── README.md                      # 튜토리얼 인덱스
│   └── OrleansX.Examples.sln
│
├── OrleansX.sln
└── README.md
```

---

## 🚀 빠른 시작

### 사전 요구사항

- .NET 9.0 SDK 이상
- (선택) 데이터베이스 - SQL Server, PostgreSQL, MySQL
- (선택) Redis - 캐싱/클러스터링
- (선택) Kafka - 이벤트 스트리밍

### 설치

```bash
# 저장소 클론
git clone https://github.com/your-org/OrleansX.git
cd OrleansX

# 전체 빌드
dotnet build
```

### 3분 안에 시작하기

#### 1. Grain 인터페이스 및 구현 작성

```csharp
// IHelloGrain.cs
public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHelloAsync(string name);
}

// HelloGrain.cs
using OrleansX.Grains;

public class HelloGrain : StatelessGrainBase, IHelloGrain
{
    public HelloGrain(ILogger<HelloGrain> logger) : base(logger) { }

    public Task<string> SayHelloAsync(string name)
    {
        return Task.FromResult($"Hello, {name}!");
    }
}
```

#### 2. Silo 호스트 생성

```csharp
// Program.cs (Silo)
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using OrleansX.Silo.Hosting.Extensions;
using OrleansX.Abstractions.Options;

var builder = Host.CreateDefaultBuilder(args);

builder.UseOrleans((context, siloBuilder) =>
{
    siloBuilder.UseOrleansXDefaults(opts =>
    {
        opts.WithCluster("dev-cluster", "hello-service")
            .WithPorts(11111, 30000)
            .WithClustering(ClusteringOptions.Localhost())
            .WithPersistence(PersistenceOptions.Memory());
    });
});

await builder.Build().RunAsync();
```

#### 3. 클라이언트 애플리케이션 작성

```csharp
// Program.cs (Client)
using OrleansX.Client.Extensions;
using OrleansX.Abstractions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrleansXClient(new OrleansClientOptions
{
    ClusterId = "dev-cluster",
    ServiceId = "hello-service"
});

var app = builder.Build();

app.MapGet("/hello/{name}", async (string name, IGrainInvoker invoker) =>
{
    var grain = invoker.GetGrain<IHelloGrain>(name);
    return await grain.SayHelloAsync(name);
});

await app.RunAsync();
```

#### 4. 실행

```bash
# Terminal 1: Silo 실행
dotnet run --project SiloHost

# Terminal 2: Client 실행
dotnet run --project Client

# Terminal 3: 테스트
curl http://localhost:5000/hello/World
# 출력: Hello, World!
```

---

## 📚 상세 가이드

### Grain 베이스 클래스 선택 가이드

| 상황 | 추천 베이스 클래스 |
|------|-------------------|
| 상태 없는 유틸리티, 계산 로직 | `StatelessGrainBase` |
| 사용자 정보, 게임 상태 등 일반적인 상태 관리 | `StatefulGrainBase<TState>` |
| 금융 거래, 재고 관리 등 ACID가 필요한 경우 | `TransactionalGrainBase<TState>` |
| 주기적인 경량 백그라운드 작업 | `StatelessWorkerGrainBase` |
| 주기적인 백그라운드 작업 + 상태 저장 | `StatefulWorkerGrainBase<TState>` |

### Timer vs Reminder

Worker Grain은 기본적으로 Timer를 사용합니다. 영속적인 스케줄링이 필요하면 IRemindable을 구현하세요.

| 특징 | Timer | Reminder |
|------|-------|----------|
| **동작 범위** | Grain 활성화 중에만 | Grain 비활성화 후에도 지속 |
| **영속성** | 없음 (메모리만) | 있음 (저장소에 저장) |
| **정확도** | 높음 | 비교적 낮음 (분 단위) |
| **사용 사례** | 짧은 주기, 실시간 모니터링 | 일일 배치, 주기적 정리 |
| **구현 복잡도** | 낮음 | 높음 (IRemindable 구현) |

#### Reminder 예제

```csharp
using Orleans.Runtime;
using OrleansX.Grains;

public class DailyReportWorkerGrain : StatefulWorkerGrainBase<ReportState>, IWorkerGrain, IRemindable
{
    private const string ReminderName = "DailyReport";

    public DailyReportWorkerGrain(
        [PersistentState("report")] IPersistentState<ReportState> state,
        ILogger<DailyReportWorkerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task StartAsync()
    {
        // Grain이 비활성화되어도 계속 실행됨
        await this.RegisterOrUpdateReminder(
            ReminderName,
            dueTime: TimeSpan.FromHours(1),
            period: TimeSpan.FromHours(24));
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ReminderName)
        {
            await ExecuteWorkAsync();
        }
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("일일 리포트 생성 중...");
        // 리포트 생성 로직
    }

    public override async Task StopAsync()
    {
        var reminder = await this.GetReminder(ReminderName);
        if (reminder != null)
        {
            await this.UnregisterReminder(reminder);
        }
        await base.StopAsync();
    }
}
```

### 프로덕션 환경 설정

#### SQL Server 사용 예제

```csharp
siloBuilder.UseOrleansXDefaults(opts =>
{
    opts.WithCluster("prod-cluster", "game-service")
        .WithPorts(11111, 30000)
        .WithClustering(ClusteringOptions.AdoNet(
            "System.Data.SqlClient",
            "Server=localhost;Database=Orleans;User Id=sa;Password=YourPassword"))
        .WithPersistence(PersistenceOptions.AdoNet(
            "System.Data.SqlClient",
            "Server=localhost;Database=Orleans;User Id=sa;Password=YourPassword"))
        .WithTransactions(TransactionOptions.AdoNet(
            "System.Data.SqlClient",
            "Server=localhost;Database=Orleans;User Id=sa;Password=YourPassword"));
});
```

#### Redis 사용 예제

```csharp
siloBuilder.UseOrleansXDefaults(opts =>
{
    opts.WithCluster("prod-cluster", "game-service")
        .WithPorts(11111, 30000)
        .WithClustering(ClusteringOptions.Redis("localhost:6379"))
        .WithPersistence(PersistenceOptions.Redis("localhost:6379"));
});
```

---

## 🎓 튜토리얼

OrleansX는 6개의 종합 튜토리얼을 제공합니다. 각 튜토리얼은 완전한 예제 코드와 상세한 설명을 포함합니다.

### 튜토리얼 목록

| 번호 | 주제 | 난이도 | 설명 |
|------|------|--------|------|
| [01](examples/Tutorials/01-StatelessGrain/README.md) | Stateless Grain | ⭐ 초급 | 상태 없는 Grain 기본 개념 |
| [02](examples/Tutorials/02-StatefulGrain/README.md) | Stateful Grain | ⭐⭐ 초급-중급 | 영속 상태 관리 |
| [03](examples/Tutorials/03-TransactionalGrain/README.md) | Transactional Grain | ⭐⭐⭐ 중급 | ACID 트랜잭션 처리 |
| [04](examples/Tutorials/04-WorkerGrains/README.md) | Worker Grains | ⭐⭐ 초급-중급 | 백그라운드 작업 자동화 |
| [05](examples/Tutorials/05-ClientSDK/README.md) | Client SDK | ⭐⭐⭐ 중급 | 고급 클라이언트 기능 |
| [06](examples/Tutorials/06-Streams/README.md) | Streams | ⭐⭐⭐⭐ 중급-고급 | 실시간 데이터 스트림 처리 |

### 학습 경로

#### 초보자 (Orleans를 처음 접하는 경우)
1. **Stateless Grain** - Orleans 기본 개념 이해
2. **Stateful Grain** - 상태 관리 학습
3. **Client SDK** - 클라이언트 사용법

#### 중급자 (Orleans 기본을 이해한 경우)
1. **Worker Grains** - 백그라운드 작업 패턴
2. **Transactional Grain** - 트랜잭션 처리
3. **Streams** - 실시간 이벤트 처리

자세한 내용은 [examples/Tutorials/README.md](examples/Tutorials/README.md)를 참조하세요.

---

## 🔧 기술 스택

| 분류 | 기술 | 버전 |
|------|------|------|
| **프레임워크** | .NET | 9.0 |
| **Orleans** | Microsoft Orleans | 9.2.1 |
| **트랜잭션** | Microsoft.Orleans.Transactions | 9.2.1 |
| **스토리지** | ADO.NET, Redis, Memory | - |
| **스트림** | Memory, Kafka, Azure Event Hubs | - |
| **테스트** | xUnit, Orleans.TestingHost | - |
| **로깅** | Microsoft.Extensions.Logging | - |

---

## 💡 베스트 프랙티스

### Grain 설계 원칙

✅ **DO**
- 단일 책임 원칙 준수
- 불변 메시지 사용 (`record` 타입 권장)
- 모든 Grain 메서드는 `async Task` 반환
- 짧은 실행 시간 유지 (긴 작업은 분리)

❌ **DON'T**
- Grain 내에서 블로킹 호출 (Thread.Sleep 등)
- 공유 가변 상태 사용
- 무한 루프나 긴 연산

### 트랜잭션 사용 지침

✅ **트랜잭션을 사용해야 하는 경우**
- 금융 거래 (결제, 송금)
- 재고 관리 (증감, 예약)
- 포인트/화폐 관리
- 여러 Grain 간 일관성이 중요한 경우

❌ **트랜잭션을 피해야 하는 경우**
- 읽기 전용 조회
- 로깅, 통계
- 긴 실행 시간이 필요한 작업
- 외부 API 호출이 포함된 경우

**트랜잭션 최적화:**
```csharp
// ❌ Bad: 읽기 전용인데 트랜잭션 사용
[Transaction(TransactionOption.Join)]
public async Task<decimal> GetBalanceAsync()
{
    var state = await GetStateAsync();
    return state.Balance;
}

// ✅ Good: 읽기 전용은 일반 메서드
public async Task<decimal> GetBalanceAsync()
{
    return await ReadStateAsync(state => state.Balance);
}
```

### Worker Grain 사용 지침

✅ **Worker Grain을 사용해야 하는 경우**
- 주기적인 데이터 정리
- 통계 집계
- 헬스체크
- 캐시 갱신

❌ **Worker Grain을 피해야 하는 경우**
- 실시간 이벤트 처리 (Streams 사용)
- 사용자 요청 처리 (일반 Grain 사용)
- 매우 무거운 작업 (별도 서비스 분리)

**Worker 최적화:**
```csharp
// ✅ Good: 작업 시간이 주기보다 짧게
protected override async Task ExecuteWorkAsync()
{
    // 5분 주기라면 작업은 1-2분 내로
    await DoQuickCleanupAsync();
}

// ❌ Bad: 작업 시간이 주기보다 길면 겹칠 수 있음
protected override async Task ExecuteWorkAsync()
{
    // 5분 주기인데 10분 걸리는 작업
    await DoVerySlowCleanupAsync(); // 나쁜 예!
}
```

### 성능 최적화

1. **Grain 호출 최소화**
   ```csharp
   // ❌ Bad: 여러 번 호출
   var player = grainFactory.GetGrain<IPlayerGrain>(id);
   var name = await player.GetNameAsync();
   var level = await player.GetLevelAsync();
   var gold = await player.GetGoldAsync();

   // ✅ Good: 한 번에 조회
   var player = grainFactory.GetGrain<IPlayerGrain>(id);
   var info = await player.GetInfoAsync(); // 모든 정보 반환
   ```

2. **Stateless Worker 활용**
   ```csharp
   // 병렬 처리가 필요한 경우
   [StatelessWorker(maxLocalWorkers: 10)]
   public class ParallelWorkerGrain : StatelessWorkerGrainBase
   {
       // 10개 인스턴스가 동시에 작업 수행
   }
   ```

3. **적절한 스토리지 선택**
   - **개발/테스트**: Memory
   - **프로덕션 (빠른 읽기/쓰기)**: Redis
   - **프로덕션 (영속성 중요)**: ADO.NET (SQL Server, PostgreSQL)

---

## 🎮 게임 서버에서 GrainEvent 활용

게임 서버에서 `GrainEvent`는 다양한 게임 로직을 추적하고 분석하는 데 매우 유용합니다.

### 1. 전투 시스템 이벤트

```csharp
using OrleansX.Abstractions.Events;

// 전투 관련 이벤트들
[GenerateSerializer]
public class BattleStartedEvent : GrainEvent
{
    [Id(0)] public string BattleId { get; set; } = string.Empty;
    [Id(1)] public List<string> PlayerIds { get; set; } = new();

    public BattleStartedEvent(string battleId)
    {
        EventType = nameof(BattleStartedEvent);
        CorrelationId = battleId; // 전투 ID로 모든 전투 이벤트 연결
    }
}

[GenerateSerializer]
public class SkillUsedEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public string SkillId { get; set; } = string.Empty;
    [Id(2)] public string TargetId { get; set; } = string.Empty;

    public SkillUsedEvent(string battleId)
    {
        EventType = nameof(SkillUsedEvent);
        CorrelationId = battleId; // 같은 전투 ID
    }
}

[GenerateSerializer]
public class DamageDealtEvent : GrainEvent
{
    [Id(0)] public string AttackerId { get; set; } = string.Empty;
    [Id(1)] public string VictimId { get; set; } = string.Empty;
    [Id(2)] public int Damage { get; set; }
    [Id(3)] public bool IsCritical { get; set; }

    public DamageDealtEvent(string battleId)
    {
        EventType = nameof(DamageDealtEvent);
        CorrelationId = battleId;
    }
}

[GenerateSerializer]
public class BattleEndedEvent : GrainEvent
{
    [Id(0)] public string WinnerId { get; set; } = string.Empty;
    [Id(1)] public int Duration { get; set; } // 전투 시간 (초)

    public BattleEndedEvent(string battleId)
    {
        EventType = nameof(BattleEndedEvent);
        CorrelationId = battleId;
    }
}

// 전투 리플레이 시스템 - CorrelationId로 전투 전체 기록 조회
public class BattleReplayGrain : StatefulGrainBase<BattleReplayState>
{
    public BattleReplayGrain(
        [PersistentState("replay")] IPersistentState<BattleReplayState> state,
        ILogger<BattleReplayGrain> logger)
        : base(state, logger)
    {
    }

    public async Task RecordEventAsync(GrainEvent evt)
    {
        if (evt.CorrelationId != null)
        {
            await UpdateStateAsync(state =>
            {
                if (!state.BattleEvents.ContainsKey(evt.CorrelationId))
                {
                    state.BattleEvents[evt.CorrelationId] = new List<GrainEvent>();
                }
                state.BattleEvents[evt.CorrelationId].Add(evt);
            });
        }
    }

    // 전투 리플레이 조회 (CorrelationId로 모든 이벤트 추출)
    public Task<List<GrainEvent>> GetBattleReplayAsync(string battleId)
    {
        return Task.FromResult(
            State.BattleEvents.TryGetValue(battleId, out var events)
                ? events.OrderBy(e => e.Timestamp).ToList()
                : new List<GrainEvent>());
    }
}
```

### 2. 퀘스트 시스템 이벤트

```csharp
[GenerateSerializer]
public class QuestAcceptedEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public string QuestId { get; set; } = string.Empty;

    public QuestAcceptedEvent(string questInstanceId)
    {
        EventType = nameof(QuestAcceptedEvent);
        CorrelationId = questInstanceId; // 퀘스트 인스턴스 ID
    }
}

[GenerateSerializer]
public class QuestProgressedEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public string ObjectiveId { get; set; } = string.Empty;
    [Id(2)] public int CurrentProgress { get; set; }
    [Id(3)] public int RequiredProgress { get; set; }

    public QuestProgressedEvent(string questInstanceId)
    {
        EventType = nameof(QuestProgressedEvent);
        CorrelationId = questInstanceId;
    }
}

[GenerateSerializer]
public class QuestCompletedEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public List<RewardInfo> Rewards { get; set; } = new();
    [Id(2)] public int CompletionTimeSeconds { get; set; }

    public QuestCompletedEvent(string questInstanceId)
    {
        EventType = nameof(QuestCompletedEvent);
        CorrelationId = questInstanceId;
    }
}
```

### 3. 거래/경제 시스템 이벤트

```csharp
[GenerateSerializer]
public class TradeInitiatedEvent : GrainEvent
{
    [Id(0)] public string InitiatorId { get; set; } = string.Empty;
    [Id(1)] public string TargetId { get; set; } = string.Empty;

    public TradeInitiatedEvent(string tradeId)
    {
        EventType = nameof(TradeInitiatedEvent);
        CorrelationId = tradeId;
    }
}

[GenerateSerializer]
public class ItemAddedToTradeEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public string ItemId { get; set; } = string.Empty;
    [Id(2)] public int Quantity { get; set; }

    public ItemAddedToTradeEvent(string tradeId)
    {
        EventType = nameof(ItemAddedToTradeEvent);
        CorrelationId = tradeId;
    }
}

[GenerateSerializer]
public class TradeCompletedEvent : GrainEvent
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? FailureReason { get; set; }

    public TradeCompletedEvent(string tradeId)
    {
        EventType = nameof(TradeCompletedEvent);
        CorrelationId = tradeId;
    }
}
```

### 4. 매칭/파티 시스템 이벤트

```csharp
[GenerateSerializer]
public class MatchmakingStartedEvent : GrainEvent
{
    [Id(0)] public string PlayerId { get; set; } = string.Empty;
    [Id(1)] public string GameMode { get; set; } = string.Empty;
    [Id(2)] public int MMR { get; set; }

    public MatchmakingStartedEvent(string matchRequestId)
    {
        EventType = nameof(MatchmakingStartedEvent);
        CorrelationId = matchRequestId;
    }
}

[GenerateSerializer]
public class PartyCreatedEvent : GrainEvent
{
    [Id(0)] public string PartyId { get; set; } = string.Empty;
    [Id(1)] public List<string> PlayerIds { get; set; } = new();

    public PartyCreatedEvent(string matchRequestId)
    {
        EventType = nameof(PartyCreatedEvent);
        CorrelationId = matchRequestId; // 매칭 요청 ID로 연결
    }
}

[GenerateSerializer]
public class MatchFoundEvent : GrainEvent
{
    [Id(0)] public string RoomId { get; set; } = string.Empty;
    [Id(1)] public int WaitTimeSeconds { get; set; }

    public MatchFoundEvent(string matchRequestId)
    {
        EventType = nameof(MatchFoundEvent);
        CorrelationId = matchRequestId;
    }
}
```

### 5. 게임 서버에서 활용 이점

#### 리플레이 시스템
```csharp
// 전투 리플레이 재생
var replay = await replayGrain.GetBattleReplayAsync("battle-123");
foreach (var evt in replay)
{
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss.fff}] {evt.EventType}");
    // 각 이벤트를 순서대로 재생하여 전투 재현
}
```

#### 게임 분석 및 통계
```csharp
// CorrelationId로 퀘스트 완료 시간 분석
var questEvents = await analyticsGrain.GetQuestEventsAsync("quest-456");
var startTime = questEvents.First(e => e is QuestAcceptedEvent).Timestamp;
var endTime = questEvents.First(e => e is QuestCompletedEvent).Timestamp;
var completionTime = endTime - startTime;
```

#### 치팅 감지
```csharp
// 의심스러운 이벤트 패턴 감지
var battleEvents = await GetBattleReplayAsync("battle-789");
var damageEvents = battleEvents.OfType<DamageDealtEvent>();

// 비정상적으로 높은 DPS 체크
var totalDamage = damageEvents.Sum(e => e.Damage);
var duration = (battleEvents.Last().Timestamp - battleEvents.First().Timestamp).TotalSeconds;
var dps = totalDamage / duration;

if (dps > EXPECTED_MAX_DPS)
{
    await ReportCheatingSuspicionAsync(battleId);
}
```

#### 고객 지원
```csharp
// 거래 분쟁 해결 - 전체 거래 이력 조회
var tradeEvents = await GetTradeEventsAsync("trade-123");
// CS 팀에서 EventId, Timestamp, CorrelationId를 활용하여
// 정확한 거래 내역 확인 가능
```

### 6. 실전 권장사항

✅ **추천 사용 사례**
- 전투/PVP 리플레이 시스템
- 퀘스트 진행 추적 및 분석
- 거래/경제 시스템 감사
- 매칭 시스템 성능 분석
- 치팅 감지 및 분석
- 고객 지원용 이벤트 로그

❌ **피해야 할 사용**
- 초당 수백 건 발생하는 이벤트 (위치 업데이트 등) → 너무 많은 오버헤드
- 실시간 게임플레이 로직 (이벤트는 비동기이므로 실시간 처리에 부적합)
- 클라이언트에 직접 전송할 데이터 (별도의 메시지 타입 사용 권장)

---

## 📄 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

---

## 🤝 기여

기여는 언제나 환영합니다! 다음 방법으로 참여할 수 있습니다:

1. 이슈 등록 - 버그 리포트, 기능 요청
2. Pull Request - 코드 개선, 문서 업데이트
3. 튜토리얼 작성 - 새로운 예제 추가

### 기여 가이드라인

- 코드는 명확하고 이해하기 쉽게 작성
- 적절한 단위 테스트 포함
- 문서 업데이트 (README, 주석)
- 기존 코드 스타일 준수

---

## 📞 지원 및 커뮤니티

- **GitHub Issues**: [버그 리포트 및 기능 요청](https://github.com/your-org/OrleansX/issues)
- **Discussions**: [질문 및 토론](https://github.com/your-org/OrleansX/discussions)
- **Orleans 공식 문서**: https://learn.microsoft.com/orleans
- **Orleans GitHub**: https://github.com/dotnet/orleans

---

## 🎉 감사의 말

이 프로젝트는 [Microsoft Orleans](https://github.com/dotnet/orleans) 프레임워크를 기반으로 합니다. Orleans 팀과 커뮤니티에 감사드립니다.

---

**OrleansX로 분산 시스템을 쉽게 구축하세요!** 🚀
