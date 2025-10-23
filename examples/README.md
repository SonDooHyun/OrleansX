# OrleansX Examples

OrleansX 프레임워크의 사용법을 보여주는 실전 예제 모음입니다.

## 📋 목차

- [프로젝트 구조](#프로젝트-구조)
- [빠른 시작](#빠른-시작)
- [게임 매칭 시스템](#게임-매칭-시스템)
  - [시스템 개요](#시스템-개요)
  - [아키텍처](#아키텍처)
  - [데이터 모델](#데이터-모델)
  - [Grain 구현](#grain-구현)
  - [API 사용법](#api-사용법)
- [워크플로우](#워크플로우)
  - [개인 매칭 플로우](#1️⃣-개인-매칭-플로우)
  - [파티 매칭 플로우](#2️⃣-파티-매칭-플로우)
  - [게임 룸 플로우](#3️⃣-게임-룸-플로우)

---

## 프로젝트 구조

```
examples/
├── Example.Grains/              # Grain 구현
│   ├── Interfaces/              # Grain 인터페이스
│   │   ├── IPlayerGrain.cs
│   │   ├── IPartyGrain.cs
│   │   ├── IMatchmakingGrain.cs
│   │   └── IRoomGrain.cs
│   ├── Models/                  # 데이터 모델
│   │   ├── PlayerState.cs
│   │   ├── PartyState.cs
│   │   ├── MatchmakingState.cs
│   │   ├── RoomState.cs
│   │   └── CharacterInfo.cs
│   ├── PlayerGrain.cs           # 플레이어 관리
│   ├── PartyGrain.cs            # 파티 관리 (트랜잭션 사용)
│   ├── MatchmakingGrain.cs      # 매칭 시스템 (MMR 기반)
│   └── RoomGrain.cs             # 게임 룸 (캐릭터 선택)
│
├── Example.Api/                 # REST API 서버
│   └── Program.cs               # API 엔드포인트
│
├── Example.SiloHost/            # Orleans Silo 호스트
│   └── Program.cs               # Silo 설정
│
└── README.md                    # 이 문서
```

---

## 빠른 시작

### 1. Silo 실행

터미널 1:
```bash
cd examples/Example.SiloHost
dotnet run
```

출력:
```
================================================================================
OrleansX Game Matchmaking - Silo Host
Features: Player, Party, Matchmaking, Room, Transaction
================================================================================

⚙️  Cluster ID: game-cluster
⚙️  Service ID: game-service
⚙️  Silo Port: 11111
⚙️  Gateway Port: 30000
⚙️  Transaction: Enabled (Memory)

Starting Orleans Silo...
```

### 2. API 서버 실행

터미널 2:
```bash
cd examples/Example.Api
dotnet run
```

출력:
```
================================================================================
OrleansX Game Matchmaking API
Listening on: http://localhost:5000
Swagger UI: http://localhost:5000/swagger
================================================================================
```

### 3. Swagger UI로 테스트

브라우저에서 `http://localhost:5000/swagger` 접속하여 API를 테스트할 수 있습니다.

---

## 게임 매칭 시스템

실제 멀티플레이 게임에서 사용할 수 있는 완전한 매칭 시스템 구현입니다.

### 시스템 개요

#### 주요 기능

1. **플레이어 관리**
   - 플레이어 생성 및 정보 조회
   - MMR (Matchmaking Rating) 관리
   - 현재 상태 추적 (파티, 매칭, 게임 중)

2. **파티 시스템**
   - 파티 생성 및 관리
   - 멤버 참가/탈퇴
   - 리더 자동 위임
   - **트랜잭션 보장** (ACID)

3. **매칭 시스템**
   - **MMR 기반 매칭**
   - 개인 매칭 / 파티 매칭 지원
   - 대기 시간에 따른 MMR 범위 확대
   - 자동 매치 생성 (5초마다)
   - 팀 밸런스 조정

4. **게임 룸**
   - 캐릭터 선택
   - **파티별 캐릭터 중복 허용**
   - 준비 상태 관리
   - 게임 시작 조건 확인

### 아키텍처

```
┌─────────────┐
│   Client    │ (웹/모바일/게임 클라이언트)
└──────┬──────┘
       │ HTTP/REST
       ▼
┌─────────────────────────────────────────────────────────┐
│                     Example.Api                          │
│  (ASP.NET Core Web API + Swagger)                        │
└──────┬──────────────────────────────────────────────────┘
       │ Orleans Client
       │
┌──────▼──────────────────────────────────────────────────┐
│             Orleans Cluster (Example.SiloHost)           │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Grains                                            │ │
│  │  ┌──────────────┐  ┌──────────────┐              │ │
│  │  │ PlayerGrain  │  │  PartyGrain  │              │ │
│  │  │ (Stateful)   │  │ (Transaction)│              │ │
│  │  └──────────────┘  └──────────────┘              │ │
│  │                                                    │ │
│  │  ┌──────────────────┐  ┌──────────────┐          │ │
│  │  │ MatchmakingGrain │  │  RoomGrain   │          │ │
│  │  │   (Singleton)    │  │  (Stateful)  │          │ │
│  │  └──────────────────┘  └──────────────┘          │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  Storage: Memory (개발), SQL Server/PostgreSQL (프로덕션)│
│  Transaction: Memory (개발), Azure Storage (프로덕션)    │
└──────────────────────────────────────────────────────────┘
```

### 데이터 모델

#### PlayerState (플레이어 상태)

```csharp
public class PlayerState
{
    public string PlayerId { get; set; }          // 플레이어 ID
    public string Name { get; set; }              // 이름
    public int Level { get; set; }                // 레벨
    public int Mmr { get; set; }                  // MMR (1000 기본)
    public string? CurrentPartyId { get; set; }   // 현재 파티 ID
    public string? CurrentRoomId { get; set; }    // 현재 룸 ID
    public PlayerMatchStatus MatchStatus { get; set; } // 상태
}

public enum PlayerMatchStatus
{
    Idle,           // 대기 중
    InParty,        // 파티에 속함
    Matchmaking,    // 매칭 중
    InRoom,         // 룸에 입장
    Playing         // 게임 플레이 중
}
```

#### PartyState (파티 상태)

```csharp
public class PartyState
{
    public string PartyId { get; set; }
    public string LeaderId { get; set; }
    public List<PartyMember> Members { get; set; }
    public int MaxMembers { get; set; } = 5;
    public PartyStatus Status { get; set; }
    public bool IsMatchmaking { get; set; }
    public int AverageMmr { get; set; }          // 평균 MMR
}
```

#### RoomState (게임 룸 상태)

```csharp
public class RoomState
{
    public string RoomId { get; set; }
    public List<RoomPlayer> TeamA { get; set; }  // 팀 A (5명)
    public List<RoomPlayer> TeamB { get; set; }  // 팀 B (5명)
    public RoomStatus Status { get; set; }
    public MatchInfo? MatchInfo { get; set; }
}

public class RoomPlayer
{
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
    public int Mmr { get; set; }
    public string? PartyId { get; set; }          // 파티 ID (파티 소속인 경우)
    public string? SelectedCharacterId { get; set; } // 선택한 캐릭터
    public bool IsReady { get; set; }
}
```

### Grain 구현

#### 1. PlayerGrain (StatefulGrainBase)

일반적인 상태 관리를 사용하는 Grain입니다.

```csharp
public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public async Task CreateAsync(string name, int level, int mmr = 1000)
    {
        await UpdateStateAsync(state =>
        {
            state.PlayerId = this.GetPrimaryKeyString();
            state.Name = name;
            state.Level = level;
            state.Mmr = mmr;
        });
    }
    
    // 기타 메서드...
}
```

**사용 라이브러리**: `OrleansX.Grains.StatefulGrainBase`

#### 2. PartyGrain (TransactionalGrainBase) 🆕

ACID 트랜잭션이 보장되는 Grain입니다.

```csharp
public class PartyGrain : TransactionalGrainBase<PartyState>, IPartyGrain
{
    [Transaction(TransactionOption.Create)]
    public async Task CreateAsync(...)
    {
        await UpdateStateAsync(state =>
        {
            // 파티 생성 로직
            // 트랜잭션으로 보호됨 (All-or-Nothing)
        });
        
        // 플레이어 상태도 원자적으로 업데이트
        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(leaderId);
        await playerGrain.JoinPartyAsync(partyId);
    }
    
    [Transaction(TransactionOption.Join)]
    public async Task<bool> JoinAsync(...)
    {
        // 여러 상태 변경이 원자적으로 처리
        return await UpdateStateAsync(state => { ... });
    }
}
```

**사용 라이브러리**: `OrleansX.Grains.TransactionalGrainBase` 🆕  
**특징**: 파티 생성, 참가, 탈퇴 등이 트랜잭션으로 보호되어 일관성 보장

#### 3. MatchmakingGrain (MMR 기반 매칭 로직)

싱글톤 Grain으로 전체 매칭 큐를 관리합니다.

```csharp
public class MatchmakingGrain : StatefulGrainBase<MatchmakingState>
{
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 5초마다 자동으로 매칭 시도
        this.RegisterTimer(_ => TryMatchAsync(), null, 
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        return base.OnActivateAsync(cancellationToken);
    }
    
    public async Task<List<CreatedMatch>> TryMatchAsync()
    {
        // MMR 기반 매칭 알고리즘
        // 1. 모든 큐를 MMR 순으로 정렬
        // 2. Team A 구성 (5명)
        // 3. Team B 찾기 (MMR 범위 내)
        // 4. 매치 생성 및 룸 생성
    }
}
```

**매칭 알고리즘**:
- 대기 시간에 따라 MMR 범위 확대 (10초마다 +50 MMR, 최대 500)
- 개인/파티 매칭 모두 지원
- 각 팀 5명으로 구성
- MMR 밸런스 고려

#### 4. RoomGrain (캐릭터 선택 로직)

게임 룸에서 캐릭터 선택 및 준비 상태를 관리합니다.

```csharp
public class RoomGrain : StatefulGrainBase<RoomState>, IRoomGrain
{
    public async Task<bool> SelectCharacterAsync(string playerId, string characterId)
    {
        // 캐릭터 중복 체크 규칙:
        // - 같은 팀 내에서
        // - 같은 파티가 아닌 경우에만 중복 불가
        // - 다른 파티거나 파티 없으면: 중복 선택 가능
        
        var teamPlayers = playerInTeamA != null ? State.TeamA : State.TeamB;
        var isDuplicate = teamPlayers
            .Where(p => p.PlayerId != playerId)
            .Where(p => p.PartyId != player.PartyId || p.PartyId == null)
            .Any(p => p.SelectedCharacterId == characterId);
        
        if (isDuplicate) return false;
        
        player.SelectedCharacterId = characterId;
        await SaveStateAsync();
        return true;
    }
}
```

**캐릭터 선택 규칙**:
- ✅ 같은 파티 멤버끼리는 같은 캐릭터 선택 가능
- ❌ 다른 파티/개인 매칭 플레이어와는 캐릭터 중복 불가

### API 사용법

전체 API 목록은 Swagger UI(`http://localhost:5000/swagger`)에서 확인할 수 있습니다.

#### 주요 엔드포인트

| 카테고리 | 메서드 | 경로 | 설명 |
|---------|--------|------|------|
| **Players** | POST | `/api/players` | 플레이어 생성 |
| | GET | `/api/players/{playerId}` | 플레이어 정보 조회 |
| **Party** | POST | `/api/parties` | 파티 생성 |
| | GET | `/api/parties/{partyId}` | 파티 정보 조회 |
| | POST | `/api/parties/{partyId}/join` | 파티 참가 |
| | POST | `/api/parties/{partyId}/leave` | 파티 탈퇴 |
| **Matchmaking** | POST | `/api/matchmaking/solo` | 개인 매칭 시작 |
| | POST | `/api/parties/{partyId}/matchmaking/start` | 파티 매칭 시작 |
| | GET | `/api/matchmaking/queue` | 큐 상태 조회 |
| | GET | `/api/matchmaking/history` | 매칭 히스토리 |
| **Room** | GET | `/api/rooms/{roomId}` | 룸 정보 조회 |
| | POST | `/api/rooms/{roomId}/select-character` | 캐릭터 선택 |
| | POST | `/api/rooms/{roomId}/ready` | 준비 완료 |
| | POST | `/api/rooms/{roomId}/start` | 게임 시작 |
| **Characters** | GET | `/api/characters` | 전체 캐릭터 목록 |

---

## 워크플로우

### 1️⃣ 개인 매칭 플로우

```
┌──────────────────────────────────────────────────────────────┐
│               개인 플레이어 매칭 워크플로우                      │
└──────────────────────────────────────────────────────────────┘

1. 플레이어 생성
   POST /api/players
   {
     "playerId": "player-001",
     "name": "Alice",
     "level": 10,
     "mmr": 1200
   }

2. 개인 매칭 시작
   POST /api/matchmaking/solo
   {
     "playerId": "player-001",
     "mmr": 1200
   }

3. 매칭 대기 (자동, 5초마다 시도)
   - MatchmakingGrain이 자동으로 매칭 시도
   - MMR 기반으로 적절한 상대 찾기
   - 대기 시간에 따라 MMR 범위 확대

4. 매치 찾음 → 룸 생성
   - 매칭 완료 시 자동으로 RoomGrain 생성
   - 플레이어 상태 → InRoom으로 변경

5. 캐릭터 선택
   POST /api/rooms/{roomId}/select-character
   {
     "playerId": "player-001",
     "characterId": "char_damage_01"
   }

6. 준비 완료
   POST /api/rooms/{roomId}/ready
   {
     "playerId": "player-001",
     "isReady": true
   }

7. 게임 시작
   - 모든 플레이어 준비 완료 시
   POST /api/rooms/{roomId}/start
```

### 2️⃣ 파티 매칭 플로우

```
┌──────────────────────────────────────────────────────────────┐
│                   파티 매칭 워크플로우                          │
└──────────────────────────────────────────────────────────────┘

1. 리더가 파티 생성 (트랜잭션)
   POST /api/parties
   {
     "leaderId": "player-001",
     "leaderName": "Alice",
     "leaderLevel": 10,
     "leaderMmr": 1200,
     "maxMembers": 5
   }
   → Response: { "partyId": "party-abc-123" }

2. 멤버들이 파티 참가 (트랜잭션)
   POST /api/parties/party-abc-123/join
   {
     "playerId": "player-002",
     "playerName": "Bob",
     "level": 12,
     "mmr": 1150
   }
   
   POST /api/parties/party-abc-123/join
   {
     "playerId": "player-003",
     "playerName": "Charlie",
     "level": 9,
     "mmr": 1250
   }

3. 파티 정보 확인
   GET /api/parties/party-abc-123
   → Response:
   {
     "partyId": "party-abc-123",
     "leaderId": "player-001",
     "members": [
       { "playerId": "player-001", "mmr": 1200 },
       { "playerId": "player-002", "mmr": 1150 },
       { "playerId": "player-003", "mmr": 1250 }
     ],
     "averageMmr": 1200,
     "status": "Waiting"
   }

4. 파티 매칭 시작
   POST /api/parties/party-abc-123/matchmaking/start

5. 매칭 대기 (자동)
   - 파티 단위로 큐에 추가
   - 평균 MMR 기준으로 매칭
   - 다른 파티 또는 개인 플레이어들과 매칭

6. 매치 찾음 → 룸 생성
   - 파티 멤버들이 같은 팀으로 배정
   - 나머지 슬롯은 다른 파티/개인으로 채움

7. 캐릭터 선택 (파티 멤버는 같은 캐릭터 선택 가능)
   POST /api/rooms/{roomId}/select-character
   {
     "playerId": "player-001",
     "characterId": "char_tank_01"
   }
   
   POST /api/rooms/{roomId}/select-character
   {
     "playerId": "player-002",
     "characterId": "char_tank_01"  // ✅ 같은 파티라 가능
   }

8. 모든 멤버 준비 → 게임 시작
```

### 3️⃣ 게임 룸 플로우

```
┌──────────────────────────────────────────────────────────────┐
│                  게임 룸 상세 플로우                           │
└──────────────────────────────────────────────────────────────┘

[매칭 완료 후 자동 생성]

Room 구성:
┌─────────────────────────────────────────┐
│ Team A (5명)        Team B (5명)        │
│ ┌─────────────┐   ┌─────────────┐      │
│ │ Party X     │   │ Party Y     │      │
│ │ - Player 1  │   │ - Player 6  │      │
│ │ - Player 2  │   │ - Player 7  │      │
│ │ - Player 3  │   │             │      │
│ └─────────────┘   └─────────────┘      │
│ ┌─────────────┐   ┌─────────────┐      │
│ │ Solo        │   │ Solo        │      │
│ │ - Player 4  │   │ - Player 8  │      │
│ │ - Player 5  │   │ - Player 9  │      │
│ │             │   │ - Player 10 │      │
│ └─────────────┘   └─────────────┘      │
└─────────────────────────────────────────┘

캐릭터 선택 규칙:
✅ Party X (Player 1, 2, 3) → 같은 캐릭터 선택 가능
❌ Player 4, 5 (Solo) → Party X와 중복 불가
❌ Player 4 ↔ Player 5 → 서로 중복 불가

예시:
- Player 1: char_tank_01 ✅
- Player 2: char_tank_01 ✅ (같은 파티)
- Player 4: char_tank_01 ❌ (다른 파티/Solo)
- Player 4: char_damage_01 ✅

준비 상태:
1. 모든 플레이어가 캐릭터 선택 완료
2. 모든 플레이어가 준비 완료 (isReady = true)
3. CanStartGame() → true
4. StartGame() 호출 가능

게임 시작:
- 모든 조건 만족 시 RoomStatus → InGame
- 플레이어 상태 → Playing
```

---

## 🎮 전체 시나리오 예제 (cURL)

### 시나리오: 3명 파티 + 2명 개인 vs 5명 파티

```bash
# ============================================================
# 1. 플레이어 생성 (8명)
# ============================================================
curl -X POST http://localhost:5000/api/players \
  -H "Content-Type: application/json" \
  -d '{"playerId":"alice","name":"Alice","level":10,"mmr":1200}'

curl -X POST http://localhost:5000/api/players \
  -H "Content-Type: application/json" \
  -d '{"playerId":"bob","name":"Bob","level":12,"mmr":1150}'

curl -X POST http://localhost:5000/api/players \
  -H "Content-Type: application/json" \
  -d '{"playerId":"charlie","name":"Charlie","level":9,"mmr":1250}'

# ... (나머지 플레이어 생성)

# ============================================================
# 2. 파티 A 생성 (3명)
# ============================================================
curl -X POST http://localhost:5000/api/parties \
  -H "Content-Type: application/json" \
  -d '{"leaderId":"alice","leaderName":"Alice","leaderLevel":10,"leaderMmr":1200}'
# → partyId: party-A

curl -X POST http://localhost:5000/api/parties/party-A/join \
  -H "Content-Type: application/json" \
  -d '{"playerId":"bob","playerName":"Bob","level":12,"mmr":1150}'

curl -X POST http://localhost:5000/api/parties/party-A/join \
  -H "Content-Type: application/json" \
  -d '{"playerId":"charlie","playerName":"Charlie","level":9,"mmr":1250}'

# ============================================================
# 3. 개인 매칭 (2명)
# ============================================================
curl -X POST http://localhost:5000/api/matchmaking/solo \
  -H "Content-Type: application/json" \
  -d '{"playerId":"dave","mmr":1180}'

curl -X POST http://localhost:5000/api/matchmaking/solo \
  -H "Content-Type: application/json" \
  -d '{"playerId":"eve","mmr":1220}'

# ============================================================
# 4. 파티 B 생성 (5명) - 상대팀
# ============================================================
curl -X POST http://localhost:5000/api/parties \
  -H "Content-Type: application/json" \
  -d '{"leaderId":"frank","leaderName":"Frank","leaderLevel":11,"leaderMmr":1190}'
# → partyId: party-B

# ... (멤버 4명 추가)

# ============================================================
# 5. 파티 매칭 시작
# ============================================================
curl -X POST http://localhost:5000/api/parties/party-A/matchmaking/start
curl -X POST http://localhost:5000/api/parties/party-B/matchmaking/start

# ============================================================
# 6. 매칭 상태 확인
# ============================================================
curl http://localhost:5000/api/matchmaking/queue
# → { "soloQueue": 2, "partyQueue": 2, "total": 4 }

# 5초 대기 (자동 매칭)...

# ============================================================
# 7. 매칭 완료 → 룸 ID 받기
# ============================================================
curl http://localhost:5000/api/matchmaking/history
# → roomId 확인

# ============================================================
# 8. 캐릭터 선택
# ============================================================
curl -X POST http://localhost:5000/api/rooms/{roomId}/select-character \
  -H "Content-Type: application/json" \
  -d '{"playerId":"alice","characterId":"char_tank_01"}'

curl -X POST http://localhost:5000/api/rooms/{roomId}/select-character \
  -H "Content-Type: application/json" \
  -d '{"playerId":"bob","characterId":"char_tank_01"}'  # ✅ 같은 파티

# ============================================================
# 9. 준비 및 게임 시작
# ============================================================
curl -X POST http://localhost:5000/api/rooms/{roomId}/ready \
  -H "Content-Type: application/json" \
  -d '{"playerId":"alice","isReady":true}'

# ... (모든 플레이어 준비)

curl -X POST http://localhost:5000/api/rooms/{roomId}/start
```

---

## 📊 OrleansX 프레임워크 기능 활용

이 예제에서 사용된 OrleansX 기능들:

| 기능 | 사용 위치 | 설명 |
|------|-----------|------|
| **StatefulGrainBase** | PlayerGrain, MatchmakingGrain, RoomGrain | 일반 상태 관리 |
| **🆕 TransactionalGrainBase** | PartyGrain | 트랜잭션 보장 (ACID) |
| **GrainInvoker** | Example.Api | 재시도 및 멱등성 내장 |
| **OrleansXSiloOptions** | Example.SiloHost | 표준화된 설정 |
| **Fluent API** | Example.SiloHost | 간결한 Silo 설정 |

---

## 🎓 학습 포인트

### 1. Grain 설계 패턴

- **단일 책임**: 각 Grain은 하나의 엔티티만 관리
- **느슨한 결합**: Grain 간 인터페이스를 통한 통신
- **상태 캡슐화**: 외부에서 직접 상태 수정 불가

### 2. 트랜잭션 사용 시기 🆕

✅ **사용해야 할 때**:
- 여러 Grain 간 일관성이 중요한 경우 (파티 생성)
- 금융 거래, 재고 관리 등 ACID가 필수인 경우
- 롤백이 필요한 복잡한 비즈니스 로직

❌ **사용하지 말아야 할 때**:
- 단순 조회 작업
- 성능이 매우 중요한 경우
- 트랜잭션 범위가 너무 큰 경우

### 3. MMR 기반 매칭 전략

- 대기 시간이 길어질수록 MMR 범위 확대
- 팀 밸런스 고려 (평균 MMR)
- 파티 우선 배정

### 4. 캐릭터 선택 로직

- 파티 멤버끼리는 유연하게
- 개인 플레이어 간에는 엄격하게
- 게임 밸런스와 플레이어 경험 고려

---

## 🔧 커스터마이징

### MMR 범위 조정

```csharp
// MatchmakingGrain.cs
public int GetMmrRange()
{
    var waitTime = DateTime.UtcNow - EnqueuedAt;
    var baseRange = 100;  // 기본 범위 조정
    var expandedRange = (int)(waitTime.TotalSeconds / 10) * 50;  // 확장 속도 조정
    return Math.Min(baseRange + expandedRange, 500);  // 최대 범위 조정
}
```

### 팀 크기 변경

```csharp
// MatchmakingGrain.cs
private const int TeamSize = 5;  // 원하는 팀 크기로 변경
```

### 캐릭터 추가

```csharp
// CharacterInfo.cs
public static class Characters
{
    public static readonly List<CharacterInfo> All = new()
    {
        new CharacterInfo { CharacterId = "new_char", Name = "New Hero", ... }
    };
}
```

---

## 📝 다음 단계

1. **프로덕션 설정**
   - SQL Server 또는 PostgreSQL로 영속성 변경
   - Azure Storage로 트랜잭션 로그 변경
   - Redis 클러스터링 추가

2. **추가 기능 구현**
   - 친구 시스템
   - 채팅
   - 랭킹/리더보드
   - 게임 결과 저장

3. **모니터링 추가**
   - Orleans Dashboard 설치
   - Application Insights 연동
   - 커스텀 메트릭

---

## 📄 라이선스

MIT License

---

## 🤝 기여

이슈나 개선사항이 있으면 PR을 올려주세요!

