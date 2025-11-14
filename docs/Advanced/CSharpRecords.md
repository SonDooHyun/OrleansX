# C# Record 타입 심층 가이드

## 📖 개요

C# 9.0에서 도입된 **Record 타입**은 주로 데이터를 담는 불변(immutable) 객체를 쉽게 만들기 위한 참조 타입입니다. 특히 **위치 기반 레코드(Positional Record)** 문법은 매우 간결하면서도 강력한 기능을 제공합니다.

## 🎯 OrleansX에서의 사용

### DatabaseOptions 예제

```csharp
// OrleansX.Abstractions/Options/OrleansClientOptions.cs (37번 라인)
public record DatabaseOptions(string DbInvariant, string ConnectionString);
```

**이 한 줄의 코드가 의미하는 것:**

```csharp
// 컴파일러가 자동으로 생성하는 코드 (개념적 표현)
public record DatabaseOptions
{
    // 1. 읽기 전용 속성 (Properties)
    public string DbInvariant { get; init; }
    public string ConnectionString { get; init; }

    // 2. 생성자
    public DatabaseOptions(string DbInvariant, string ConnectionString)
    {
        this.DbInvariant = DbInvariant;
        this.ConnectionString = ConnectionString;
    }

    // 3. Deconstruct 메서드 (분해 기능)
    public void Deconstruct(out string DbInvariant, out string ConnectionString)
    {
        DbInvariant = this.DbInvariant;
        ConnectionString = this.ConnectionString;
    }

    // 4. ToString 오버라이드
    public override string ToString()
    {
        return $"DatabaseOptions {{ DbInvariant = {DbInvariant}, ConnectionString = {ConnectionString} }}";
    }

    // 5. Equals 및 GetHashCode (값 기반 비교)
    public virtual bool Equals(DatabaseOptions? other)
    {
        return other != null &&
               DbInvariant == other.DbInvariant &&
               ConnectionString == other.ConnectionString;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DbInvariant, ConnectionString);
    }

    // 6. with 표현식 지원 (복사 생성자)
    public virtual DatabaseOptions Clone()
    {
        return new DatabaseOptions(DbInvariant, ConnectionString);
    }
}
```

## 🔍 Record vs Class 비교

### 1. 생성 및 사용 예제

```csharp
// ✅ Record 사용 (OrleansX 방식)
public record DatabaseOptions(string DbInvariant, string ConnectionString);

var dbOptions = new DatabaseOptions("MySql.Data.MySqlClient", "Server=localhost;...");

// 속성 접근
Console.WriteLine(dbOptions.DbInvariant); // "MySql.Data.MySqlClient"

// ToString 자동 구현
Console.WriteLine(dbOptions);
// 출력: "DatabaseOptions { DbInvariant = MySql.Data.MySqlClient, ConnectionString = Server=localhost;... }"
```

```csharp
// ❌ 같은 기능을 Class로 구현하면?
public class DatabaseOptions
{
    public string DbInvariant { get; }
    public string ConnectionString { get; }

    public DatabaseOptions(string dbInvariant, string connectionString)
    {
        DbInvariant = dbInvariant;
        ConnectionString = connectionString;
    }

    // ToString 수동 구현 필요
    public override string ToString()
    {
        return $"DatabaseOptions {{ DbInvariant = {DbInvariant}, ConnectionString = {ConnectionString} }}";
    }

    // Equals 수동 구현 필요
    public override bool Equals(object? obj)
    {
        if (obj is DatabaseOptions other)
        {
            return DbInvariant == other.DbInvariant &&
                   ConnectionString == other.ConnectionString;
        }
        return false;
    }

    // GetHashCode 수동 구현 필요
    public override int GetHashCode()
    {
        return HashCode.Combine(DbInvariant, ConnectionString);
    }
}
```

### 2. 값 기반 비교 (Value-based Equality)

```csharp
// Record: 값 기반 비교
var db1 = new DatabaseOptions("MySql.Data.MySqlClient", "Server=localhost");
var db2 = new DatabaseOptions("MySql.Data.MySqlClient", "Server=localhost");

Console.WriteLine(db1 == db2); // ✅ True (내용이 같으면 같은 것으로 간주)
Console.WriteLine(db1.Equals(db2)); // ✅ True

// Class: 참조 기반 비교 (Equals를 오버라이드하지 않으면)
public class DbConfig
{
    public string DbInvariant { get; set; }
    public string ConnectionString { get; set; }
}

var cfg1 = new DbConfig { DbInvariant = "MySql.Data.MySqlClient", ConnectionString = "Server=localhost" };
var cfg2 = new DbConfig { DbInvariant = "MySql.Data.MySqlClient", ConnectionString = "Server=localhost" };

Console.WriteLine(cfg1 == cfg2); // ❌ False (다른 인스턴스이므로)
Console.WriteLine(cfg1.Equals(cfg2)); // ❌ False (Equals를 오버라이드하지 않음)
```

## 💻 Record의 핵심 기능

### 1. with 표현식 (비파괴적 변경)

```csharp
var originalDb = new DatabaseOptions("MySql.Data.MySqlClient", "Server=prod");

// ConnectionString만 변경한 새로운 인스턴스 생성
var devDb = originalDb with { ConnectionString = "Server=dev" };

Console.WriteLine(originalDb.ConnectionString); // "Server=prod" (원본 불변)
Console.WriteLine(devDb.ConnectionString);      // "Server=dev" (새 인스턴스)
Console.WriteLine(devDb.DbInvariant);           // "MySql.Data.MySqlClient" (복사됨)
```

**OrleansX 실전 사용 예:**

```csharp
public class ConfigurationService
{
    private DatabaseOptions _productionDb = new("MySql.Data.MySqlClient", "Server=prod;...");

    public DatabaseOptions GetTestConfiguration()
    {
        // 프로덕션 설정을 기반으로 테스트 설정 생성
        return _productionDb with
        {
            ConnectionString = "Server=localhost;Database=test_db;..."
        };
    }
}
```

### 2. 분해(Deconstruction)

```csharp
var dbOptions = new DatabaseOptions("MySql.Data.MySqlClient", "Server=localhost");

// 튜플처럼 분해 가능
var (invariant, connStr) = dbOptions;

Console.WriteLine(invariant); // "MySql.Data.MySqlClient"
Console.WriteLine(connStr);   // "Server=localhost"
```

**실전 사용:**

```csharp
public void ConfigureDatabase(DatabaseOptions options)
{
    var (dbInvariant, connectionString) = options;

    DbProviderFactories.RegisterFactory(dbInvariant, MySqlClientFactory.Instance);
    _connection = new MySqlConnection(connectionString);
}
```

### 3. 패턴 매칭

```csharp
public string GetDatabaseType(DatabaseOptions options)
{
    return options switch
    {
        { DbInvariant: "MySql.Data.MySqlClient" } => "MySQL",
        { DbInvariant: "Npgsql" } => "PostgreSQL",
        { DbInvariant: "Microsoft.Data.SqlClient" } => "SQL Server",
        _ => "Unknown"
    };
}

// 사용
var mysqlOptions = new DatabaseOptions("MySql.Data.MySqlClient", "Server=localhost");
Console.WriteLine(GetDatabaseType(mysqlOptions)); // "MySQL"
```

## 🎮 OrleansX의 Record 사용 예제

### 1. 설정 옵션 정의

```csharp
// 간단한 설정 옵션
public record DatabaseOptions(string DbInvariant, string ConnectionString);
public record RetryOptions(int MaxAttempts, int BaseDelayMs, int MaxDelayMs);

// 사용
var dbOptions = new DatabaseOptions(
    "MySql.Data.MySqlClient",
    "Server=localhost;Database=orleans;User=root;Password=1234;"
);

var retryOptions = new RetryOptions(MaxAttempts: 3, BaseDelayMs: 200, MaxDelayMs: 10000);
```

### 2. 이벤트 데이터 정의

```csharp
// 게임 이벤트 레코드
public record PlayerJoinedEvent(
    string PlayerId,
    string PlayerName,
    DateTime JoinedAt
) : GrainEvent
{
    public PlayerJoinedEvent() : this(string.Empty, string.Empty, DateTime.UtcNow)
    {
        EventType = nameof(PlayerJoinedEvent);
    }
}

// 사용
var joinEvent = new PlayerJoinedEvent("player123", "Alice", DateTime.UtcNow);
Console.WriteLine(joinEvent);
// PlayerJoinedEvent { PlayerId = player123, PlayerName = Alice, JoinedAt = ... }

// with 표현식으로 CorrelationId 추가
var correlatedEvent = joinEvent with { CorrelationId = "session-456" };
```

### 3. 응답 DTO 정의

```csharp
// API 응답 레코드
public record PlayerInfoResponse(
    string PlayerId,
    string Name,
    int Level,
    long Gold,
    DateTime LastLogin
);

// Grain에서 사용
public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public Task<PlayerInfoResponse> GetPlayerInfoAsync()
    {
        return Task.FromResult(new PlayerInfoResponse(
            State.PlayerId,
            State.Name,
            State.Level,
            State.Gold,
            State.LastLogin
        ));
    }
}
```

### 4. 상태 스냅샷

```csharp
// 게임 상태 스냅샷
public record BattleSnapshot(
    string BattleId,
    string AttackerId,
    string DefenderId,
    int AttackerHp,
    int DefenderHp,
    int Turn,
    DateTime SnapshotTime
);

public class BattleGrain : StatefulGrainBase<BattleState>, IBattleGrain
{
    private readonly List<BattleSnapshot> _snapshots = new();

    public Task PerformAttackAsync(string attackerId, int damage)
    {
        // 공격 처리...

        // 스냅샷 저장 (불변 레코드이므로 안전하게 저장)
        _snapshots.Add(new BattleSnapshot(
            State.BattleId,
            attackerId,
            GetDefenderId(attackerId),
            GetHp(attackerId),
            GetHp(GetDefenderId(attackerId)),
            State.Turn,
            DateTime.UtcNow
        ));

        return Task.CompletedTask;
    }

    public Task<IEnumerable<BattleSnapshot>> GetBattleReplayAsync()
    {
        // 불변 레코드이므로 외부에서 수정할 수 없음!
        return Task.FromResult(_snapshots.AsEnumerable());
    }
}
```

## 🔧 Record의 고급 기능

### 1. Primary Constructor

```csharp
// 위치 기반 레코드 (Positional Record)
public record DatabaseOptions(string DbInvariant, string ConnectionString);

// 위치 기반 + 추가 속성
public record ExtendedDatabaseOptions(string DbInvariant, string ConnectionString)
{
    // 추가 속성 정의 가능
    public int MaxPoolSize { get; init; } = 100;
    public int Timeout { get; init; } = 30;
}

// 사용
var options = new ExtendedDatabaseOptions("MySql.Data.MySqlClient", "Server=localhost")
{
    MaxPoolSize = 200,
    Timeout = 60
};
```

### 2. 명명된 Record (명시적 속성)

```csharp
// 위치 기반이 아닌 명명된 Record
public record RetryConfiguration
{
    public int MaxAttempts { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 200;
    public int MaxDelayMs { get; init; } = 10000;
}

// 사용 (객체 초기화 구문)
var config = new RetryConfiguration
{
    MaxAttempts = 5,
    BaseDelayMs = 500
};
```

### 3. 상속

```csharp
// 베이스 레코드
public abstract record BaseEvent(string EventId, DateTime Timestamp);

// 파생 레코드
public record PlayerEvent(string EventId, DateTime Timestamp, string PlayerId)
    : BaseEvent(EventId, Timestamp);

public record ItemEvent(string EventId, DateTime Timestamp, string ItemId)
    : BaseEvent(EventId, Timestamp);

// 사용
BaseEvent evt = new PlayerEvent(Guid.NewGuid().ToString(), DateTime.UtcNow, "player123");

if (evt is PlayerEvent playerEvt)
{
    Console.WriteLine(playerEvt.PlayerId);
}
```

## ⚠️ Record 사용 시 주의사항

### 1. 참조 타입 멤버

```csharp
// ⚠️ 주의: 참조 타입 멤버는 얕은 복사(shallow copy)됨
public record GameState(string GameId, List<string> PlayerIds);

var state1 = new GameState("game1", new List<string> { "p1", "p2" });
var state2 = state1 with { GameId = "game2" };

state2.PlayerIds.Add("p3"); // ❌ state1.PlayerIds에도 영향!

Console.WriteLine(state1.PlayerIds.Count); // 3 (의도하지 않은 변경!)
Console.WriteLine(state2.PlayerIds.Count); // 3
```

```csharp
// ✅ 해결: 불변 컬렉션 사용
using System.Collections.Immutable;

public record GameState(string GameId, ImmutableList<string> PlayerIds);

var state1 = new GameState("game1", ImmutableList.Create("p1", "p2"));
var state2 = state1 with
{
    GameId = "game2",
    PlayerIds = state1.PlayerIds.Add("p3") // 새 리스트 생성
};

Console.WriteLine(state1.PlayerIds.Count); // 2 (변경되지 않음!)
Console.WriteLine(state2.PlayerIds.Count); // 3
```

### 2. Record는 참조 타입

```csharp
// Record는 class처럼 참조 타입
public record Point(int X, int Y);

Point p1 = new(10, 20);
Point? p2 = null; // ✅ null 가능

void ProcessPoint(Point point)
{
    // point는 참조이므로 null 체크 필요
    if (point == null)
        throw new ArgumentNullException(nameof(point));
}
```

### 3. Record Struct (C# 10.0+)

```csharp
// 값 타입 Record가 필요하면 record struct 사용
public record struct Position(int X, int Y);

Position p1 = new(10, 20);
Position p2 = p1; // 값 복사 (참조가 아님)

p2 = p2 with { X = 30 };

Console.WriteLine(p1.X); // 10 (변경되지 않음)
Console.WriteLine(p2.X); // 30
```

## 📊 Record vs Class vs Struct 비교

| 특징 | Record | Class | Struct |
|------|--------|-------|--------|
| **타입** | 참조 타입 | 참조 타입 | 값 타입 |
| **기본 동작** | 불변(권장) | 가변 | 가변 |
| **Equals** | 값 기반 | 참조 기반 | 값 기반 |
| **ToString** | 자동 구현 | 수동 구현 | 수동 구현 |
| **with 표현식** | ✅ 지원 | ❌ 미지원 | ❌ 미지원 |
| **상속** | ✅ 지원 | ✅ 지원 | ❌ 미지원 |
| **null 허용** | ✅ 가능 | ✅ 가능 | ❌ 불가 (nullable 제외) |
| **성능** | 힙 할당 | 힙 할당 | 스택 할당 |
| **사용 사례** | DTO, 이벤트, 설정 | 복잡한 비즈니스 로직 | 작은 데이터, 고성능 |

## 💡 언제 Record를 사용할까?

### ✅ Record 사용이 적합한 경우

1. **설정 옵션 (Options)**
   ```csharp
   public record DatabaseOptions(string DbInvariant, string ConnectionString);
   ```

2. **DTO (Data Transfer Object)**
   ```csharp
   public record PlayerInfoDto(string PlayerId, string Name, int Level);
   ```

3. **이벤트 데이터**
   ```csharp
   public record OrderCreatedEvent(string OrderId, decimal Amount, DateTime CreatedAt);
   ```

4. **응답 모델**
   ```csharp
   public record ApiResponse<T>(bool Success, T? Data, string? ErrorMessage);
   ```

5. **불변 상태 스냅샷**
   ```csharp
   public record GameStateSnapshot(string GameId, int Turn, List<PlayerData> Players);
   ```

### ❌ Class 사용이 더 나은 경우

1. **복잡한 비즈니스 로직을 가진 엔티티**
   ```csharp
   public class PlayerGrain : StatefulGrainBase<PlayerState> { }
   ```

2. **많은 메서드와 상태 변경을 가진 객체**
   ```csharp
   public class BattleEngine { }
   ```

3. **상속 계층이 복잡한 경우**
   ```csharp
   public class BaseService { }
   public class PlayerService : BaseService { }
   ```

## 🔗 관련 문서

- [의존성 주입 (DI)](DependencyInjection.md) - Record 타입을 Options 패턴으로 사용
- [이벤트 시스템](../Events/GrainEvent.md) - Record를 이벤트 데이터로 활용
- [Orleans 기초](../Orleans-Basics.md) - Grain 상태와 Record

## 📚 참고 자료

- [C# Record 공식 문서](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record)
- [위치 기반 레코드](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record#positional-syntax-for-property-definition)
- [with 표현식](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/with-expression)
