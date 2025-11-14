# Tutorial 03: Transactional Grain

## 개요

Transactional Grain은 ACID 트랜잭션을 지원하는 Grain입니다. 여러 Grain의 상태를 원자적으로 업데이트할 수 있으며, 실패 시 자동으로 롤백됩니다.

## 언제 사용하나요?

- 은행 계좌 이체
- 재고 관리 및 주문 처리
- 포인트/화폐 거래
- 여러 엔티티 간의 일관성이 중요한 작업

## 예제: 은행 계좌 이체

### 1. 상태 모델 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.TransactionalGrain;

[GenerateSerializer]
public class AccountState
{
    [Id(0)]
    public string AccountId { get; set; } = string.Empty;

    [Id(1)]
    public string Owner { get; set; } = string.Empty;

    [Id(2)]
    public decimal Balance { get; set; } = 0;

    [Id(3)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Id(4)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2. Grain 인터페이스 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.TransactionalGrain;

public interface IAccountGrain : IGrainWithStringKey
{
    [Transaction(TransactionOption.Join)]
    Task<decimal> GetBalanceAsync();

    [Transaction(TransactionOption.Join)]
    Task DepositAsync(decimal amount);

    [Transaction(TransactionOption.Join)]
    Task WithdrawAsync(decimal amount);

    [Transaction(TransactionOption.Create)]
    Task TransferToAsync(string targetAccountId, decimal amount);
}

[GenerateSerializer]
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}
```

### 3. Grain 구현

```csharp
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.TransactionalGrain;

public class AccountGrain : TransactionalGrainBase<AccountState>, IAccountGrain
{
    private readonly IGrainInvoker _invoker;

    public AccountGrain(
        [TransactionalState("account")] ITransactionalState<AccountState> state,
        ILogger<AccountGrain> logger,
        IGrainInvoker invoker)
        : base(state, logger)
    {
        _invoker = invoker;
    }

    public Task<decimal> GetBalanceAsync()
    {
        return Task.FromResult(State.Balance);
    }

    public Task DepositAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));
        }

        State.Balance += amount;
        State.UpdatedAt = DateTime.UtcNow;

        Logger.LogInformation("Account {AccountId} deposited {Amount}. New balance: {Balance}",
            this.GetPrimaryKeyString(), amount, State.Balance);

        return Task.CompletedTask;
    }

    public Task WithdrawAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
        }

        if (State.Balance < amount)
        {
            Logger.LogWarning("Account {AccountId} has insufficient funds. Balance: {Balance}, Requested: {Amount}",
                this.GetPrimaryKeyString(), State.Balance, amount);
            throw new InsufficientFundsException(
                $"Insufficient funds. Balance: {State.Balance}, Requested: {amount}");
        }

        State.Balance -= amount;
        State.UpdatedAt = DateTime.UtcNow;

        Logger.LogInformation("Account {AccountId} withdrew {Amount}. New balance: {Balance}",
            this.GetPrimaryKeyString(), amount, State.Balance);

        return Task.CompletedTask;
    }

    [Transaction(TransactionOption.Create)]
    public async Task TransferToAsync(string targetAccountId, decimal amount)
    {
        Logger.LogInformation("Starting transfer from {From} to {To} amount {Amount}",
            this.GetPrimaryKeyString(), targetAccountId, amount);

        // 현재 계좌에서 출금
        await WithdrawAsync(amount);

        // 대상 계좌에 입금
        var targetAccount = _invoker.GetGrain<IAccountGrain>(targetAccountId);
        await targetAccount.DepositAsync(amount);

        Logger.LogInformation("Transfer completed from {From} to {To} amount {Amount}",
            this.GetPrimaryKeyString(), targetAccountId, amount);
    }
}
```

### 4. 클라이언트에서 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.TransactionalGrain;

public class BankingService
{
    private readonly IGrainInvoker _invoker;

    public BankingService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task DemoTransferAsync()
    {
        var account1 = _invoker.GetGrain<IAccountGrain>("account-001");
        var account2 = _invoker.GetGrain<IAccountGrain>("account-002");

        // 초기 잔액 설정
        await account1.DepositAsync(1000m);
        await account2.DepositAsync(500m);

        Console.WriteLine($"Account 1 Balance: {await account1.GetBalanceAsync()}");
        Console.WriteLine($"Account 2 Balance: {await account2.GetBalanceAsync()}");

        // 이체 실행
        try
        {
            await account1.TransferToAsync("account-002", 300m);
            Console.WriteLine("Transfer successful!");

            Console.WriteLine($"Account 1 Balance: {await account1.GetBalanceAsync()}");
            Console.WriteLine($"Account 2 Balance: {await account2.GetBalanceAsync()}");
        }
        catch (InsufficientFundsException ex)
        {
            Console.WriteLine($"Transfer failed: {ex.Message}");
        }

        // 잔액 부족 이체 시도
        try
        {
            await account1.TransferToAsync("account-002", 2000m);
        }
        catch (InsufficientFundsException ex)
        {
            Console.WriteLine($"Transfer failed as expected: {ex.Message}");

            // 롤백 확인
            Console.WriteLine($"Account 1 Balance (after rollback): {await account1.GetBalanceAsync()}");
            Console.WriteLine($"Account 2 Balance (after rollback): {await account2.GetBalanceAsync()}");
        }
    }
}
```

## 실행 방법

### Silo 구성 (메모리 트랜잭션 스토리지)

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(options =>
        {
            options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
            options.UseInMemoryTransactionLog();
            options.AddMemoryGrainStorage("account");
        });
    });

await builder.Build().RunAsync();
```

### Silo 구성 (Azure Storage)

```csharp
siloBuilder.UseOrleansX(options =>
{
    options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
    options.UseAzureTransactionLog("your-connection-string");
    options.AddAzureTableGrainStorage("account", opt =>
    {
        opt.ConfigureTableServiceClient("your-connection-string");
    });
});
```

## 주요 특징

### 1. Transaction 속성

```csharp
[Transaction(TransactionOption.Create)]    // 새 트랜잭션 시작
[Transaction(TransactionOption.Join)]      // 기존 트랜잭션에 참여
[Transaction(TransactionOption.CreateOrJoin)] // 있으면 참여, 없으면 생성
[Transaction(TransactionOption.Suppress)]  // 트랜잭션 없이 실행
[Transaction(TransactionOption.NotAllowed)] // 트랜잭션 내에서 호출 불가
```

### 2. ACID 보장
- **Atomicity (원자성)**: 모든 작업이 성공하거나 모두 실패
- **Consistency (일관성)**: 트랜잭션 전후 데이터 일관성 유지
- **Isolation (격리성)**: 동시 트랜잭션 간 격리
- **Durability (지속성)**: 커밋된 트랜잭션은 영구 저장

### 3. 자동 롤백
```csharp
// 예외 발생 시 모든 변경사항 자동 롤백
await account1.WithdrawAsync(100); // 성공
await account2.DepositAsync(100);  // 실패 → 첫 번째 작업도 롤백
```

## 실행 예제

```bash
# Silo 실행
cd Tutorials/03-TransactionalGrain/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/03-TransactionalGrain/Client
dotnet run
```

## 예상 출력

```
Account 1 Balance: 1000
Account 2 Balance: 500
Transfer successful!
Account 1 Balance: 700
Account 2 Balance: 800
Transfer failed as expected: Insufficient funds. Balance: 700, Requested: 2000
Account 1 Balance (after rollback): 700
Account 2 Balance (after rollback): 800
```

## Best Practices

### 1. 트랜잭션 범위 최소화
```csharp
// ✅ 좋은 예 - 필요한 부분만 트랜잭션
[Transaction(TransactionOption.Create)]
public async Task TransferAsync(...)
{
    // 트랜잭션 내에서만 상태 변경
}

// ❌ 나쁜 예 - 불필요하게 긴 트랜잭션
[Transaction(TransactionOption.Create)]
public async Task ProcessOrderAsync(...)
{
    await SendEmailAsync(); // 트랜잭션 밖으로!
    await UpdateInventoryAsync(); // 트랜잭션 필요
}
```

### 2. 외부 I/O는 트랜잭션 밖에서
```csharp
// ✅ 트랜잭션 완료 후 알림
await account.TransferAsync(...);
await SendNotificationAsync(); // 트랜잭션 외부

// ❌ 트랜잭션 내에서 외부 호출
[Transaction(TransactionOption.Create)]
public async Task TransferAsync(...)
{
    // ...
    await httpClient.PostAsync(...); // 롤백 불가능!
}
```

### 3. 타임아웃 고려
```csharp
// 트랜잭션 타임아웃 설정
siloBuilder.Configure<TransactionalStateOptions>(options =>
{
    options.LockTimeout = TimeSpan.FromSeconds(30);
    options.LockAcquireTimeout = TimeSpan.FromSeconds(10);
});
```

## 트랜잭션 제한사항

### 1. 지원되는 스토리지
- Azure Table Storage
- 메모리 (개발/테스트용)

### 2. 성능 고려사항
- 트랜잭션은 일반 Grain보다 오버헤드가 큽니다
- 읽기 전용 작업은 일반 Grain 사용 권장

### 3. 분산 트랜잭션
- Orleans 트랜잭션은 Grain 간에만 작동
- 외부 데이터베이스와의 2PC는 지원하지 않음

## 다음 단계

- [Tutorial 04: Worker Grains](../04-WorkerGrains/README.md) - 백그라운드 작업 학습
- [Tutorial 02: Stateful Grain](../02-StatefulGrain/README.md) - 일반 상태 관리 학습
