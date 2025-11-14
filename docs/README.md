# OrleansX 상세 문서

OrleansX 프레임워크의 구현 내용에 대한 심층 가이드입니다.

## 📚 작성된 문서 목록

### 클라이언트 SDK ✅
- [GrainInvoker](Client/GrainInvoker.md) - Grain 호출 래퍼 및 Facade 패턴 (600+ 줄)
- [재시도 정책 (Retry Policy)](Client/RetryPolicy.md) - 자동 재시도 메커니즘 (지수 백오프) (550+ 줄)
- [멱등성 (Idempotency)](Client/Idempotency.md) - 중복 요청 방지 패턴 (500+ 줄)

### 고급 주제 ✅
- [C# Record 타입](Advanced/CSharpRecords.md) - 위치 기반 레코드 문법 설명 (550+ 줄)
- [의존성 주입 (DI)](Advanced/DependencyInjection.md) - AddSingleton 패턴 이해 (500+ 줄)
- [AsyncLocal](Advanced/AsyncLocal.md) - 비동기 컨텍스트 데이터 전달 (450+ 줄)
- [WorkerExecutor 내부 구조](Advanced/WorkerExecutor.md) - WorkerExecutor 심층 분석 (550+ 줄)

### 튜토리얼 (examples/Tutorials)
OrleansX의 실전 사용법은 [튜토리얼 가이드](../examples/Tutorials/README.md)를 참고하세요:
- 01-StatelessGrain - 상태 없는 Grain
- 02-StatefulGrain - 영속 상태를 가진 Grain
- 03-TransactionalGrain - ACID 트랜잭션 Grain
- 04-WorkerGrains - 백그라운드 작업 자동화
- 05-ClientSDK - 클라이언트 SDK 고급 기능
- 06-Streams - 실시간 데이터 스트림 처리

---

## 🎯 빠른 링크

### 처음 시작하는 경우
1. [튜토리얼 가이드](../examples/Tutorials/README.md) - 단계별 학습 (입문자용)
2. [C# Record 타입](Advanced/CSharpRecords.md) - OrleansX에서 많이 사용하는 문법
3. [의존성 주입 (DI)](Advanced/DependencyInjection.md) - DI 패턴 이해

### 클라이언트 개발
1. [GrainInvoker](Client/GrainInvoker.md) - Grain 호출 방법
2. [재시도 정책 (Retry Policy)](Client/RetryPolicy.md) - 안정적인 호출
3. [멱등성 (Idempotency)](Client/Idempotency.md) - 중복 방지

### 문제 해결
- **"Record 타입이 이해가 안 돼요"**: [C# Record 타입](Advanced/CSharpRecords.md)
- **"DI가 어떻게 동작하나요?"**: [의존성 주입](Advanced/DependencyInjection.md)
- **"AsyncLocal이 뭔가요?"**: [AsyncLocal](Advanced/AsyncLocal.md)
- **"WorkerExecutor는 왜 만들었나요?"**: [WorkerExecutor 내부 구조](Advanced/WorkerExecutor.md)
- **"재시도는 어떻게 동작하나요?"**: [재시도 정책](Client/RetryPolicy.md)
- **"멱등성은 왜 필요한가요?"**: [멱등성](Client/Idempotency.md)

---

## 📖 문서 작성 철학

이 문서는 다음 원칙을 따릅니다:

1. **실용성**: 실제 코드 예제와 함께 설명
2. **완전성**: "왜?"에 대한 답변 제공
3. **점진적 학습**: 기초부터 고급까지 단계별 구성
4. **시각화**: 다이어그램과 흐름도 활용
5. **실전 중심**: 게임 서버, 엔터프라이즈 시나리오 포함

---

## 🤝 기여하기

문서 개선 제안은 언제나 환영합니다!
- 오타 수정, 예제 추가, 번역 등 자유롭게 PR 보내주세요
- 이해하기 어려운 부분이 있다면 Issue로 알려주세요

---

## 📝 라이선스

이 문서는 OrleansX 프로젝트의 일부로 MIT 라이선스를 따릅니다.
