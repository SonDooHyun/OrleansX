# OrleansX Framework

Orleans 기반의 엔터프라이즈급 분산 상태 관리 프레임워크입니다. 게임, 실시간 애플리케이션, IoT 등 다양한 도메인에서 재사용 가능하도록 설계되었으며, **분산 트랜잭션(Distributed Transactions)**을 지원합니다.

## 📋 목차

- [소개](#-소개)
- [주요 기능](#-주요-기능)
- [프로젝트 구조](#-프로젝트-구조)
- [시작하기](#-시작하기)
- [라이브러리 사용법](#-라이브러리-사용법)
- [예제 프로젝트](#-예제-프로젝트)
- [기술 스택](#-기술-스택)

---

## 🎯 소개

**OrleansX**는 Microsoft Orleans를 기반으로 한 프로덕션 레디 분산 상태 관리 프레임워크입니다.

### Orleans란?

Orleans는 Microsoft에서 개발한 **Virtual Actor Model** 기반의 분산 시스템 프레임워크입니다.

#### Virtual Actor Model의 핵심 개념

1. **Grain (가상 액터)**
   - 상태와 행동을 가진 분산 객체
   - 고유한 ID로 식별됨
   - 단일 쓰레드로 실행되어 동시성 문제 자동 해결
   - 필요할 때 자동으로 활성화/비활성화

2. **위치 투명성**
   - Grain이 어느 서버에 있든 동일한 방식으로 호출
   - 프레임워크가 자동으로 라우팅 처리

3. **확장성**
   - 서버를 추가하면 자동으로 부하 분산
   - 수평 확장이 쉬움

4. **내결함성**
   - Grain 상태를 영구 저장소에 저장 가능
   - 서버 장애 시 다른 서버에서 자동 복구

### OrleansX의 차별점

- ✅ **즉시 사용 가능한 베이스 클래스**
- ✅ **분산 트랜잭션(ACID) 지원**
- ✅ **재시도 및 멱등성 내장**
- ✅ **표준화된 설정 패턴**
- ✅ **통합 테스트 키트**

---

## ✨ 주요 기능

### 1. OrleansX.Abstractions
> 인터페이스, 옵션, 이벤트 계약

| 기능 | 설명 |
|------|------|
| **IGrainInvoker** | Grain 호출 추상화 인터페이스 |
| **IRetryPolicy** | 재시도 정책 인터페이스 |
| **IIdempotencyKeyProvider** | 멱등성 키 관리 인터페이스 |
| **OrleansClientOptions** | Client 설정 옵션 (클러스터, 재시도, DB) |
| **OrleansXSiloOptions** | Silo 설정 옵션 (클러스터링, 영속성, 스트림, 트랜잭션) |
| **GrainEvent<T>** | Grain 이벤트 베이스 클래스 |
| **OrleansXException** | OrleansX 전용 예외 |

#### 주요 옵션 클래스

```csharp
// Clustering 옵션
ClusteringOptions.Localhost()
ClusteringOptions.AdoNet(dbInvariant, connectionString)
ClusteringOptions.Redis(connectionString)

// Persistence 옵션
PersistenceOptions.Memory()
PersistenceOptions.AdoNet(dbInvariant, connectionString)
PersistenceOptions.Redis(connectionString)

// Streams 옵션
StreamsOptions.Memory(streamProvider)
StreamsOptions.Kafka(bootstrapServers, streamProvider)
StreamsOptions.EventHubs(connectionString, streamProvider)

// 🆕 Transaction 옵션
TransactionOptions.Memory()
TransactionOptions.AzureStorage(connectionString)
TransactionOptions.AdoNet(dbInvariant, connectionString)
```

### 2. OrleansX.Grains
> 재사용 가능한 Grain 베이스 클래스

| 클래스 | 설명 | 사용 시기 |
|--------|------|-----------|
| **StatefulGrainBase<TState>** | 영속 상태를 가진 Grain | 일반적인 상태 관리 |
| **StatelessGrainBase** | 상태가 없는 Grain | 유틸리티, 계산 로직 |
| **🆕 TransactionalGrainBase<TState>** | 트랜잭션 상태를 가진 Grain | ACID가 필요한 경우 (금융, 재고 등) |
| **StreamHelper** | 스트림 작업 헬퍼 유틸리티 | 이벤트 발행/구독 |

#### StatefulGrainBase 기능

```csharp
// 상태 관리
protected TState State { get; }
protected bool IsStateRecorded { get; }
protected string? StateEtag { get; }

// 메서드
protected Task SaveStateAsync()
protected Task ReadStateAsync()
protected Task ClearStateAsync()
protected Task UpdateStateAsync(Action<TState> updateAction)
```

#### 🆕 TransactionalGrainBase 기능

```csharp
// 트랜잭션 상태 관리 (ACID 보장)
protected Task<TState> GetStateAsync()
protected Task UpdateStateAsync(Action<TState> updateAction)
protected Task<TResult> UpdateStateAsync<TResult>(Func<TState, TResult> updateFunc)
protected Task<TResult> ReadStateAsync<TResult>(Func<TState, TResult> readFunc)
```

**트랜잭션 특징:**
- ✅ **원자성(Atomicity)**: All-or-Nothing 보장
- ✅ **일관성(Consistency)**: 여러 Grain 간 일관된 상태
- ✅ **격리성(Isolation)**: 트랜잭션 간 격리
- ✅ **자동 롤백**: 예외 발생 시 자동 롤백

### 3. OrleansX.Client
> Orleans Client 래퍼 및 고급 기능

| 기능 | 설명 |
|------|------|
| **GrainInvoker** | Grain 호출 래퍼 (재시도, 멱등성 포함) |
| **ExponentialRetryPolicy** | 지수 백오프 재시도 정책 |
| **AsyncLocalIdempotencyKeyProvider** | 비동기 컨텍스트 기반 멱등성 키 관리 |
| **ServiceCollectionExtensions** | DI 통합 확장 메서드 |

#### 특징

- **자동 재시도**: 일시적 오류 자동 복구
- **Circuit Breaker**: 연속 실패 시 차단
- **Idempotency**: 중복 요청 방지
- **연결 관리**: 자동 재연결

### 4. OrleansX.Silo.Hosting
> Silo 호스팅 확장 및 표준화된 설정

| 기능 | 설명 |
|------|------|
| **SiloBuilderExtensions** | Fluent API 기반 Silo 설정 |
| **클러스터링 지원** | Localhost, ADO.NET, Redis |
| **영속성 지원** | Memory, ADO.NET, Redis |
| **스트림 지원** | Memory, Kafka, Azure Event Hubs |
| **🆕 트랜잭션 지원** | Memory, Azure Storage, ADO.NET |

#### Fluent API 예제

```csharp
siloBuilder.UseOrleansXDefaults(opts =>
{
    opts.WithCluster("game-cluster", "game-service")
        .WithPorts(siloPort: 11111, gatewayPort: 30000)
        .WithClustering(new ClusteringOptions.Localhost())
        .WithPersistence(new PersistenceOptions.Memory())
        .WithStreams(new StreamsOptions.Memory("Default"))
        .WithTransactions(new TransactionOptions.Memory()); // 🆕 트랜잭션
});
```

### 5. OrleansX.TestKit
> 통합 테스트 유틸리티

| 기능 | 설명 |
|------|------|
| **OrleansXTestClusterFixture** | xUnit 테스트 클러스터 Fixture |
| **In-Memory 클러스터** | 빠른 테스트 실행 |
| **자동 초기화/정리** | 테스트 격리 보장 |

#### 사용 예제

```csharp
[Collection("OrleansXCluster")]
public class MyGrainTests
{
    private readonly OrleansXTestClusterFixture _fixture;

    public MyGrainTests(OrleansXTestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestGrain()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMyGrain>("key");
        var result = await grain.DoSomethingAsync();
        Assert.NotNull(result);
    }
}
```

---

## 📁 프로젝트 구조

```
OrleansX/
├── src/                                    # 라이브러리 프로젝트
│   ├── OrleansX.Abstractions/             # 인터페이스, DTO, 옵션
│   │   ├── Events/
│   │   │   └── GrainEvent.cs
│   │   ├── Exceptions/
│   │   │   └── OrleansXException.cs
│   │   ├── Options/
│   │   │   ├── OrleansClientOptions.cs
│   │   │   └── OrleansXSiloOptions.cs
│   │   ├── IGrainInvoker.cs
│   │   ├── IRetryPolicy.cs
│   │   └── IIdempotencyKeyProvider.cs
│   │
│   ├── OrleansX.Grains/                   # Grain 베이스 클래스
│   │   ├── StatefulGrainBase.cs           # 영속 상태 Grain
│   │   ├── StatelessGrainBase.cs          # 상태 없는 Grain
│   │   ├── TransactionalGrainBase.cs      # 🆕 트랜잭션 Grain
│   │   └── Utilities/
│   │       └── StreamHelper.cs
│   │
│   ├── OrleansX.Client/                   # Orleans Client 래퍼
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
│   │       └── SiloBuilderExtensions.cs
│   │
│   └── OrleansX.TestKit/                  # 테스트 유틸리티
│       └── OrleansXTestClusterFixture.cs
│
├── examples/                               # 예제 프로젝트
│   ├── 1-Tutorial/                        # 기본 사용법 튜토리얼
│   ├── 2-GameMatchmaking/                 # 게임 매칭 시스템
│   └── README.md                          # 예제 설명서
│
├── OrleansX.sln
└── README.md
```

---

## 🚀 시작하기

### 사전 요구사항

- .NET 9.0 SDK 이상
- (옵션) **데이터베이스** - SQL Server, PostgreSQL, MySQL 등 (프로덕션 환경)
- (옵션) Redis - 캐싱/스트림
- (옵션) Kafka - 이벤트 스트리밍

### 빌드 및 실행

```bash
# 저장소 복제
git clone <repository-url>
cd OrleansX

# 전체 빌드
dotnet build

# 예제 실행 (자세한 내용은 examples/README.md 참조)
cd examples/2-GameMatchmaking/GameMatchmaking.SiloHost
dotnet run
```

---

## 📚 라이브러리 사용법

### 1. Silo 설정

```csharp
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using OrleansX.Silo.Hosting.Extensions;
using OrleansX.Abstractions.Options;

var builder = Host.CreateDefaultBuilder(args);

builder.UseOrleans((context, siloBuilder) =>
{
    siloBuilder.UseOrleansXDefaults(opts =>
    {
        opts.WithCluster("my-cluster", "my-service")
            .WithPorts(siloPort: 11111, gatewayPort: 30000)
            .WithClustering(new ClusteringOptions.Localhost())
            .WithPersistence(new PersistenceOptions.Memory())
            .WithStreams(new StreamsOptions.Memory("Default"))
            .WithTransactions(new TransactionOptions.Memory()); // 🆕 트랜잭션
    });
});

var host = builder.Build();
await host.RunAsync();
```

### 2. Client 설정

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

app.MapGet("/hello/{name}", async (string name, IGrainInvoker invoker) =>
{
    var grain = invoker.GetGrain<IHelloGrain>(name);
    return await grain.SayHelloAsync();
});

app.Run();
```

### 3. 일반 Grain 작성

```csharp
using OrleansX.Grains;
using Orleans.Runtime;

// 상태 정의
[GenerateSerializer]
public class PlayerState
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public int Level { get; set; }
    [Id(2)] public int Experience { get; set; }
}

// Grain 구현
public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task GainExperienceAsync(int exp)
    {
        await UpdateStateAsync(state =>
        {
            state.Experience += exp;
            if (state.Experience >= 100)
            {
                state.Level++;
                state.Experience = 0;
            }
        });
    }
}
```

### 4. 🆕 트랜잭션 Grain 작성

```csharp
using OrleansX.Grains;
using Orleans.Transactions.Abstractions;

// 계좌 상태 (트랜잭션)
[GenerateSerializer]
public class AccountState
{
    [Id(0)] public string AccountNumber { get; set; } = string.Empty;
    [Id(1)] public decimal Balance { get; set; }
}

// 트랜잭션 Grain (ACID 보장)
public class AccountGrain : TransactionalGrainBase<AccountState>, IAccountGrain
{
    public AccountGrain(
        [TransactionalState("account")] ITransactionalState<AccountState> state,
        ILogger<AccountGrain> logger)
        : base(state, logger)
    {
    }

    // 🔷 트랜잭션 메서드 - 입금
    [Transaction(TransactionOption.Join)]
    public async Task DepositAsync(decimal amount)
    {
        await UpdateStateAsync(state =>
        {
            state.Balance += amount;
        });
    }

    // 🔷 트랜잭션 메서드 - 출금
    [Transaction(TransactionOption.Join)]
    public async Task WithdrawAsync(decimal amount)
    {
        await UpdateStateAsync(state =>
        {
            if (state.Balance < amount)
                throw new InvalidOperationException("Insufficient balance");
            state.Balance -= amount;
        });
    }

    // ⚪ 일반 메서드 - 조회 (트랜잭션 불필요)
    public async Task<decimal> GetBalanceAsync()
    {
        return await ReadStateAsync(state => state.Balance);
    }
}

// 계좌 이체 Grain (여러 Grain 간 트랜잭션)
public class TransferGrain : Grain, ITransferGrain
{
    [Transaction(TransactionOption.Create)] // 🆕 트랜잭션 생성
    public async Task<bool> TransferAsync(string fromAccount, string toAccount, decimal amount)
    {
        var fromGrain = GrainFactory.GetGrain<IAccountGrain>(fromAccount);
        var toGrain = GrainFactory.GetGrain<IAccountGrain>(toAccount);

        // 출금과 입금이 원자적으로 처리 (All-or-Nothing)
        await fromGrain.WithdrawAsync(amount);
        await toGrain.DepositAsync(amount);

        return true;
    }
}
```

**트랜잭션 속성:**
- `TransactionOption.Create`: 새 트랜잭션 생성
- `TransactionOption.Join`: 기존 트랜잭션 참여
- `TransactionOption.CreateOrJoin`: 있으면 참여, 없으면 생성
- `TransactionOption.Suppress`: 트랜잭션 없이 실행
- `TransactionOption.NotAllowed`: 트랜잭션 컨텍스트에서 호출 시 예외

---

## 🎮 예제 프로젝트

자세한 내용은 [examples/README.md](examples/README.md)를 참조하세요.

### 1. Tutorial - 기본 사용법
- StatefulGrain 사용
- StatelessGrain 사용
- TransactionalGrain 사용 (🆕)
- Stream 사용
- 테스트 작성

### 2. Game Matchmaking - 실전 예제
- 플레이어 관리
- 파티 시스템
- MMR 기반 매칭 (개인/파티)
- 룸 및 캐릭터 선택
- 트랜잭션 활용 (파티 생성, 매칭 완료)

---

## 🔧 기술 스택

| 분류 | 기술 |
|------|------|
| **프레임워크** | .NET 9.0 |
| **Orleans** | Microsoft Orleans 9.2.1 |
| **트랜잭션** | Microsoft.Orleans.Transactions 9.2.1 🆕 |
| **스토리지** | ADO.NET (SQL Server, PostgreSQL, MySQL), Redis, Memory |
| **스트림** | Memory, Kafka, Azure Event Hubs |
| **테스트** | xUnit, Orleans.TestingHost |
| **로깅** | Microsoft.Extensions.Logging |

---

## 🎓 Best Practices

### Grain 설계
- ✅ 단일 책임 원칙
- ✅ 불변 메시지 사용
- ✅ 모든 메서드는 async
- ✅ ACID가 필요한 경우 트랜잭션 사용 🆕

### 트랜잭션 사용 시 주의사항 🆕
- ✅ 금융, 재고 등 중요한 데이터에 사용
- ✅ 트랜잭션 범위를 최소화
- ✅ 읽기 전용은 일반 메서드 사용
- ⚠️ 트랜잭션 내에서 외부 API 호출 지양
- ⚠️ 긴 작업은 트랜잭션 분리

### 성능 최적화
- ✅ Grain 호출 최소화
- ✅ Stateless Worker 활용
- ✅ 적절한 캐싱
- ✅ 불필요한 트랜잭션 지양

---

## 📄 라이선스

MIT License

---

## 🤝 기여

기여는 환영합니다! 이슈나 PR을 올려주세요.

---

## 📞 지원

문제가 발생하면 GitHub Issues에 등록해주세요.
