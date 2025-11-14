# OrleansX Tutorials

OrleansX의 모든 기능을 단계별로 학습할 수 있는 종합 튜토리얼입니다.

## 튜토리얼 목록

### 1. [Stateless Grain](01-StatelessGrain/README.md)
상태를 저장하지 않는 가장 기본적인 Grain을 학습합니다.

**학습 내용:**
- Stateless Grain 개념
- IGrainInvoker 사용법
- 계산기 예제

**난이도:** ⭐ 초급

---

### 2. [Stateful Grain](02-StatefulGrain/README.md)
영속적인 상태를 가지는 Grain을 학습합니다.

**학습 내용:**
- 상태 관리 (State, SaveStateAsync)
- 직렬화 (`[GenerateSerializer]`, `[Id]`)
- 플레이어 정보 관리 예제

**난이도:** ⭐⭐ 초급-중급

---

### 3. [Transactional Grain](03-TransactionalGrain/README.md)
ACID 트랜잭션을 지원하는 Grain을 학습합니다.

**학습 내용:**
- 트랜잭션 개념 (ACID)
- Transaction 속성 사용법
- 은행 계좌 이체 예제

**난이도:** ⭐⭐⭐ 중급

---

### 4. [Worker Grains](04-WorkerGrains/README.md)
주기적인 백그라운드 작업을 수행하는 Grain을 학습합니다.

**학습 내용:**
- Stateless/Stateful Worker Grain
- Timer 기반 스케줄링
- 정리 작업 및 통계 집계 예제

**난이도:** ⭐⭐ 초급-중급

---

### 5. [Client SDK](05-ClientSDK/README.md)
고급 클라이언트 기능을 학습합니다.

**학습 내용:**
- IGrainInvoker 심화
- 재시도 정책 (Retry Policy)
- 멱등성 관리 (Idempotency)
- 고급 패턴 (Circuit Breaker, Batch Operations)

**난이도:** ⭐⭐⭐ 중급

---

### 6. [Streams](06-Streams/README.md)
실시간 데이터 스트림 처리를 학습합니다.

**학습 내용:**
- Orleans Streams 개념
- Producer/Consumer 패턴
- Stream 필터링 및 배치 처리
- 실시간 알림 시스템 예제

**난이도:** ⭐⭐⭐⭐ 중급-고급

---

## 학습 경로

### 입문자 (Orleans를 처음 접하는 경우)
1. **Stateless Grain** - Orleans의 기본 개념 이해
2. **Stateful Grain** - 상태 관리 학습
3. **Client SDK** - 클라이언트 사용법 학습

### 중급자 (Orleans 기본을 이해한 경우)
1. **Worker Grains** - 백그라운드 작업 패턴
2. **Transactional Grain** - 트랜잭션 처리
3. **Streams** - 실시간 이벤트 처리

### 고급 사용자
모든 튜토리얼을 순서대로 학습하여 OrleansX의 전체 기능을 마스터하세요.

---

## 시작하기

### 필수 요구사항

- .NET 9.0 SDK 이상
- Visual Studio 2022 또는 VS Code
- 기본적인 C# 및 비동기 프로그래밍 지식

### 설치

```bash
# 저장소 클론
git clone https://github.com/your-repo/OrleansX.git
cd OrleansX

# 프로젝트 빌드
dotnet build
```

### 각 튜토리얼 실행 방법

각 튜토리얼 디렉토리에는 다음 파일들이 포함되어 있습니다:
- `README.md` - 튜토리얼 설명 및 코드 예제
- 코드 예제는 README에 인라인으로 포함되어 있습니다

실제 실행 가능한 예제는 각 튜토리얼의 README를 참고하여 직접 프로젝트를 생성하세요.

---

## 튜토리얼별 주요 개념

| 튜토리얼 | 핵심 클래스 | 사용 사례 |
|---------|-----------|---------|
| Stateless Grain | `StatelessGrainBase` | API 호출, 계산 작업 |
| Stateful Grain | `StatefulGrainBase<T>` | 사용자 정보, 게임 상태 |
| Transactional Grain | `TransactionalGrainBase<T>` | 결제, 재고 관리 |
| Worker Grains | `StatelessWorkerGrainBase`<br>`StatefulWorkerGrainBase<T>` | 정리 작업, 통계 집계 |
| Client SDK | `IGrainInvoker`<br>`IRetryPolicy` | 클라이언트 로직 |
| Streams | `IAsyncStream<T>` | 실시간 알림, 이벤트 |

---

## 추가 리소스

### 공식 문서
- [Orleans 공식 문서](https://learn.microsoft.com/orleans)
- [Orleans GitHub](https://github.com/dotnet/orleans)

### OrleansX 소스 코드
- `src/OrleansX.Abstractions` - 공용 인터페이스
- `src/OrleansX.Grains` - Grain 베이스 클래스
- `src/OrleansX.Client` - 클라이언트 SDK
- `src/OrleansX.Silo.Hosting` - Silo 호스팅 확장

### 커뮤니티
- GitHub Issues - 버그 리포트 및 기능 요청
- Discussions - 질문 및 토론

---

## 기여하기

튜토리얼 개선 아이디어나 새로운 예제가 있으시면 Pull Request를 환영합니다!

### 기여 가이드라인
1. 코드는 명확하고 이해하기 쉬워야 합니다
2. 실행 가능한 예제를 포함해야 합니다
3. 충분한 주석과 설명을 추가해야 합니다

---

## 라이선스

이 튜토리얼은 OrleansX 프로젝트의 일부로 MIT 라이선스 하에 제공됩니다.

---

## 피드백

튜토리얼에 대한 의견이나 개선 제안이 있으시면 이슈를 등록해주세요!

**Happy Learning!** 🎓
