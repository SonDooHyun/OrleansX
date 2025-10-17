# OrleansX Framework

Orleans 기반의 범용 분산 상태 관리 프레임워크입니다. 게임, 실시간 애플리케이션, IoT 등 다양한 도메인에서 재사용 가능하도록 설계되었습니다.

## 📋 목차

- [소개](#소개)
- [주요 기능](#주요-기능)
- [아키텍처](#아키텍처)
- [프로젝트 구조](#프로젝트-구조)
- [시작하기](#시작하기)
- [예제: 게임 파티 & 매칭 시스템](#예제-게임-파티--매칭-시스템)
- [라이브러리 사용법](#라이브러리-사용법)
- [기술 스택](#기술-스택)

---

## 🎯 소개

**OrleansX**는 Microsoft Orleans를 기반으로 한 범용 분산 상태 관리 프레임워크입니다. 

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

### Orleans의 주요 이점

- **간단한 프로그래밍 모델**: 일반 C# 객체처럼 작성
- **자동 동시성 제어**: Lock이나 Transaction 불필요
- **자동 확장**: 부하에 따라 자동으로 Grain 분산
- **상태 관리**: 메모리와 영구 저장소 통합

---

## ✨ 주요 기능

### 1. Client 라이브러리 (OrleansX.Client)
- Orleans 클러스터 연결 관리
- 지수 백오프 재시도 정책
- Idempotency Key 지원
- Circuit Breaker 패턴
- 간편한 DI 통합

### 2. Silo 호스팅 (OrleansX.Silo.Hosting)
- 표준화된 Silo 설정
- 다양한 스토리지 Provider 지원 (Memory, ADO.NET, Redis)
- 스트림 Provider 지원 (Memory, Kafka, EventHubs)
- 클러스터링 옵션 (Localhost, ADO.NET)

### 3. Grain 베이스 클래스 (OrleansX.Grains)
- `StatefulGrainBase<TState>`: 상태를 가진 Grain의 베이스 클래스
- `StatelessGrainBase`: 상태가 없는 Grain의 베이스 클래스
- 스트림 헬퍼 유틸리티

### 4. 테스트 키트 (OrleansX.TestKit)
- In-Memory 테스트 클러스터
- xUnit 통합
- 빠른 단위/통합 테스트

---

## 🏗️ 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  ┌──────────────┐              ┌──────────────┐             │
│  │  API Server  │              │ Worker/Batch │             │
│  │  (ASP.NET)   │              │   Service    │             │
│  └──────┬───────┘              └──────┬───────┘             │
│         │                              │                     │
└─────────┼──────────────────────────────┼─────────────────────┘
          │                              │
          └──────────┬───────────────────┘
                     │
          ┌──────────▼──────────┐
          │  OrleansX.Client    │
          │  - GrainInvoker     │
          │  - RetryPolicy      │
          │  - Idempotency      │
          └──────────┬──────────┘
                     │
                     │ TCP
                     │
┌────────────────────▼────────────────────────────────────────┐
│                   Orleans Cluster                            │
│  ┌────────────────────────────────────────────────────┐     │
│  │              Silo 1              Silo 2            │     │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐        │     │
│  │  │ Grain A  │  │ Grain B  │  │ Grain C  │        │     │
│  │  │ (Memory) │  │ (Memory) │  │ (Memory) │        │     │
│  │  └─────┬────┘  └─────┬────┘  └─────┬────┘        │     │
│  │        │             │             │              │     │
│  │        └─────────────┴─────────────┘              │     │
│  │                      │                            │     │
│  └──────────────────────┼────────────────────────────┘     │
│                         │                                   │
│         ┌───────────────▼───────────────┐                  │
│         │  OrleansX.Silo.Hosting        │                  │
│         │  - Storage Providers          │                  │
│         │  - Stream Providers           │                  │
│         │  - Clustering                 │                  │
│         └───────────────┬───────────────┘                  │
└─────────────────────────┼──────────────────────────────────┘
                          │
          ┌───────────────┴───────────────┐
          │                               │
  ┌───────▼────────┐            ┌─────────▼──────┐
  │   PostgreSQL   │            │   Redis/Kafka  │
  │  (Clustering,  │            │   (Streams)    │
  │   Persistence) │            │                │
  └────────────────┘            └────────────────┘
```

---

## 📁 프로젝트 구조

```
OrleansX/
├── src/                                    # 라이브러리 프로젝트
│   ├── OrleansX.Abstractions/             # 인터페이스, DTO, 이벤트 계약
│   │   ├── IGrainInvoker.cs
│   │   ├── IRetryPolicy.cs
│   │   ├── IIdempotencyKeyProvider.cs
│   │   ├── Options/
│   │   │   ├── OrleansClientOptions.cs
│   │   │   └── OrleansXSiloOptions.cs
│   │   └── Events/
│   │       └── GrainEvent.cs
│   │
│   ├── OrleansX.Grains/                   # 베이스 Grain 클래스
│   │   ├── StatefulGrainBase.cs
│   │   ├── StatelessGrainBase.cs
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
│   │       ├── SiloBuilderExtensions.cs
│   │       └── SiloBuilderExtensionsWithAction.cs
│   │
│   └── OrleansX.TestKit/                  # 테스트 유틸리티
│       └── OrleansXTestClusterFixture.cs
│
├── examples/                               # 예제 프로젝트
│   ├── Example.Grains/                    # 게임 Grain 구현
│   │   ├── Models/
│   │   │   ├── PartyState.cs
│   │   │   └── MatchmakingState.cs
│   │   ├── Interfaces/
│   │   │   ├── IPartyGrain.cs
│   │   │   └── IMatchmakingGrain.cs
│   │   ├── PartyGrain.cs
│   │   └── MatchmakingGrain.cs
│   │
│   ├── Example.Api/                       # API 서버
│   │   └── Program.cs
│   │
│   └── Example.SiloHost/                  # Silo 호스트
│       └── Program.cs
│
├── OrleansX.sln                           # 솔루션 파일
├── OrleansX_PRD.md                        # 제품 요구사항 문서
└── README.md                              # 이 문서
```

---

## 🚀 시작하기

### 사전 요구사항

- .NET 9.0 SDK 이상
- (옵션) PostgreSQL - 프로덕션 환경에서 클러스터링/영속성
- (옵션) Redis/Kafka - 스트림 처리

### 1. 프로젝트 복제 및 빌드

```bash
# 저장소 복제
git clone <repository-url>
cd OrleansX

# 전체 빌드
dotnet build OrleansX.sln
```

### 2. 예제 실행

#### Silo 실행

터미널 1에서:
```bash
cd examples/Example.SiloHost
dotnet run
```

출력:
```
================================================================================
OrleansX Example - Silo Host
Game Party & Matchmaking System
================================================================================

Starting Orleans Silo...

info: Orleans.Hosting.SiloHostedService[0]
      Starting Orleans Silo
...
```

#### API 서버 실행

터미널 2에서:
```bash
cd examples/Example.Api
dotnet run
```

출력:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

## 🎮 예제: 게임 파티 & 매칭 시스템

이 예제는 실제 Live 게임에서 사용할 수 있는 파티 및 매칭 시스템을 구현합니다.

### 주요 기능

1. **파티 관리**
   - 파티 생성/해산
   - 멤버 참가/탈퇴
   - 리더 자동 위임

2. **매칭 시스템**
   - 레이팅 기반 매칭
   - 대기 시간에 따른 범위 확대
   - 자동 매치 생성

### API 사용 예제

#### 1. 파티 생성

```bash
curl -X POST http://localhost:5000/api/parties \
  -H "Content-Type: application/json" \
  -d '{
    "leaderId": "player-001",
    "leaderName": "Alice",
    "maxMembers": 4
  }'
```

응답:
```json
{
  "partyId": "d4f9a8b2-..."
}
```

#### 2. 파티 참가

```bash
curl -X POST http://localhost:5000/api/parties/d4f9a8b2-.../join \
  -H "Content-Type: application/json" \
  -d '{
    "playerId": "player-002",
    "playerName": "Bob",
    "level": 10
  }'
```

#### 3. 파티 정보 조회

```bash
curl http://localhost:5000/api/parties/d4f9a8b2-...
```

응답:
```json
{
  "partyId": "d4f9a8b2-...",
  "leaderId": "player-001",
  "members": [
    {
      "playerId": "player-001",
      "playerName": "Alice",
      "level": 5,
      "joinedAt": "2025-10-17T08:00:00Z"
    },
    {
      "playerId": "player-002",
      "playerName": "Bob",
      "level": 10,
      "joinedAt": "2025-10-17T08:01:00Z"
    }
  ],
  "maxMembers": 4,
  "status": "Waiting",
  "isMatchmaking": false
}
```

#### 4. 매칭 시작

```bash
curl -X POST http://localhost:5000/api/parties/d4f9a8b2-.../matchmaking/start
```

#### 5. 매칭 큐 상태 확인

```bash
curl http://localhost:5000/api/matchmaking/queue
```

응답:
```json
{
  "queueSize": 3
}
```

### 아키텍처 흐름

```
┌──────────┐      ┌──────────────┐      ┌────────────┐
│  Client  │─────▶│  Example.Api │─────▶│ PartyGrain │
└──────────┘      └──────────────┘      └─────┬──────┘
                                              │
                                              │ StartMatchmaking
                                              │
                                              ▼
                                    ┌──────────────────────┐
                                    │ MatchmakingGrain     │
                                    │ - Enqueue Party      │
                                    │ - TryMatch           │
                                    │ - Create Match       │
                                    └──────────┬───────────┘
                                              │
                                              │ OnMatchFound
                                              │
                     ┌────────────────────────┴────────────┐
                     ▼                                     ▼
              ┌────────────┐                      ┌────────────┐
              │ PartyGrain │                      │ PartyGrain │
              │   (Team A) │                      │   (Team B) │
              └────────────┘                      └────────────┘
```

### Grain 설계

#### PartyGrain
- **책임**: 파티 상태 관리, 멤버 관리
- **상태**: 파티 ID, 리더, 멤버 목록, 상태
- **주요 메서드**:
  - `CreateAsync()`: 파티 생성
  - `JoinAsync()`: 멤버 참가
  - `LeaveAsync()`: 멤버 탈퇴
  - `StartMatchmakingAsync()`: 매칭 시작

#### MatchmakingGrain
- **책임**: 매칭 큐 관리, 매치 생성
- **상태**: 대기 중인 파티 목록, 활성 매치
- **주요 메서드**:
  - `EnqueuePartyAsync()`: 파티를 큐에 추가
  - `TryMatchAsync()`: 매칭 시도 (레이팅 기반)

---

## 📚 라이브러리 사용법

### 1. Client 사용법

```csharp
using OrleansX.Client.Extensions;
using OrleansX.Abstractions;
using OrleansX.Abstractions.Options;

var builder = WebApplication.CreateBuilder(args);

// OrleansX Client 등록
builder.Services.AddOrleansXClient(new OrleansClientOptions
{
    ClusterId = "my-cluster",
    ServiceId = "my-service",
    Db = new DatabaseOptions("Npgsql", "Host=localhost;Database=orleans;"),
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        BaseDelayMs = 200,
        MaxDelayMs = 10000
    }
});

var app = builder.Build();

// Grain 호출
app.MapGet("/greet/{name}", async (string name, IGrainInvoker invoker) =>
{
    var grain = invoker.GetGrain<IMyGrain>(name);
    var result = await grain.SayHelloAsync();
    return Results.Ok(new { message = result });
});

app.Run();
```

### 2. Silo 사용법

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
            .WithStreams(new StreamsOptions.Memory("Default"));
    });
});

var host = builder.Build();
await host.RunAsync();
```

### 3. Grain 작성

```csharp
using OrleansX.Grains;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

// 인터페이스 정의
public interface IMyGrain : IGrainWithStringKey
{
    Task<string> SayHelloAsync();
    Task SetNameAsync(string name);
}

// 상태 정의
[GenerateSerializer]
public class MyGrainState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;
    
    [Id(1)]
    public int VisitCount { get; set; }
}

// Grain 구현
public class MyGrain : StatefulGrainBase<MyGrainState>, IMyGrain
{
    public MyGrain(
        [PersistentState("mystate")] IPersistentState<MyGrainState> state,
        ILogger<MyGrain> logger) : base(state, logger)
    {
    }

    public Task<string> SayHelloAsync()
    {
        State.VisitCount++;
        await SaveStateAsync();
        
        return Task.FromResult(
            $"Hello, {State.Name}! Visit count: {State.VisitCount}");
    }

    public async Task SetNameAsync(string name)
    {
        State.Name = name;
        await SaveStateAsync();
    }
}
```

### 4. 테스트 작성

```csharp
using OrleansX.TestKit;
using Xunit;

[Collection("OrleansXCluster")]
public class MyGrainTests
{
    private readonly OrleansXTestClusterFixture _fixture;

    public MyGrainTests(OrleansXTestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SayHello_ReturnsGreeting()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory
            .GetGrain<IMyGrain>("test-user");
        await grain.SetNameAsync("Alice");

        // Act
        var result = await grain.SayHelloAsync();

        // Assert
        Assert.Contains("Hello, Alice", result);
        Assert.Contains("Visit count: 1", result);
    }
}
```

---

## 🔧 기술 스택

| 분류 | 기술 |
|------|------|
| **프레임워크** | .NET 9.0 |
| **Orleans** | Microsoft Orleans 9.2.1 |
| **스토리지** | ADO.NET (PostgreSQL), Redis, Memory |
| **스트림** | Memory, Kafka, Azure Event Hubs |
| **테스트** | xUnit, Orleans.TestingHost |
| **로깅** | Microsoft.Extensions.Logging |

---

## 📖 Orleans 주요 개념

### Grain Lifecycle

```
┌─────────────┐
│  Inactive   │
└──────┬──────┘
       │ 첫 번째 호출
       ▼
┌─────────────┐
│ Activating  │ ◄─── OnActivateAsync() 호출
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Active    │ ◄─── 메서드 호출 처리
└──────┬──────┘
       │ Idle timeout 또는 DeactivateOnIdle()
       ▼
┌─────────────┐
│ Deactivating│ ◄─── OnDeactivateAsync() 호출
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Inactive   │
└─────────────┘
```

### 상태 영속성

```csharp
// 상태 읽기
await State.ReadStateAsync();

// 상태 수정 및 저장
State.MyProperty = newValue;
await State.WriteStateAsync();

// 상태 삭제
await State.ClearStateAsync();
```

### 스트림 사용

```csharp
// Producer
var stream = this.GetStreamProvider("Default")
    .GetStream<MyEvent>(streamId);
await stream.OnNextAsync(new MyEvent { ... });

// Consumer
var subscription = await stream.SubscribeAsync(
    async (data, token) => {
        // 이벤트 처리
    });
```

---

## 🎓 Best Practices

### 1. Grain 설계
- **단일 책임**: 각 Grain은 하나의 엔티티만 관리
- **불변 메시지**: DTO는 불변 객체로 설계
- **비동기 우선**: 모든 메서드는 Task 반환

### 2. 상태 관리
- **적절한 저장 시점**: 비즈니스 로직 완료 후 저장
- **낙관적 동시성**: ETag를 활용한 충돌 감지
- **스냅샷**: 큰 상태는 주기적으로 스냅샷

### 3. 성능 최적화
- **Grain 호출 최소화**: 여러 정보를 한 번에 조회
- **Stateless Worker**: 무상태 작업은 StatelessWorker 사용
- **캐싱**: 자주 읽는 데이터는 메모리에 캐시

### 4. 에러 처리
- **재시도 정책**: 일시적 오류는 자동 재시도
- **Circuit Breaker**: 연속된 실패 시 차단
- **로깅**: 모든 중요 이벤트 로깅

---

## 📄 라이선스

MIT License

---

## 🤝 기여

기여는 환영합니다! 이슈나 PR을 올려주세요.

---

## 📞 지원

문제가 발생하면 GitHub Issues에 등록해주세요.
